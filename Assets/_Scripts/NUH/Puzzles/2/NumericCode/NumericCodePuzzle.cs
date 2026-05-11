using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-1 숫자 입력 퍼즐 본체.
/// 
/// 역할
/// - seed 기반으로 정답 4자리를 재구성한다.
/// - 숫자 버튼 입력을 4개까지 받는다.
/// - 4개가 모두 입력되면 정답 판정한다.
/// - 오답이면 빨간 화면 3회 점멸 후 입력 초기화
/// - 정답이면 퍼즐 클리어 후 3단계 힌트 화면으로 전환
/// - solved 상태가 네트워크로 바뀌면 모든 클라이언트에서 성공 화면을 반영한다.
/// - 성공 시 Stage3HintRoot에 배정된 3단계 힌트를 표시할 수 있다.
/// </summary>
public class NumericCodePuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private NumericCodeView codeView; // 하단 입력 / 실패 연출 / 성공 화면 담당 뷰

    [Header("설정")]
    [SerializeField] private int digitCount = 4; // 최종 입력 자릿수
    [SerializeField] private bool lockInputDuringFailEffect = true; // 실패 연출 중 입력 잠금 여부
    [SerializeField] private bool showStage3HintImmediately = true; // 성공 직후 3단계 힌트 화면으로 바로 전환할지 여부

    [Header("실패 연출 대기")]
    [SerializeField] private float failFlashInterval = 0.15f; // NumericCodeView와 동일 값 권장
    [SerializeField] private int failFlashCount = 3; // NumericCodeView와 동일 값 권장

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData; // seed 기반 정답 데이터
    private bool _hasAnswerSeed; // 시드 적용 완료 여부
    private bool _isFailRoutineRunning; // 실패 연출 중 입력 잠금 여부

    private FinalCodeHintData _stage3HintData; // 이 퍼즐이 표시할 3단계 힌트 데이터
    private bool _hasStage3HintData; // 3단계 힌트 데이터 적용 여부

    [Networked, OnChangedRender(nameof(OnInputStateChanged))]
    private int NetInputCount { get; set; } // 현재 입력된 숫자 개수

    [Networked] private int NetDigit0 { get; set; } // 1번째 입력 숫자
    [Networked] private int NetDigit1 { get; set; } // 2번째 입력 숫자
    [Networked] private int NetDigit2 { get; set; } // 3번째 입력 숫자
    [Networked] private int NetDigit3 { get; set; } // 4번째 입력 숫자

    [Networked, OnChangedRender(nameof(OnFailFlashTriggered))]
    private int NetFailFlashSerial { get; set; } // 실패 연출 이벤트 카운터

    public override void Spawned()
    {
        base.Spawned();

        RefreshInputView();

        if (IsSolved)
            ApplySolvedPresentation();
    }

    /// <summary>
    /// seed 기반으로 정답 데이터를 재구성하고 입력 상태를 초기화한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = NumericCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;
        _isFailRoutineRunning = false;

        if (HasStateAuthority)
            ResetInputStateOnly();

        if (codeView != null)
            codeView.ResetToDefault();

        RefreshInputView();

        if (IsSolved)
            ApplySolvedPresentation();

        Log($"정답 시드 적용 완료 | seed = {seed} | code = {_answerData.GetFinalCodeString()}");
    }

    /// <summary>
    /// 이 퍼즐이 성공 후 표시할 3단계 힌트 데이터를 세팅한다.
    /// 데이터는 Networked 값으로 저장되어 Host뿐 아니라 Client 화면에도 동일하게 반영된다.
    /// </summary>
    public void SetStage3HintData(FinalCodeHintData hintData)
    {
        SetNetworkStage3HintData(hintData);
    }

    /// <summary>
    /// Networked Stage3 힌트 데이터가 변경되었을 때 모든 클라이언트에서 호출된다.
    /// </summary>
    protected override void HandleStage3HintDataChanged()
    {
        RefreshStage3HintViewFromNetwork();

        if (IsSolved)
            ApplySolvedPresentation();
    }

    /// <summary>
    /// Networked Stage3 힌트 데이터를 로컬 캐시와 View에 반영한다.
    /// </summary>
    private bool RefreshStage3HintViewFromNetwork()
    {
        if (!TryGetNetworkStage3HintData(out FinalCodeHintData hintData))
        {
            _stage3HintData = null;
            _hasStage3HintData = false;
            return false;
        }

        _stage3HintData = hintData;
        _hasStage3HintData = true;

        if (codeView != null)
            codeView.ApplyStage3Hint(_stage3HintData);

        return true;
    }

    /// <summary>
    /// 외부 숫자 버튼에서 호출하는 입력 함수.
    /// </summary>
    public void OnDigitPressed(int digitValue)
    {
        if (!HasStateAuthority)
            return;

        if (!CanAcceptDigitInput())
            return;

        if (digitValue < 0 || digitValue > 9)
            return;

        AppendDigit(digitValue);
        RefreshInputView();

        Log($"숫자 입력 | value = {digitValue} | inputCount = {NetInputCount}/{digitCount}");

        if (IsInputComplete())
            EvaluateInput();
    }

    /// <summary>
    /// 현재 퍼즐이 숫자 입력을 받을 수 있는지 반환한다.
    /// </summary>
    public bool CanAcceptDigitInput()
    {
        if (IsSolved)
            return false;

        if (!_hasAnswerSeed)
            return false;

        if (_isFailRoutineRunning && lockInputDuringFailEffect)
            return false;

        if (NetInputCount >= digitCount)
            return false;

        return true;
    }

    /// <summary>
    /// 현재 입력된 숫자들을 순서대로 반환한다.
    /// </summary>
    public List<int> GetCurrentInputDigits()
    {
        List<int> digits = new List<int>(NetInputCount);

        if (NetInputCount >= 1) digits.Add(NetDigit0);
        if (NetInputCount >= 2) digits.Add(NetDigit1);
        if (NetInputCount >= 3) digits.Add(NetDigit2);
        if (NetInputCount >= 4) digits.Add(NetDigit3);

        return digits;
    }

    /// <summary>
    /// 루트 퍼즐 직접 상호작용은 사용하지 않는다.
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    /// <summary>
    /// 입력 숫자 하나를 내부 Networked 상태에 추가한다.
    /// </summary>
    private void AppendDigit(int digitValue)
    {
        switch (NetInputCount)
        {
            case 0:
                NetDigit0 = digitValue;
                break;
            case 1:
                NetDigit1 = digitValue;
                break;
            case 2:
                NetDigit2 = digitValue;
                break;
            case 3:
                NetDigit3 = digitValue;
                break;
            default:
                return;
        }

        NetInputCount++;
    }

    /// <summary>
    /// 현재 입력이 목표 자릿수 완성되었는지 검사한다.
    /// </summary>
    private bool IsInputComplete()
    {
        return NetInputCount >= digitCount;
    }

    /// <summary>
    /// 현재 입력과 정답을 비교한다.
    /// </summary>
    private void EvaluateInput()
    {
        if (_answerData == null || _answerData.FinalDigits.Count < digitCount)
            return;

        List<int> inputDigits = GetCurrentInputDigits();

        bool isCorrect = true;

        for (int i = 0; i < digitCount; i++)
        {
            if (inputDigits[i] != _answerData.FinalDigits[i])
            {
                isCorrect = false;
                break;
            }
        }

        if (isCorrect)
        {
            HandleSolved();
            return;
        }

        HandleFailed();
    }

    /// <summary>
    /// 성공 처리.
    /// </summary>
    private void HandleSolved()
    {
        MarkSolved();
        ApplySolvedPresentation();

        Log("숫자 입력 퍼즐 성공");
    }

    /// <summary>
    /// 실패 처리.
    /// </summary>
    private void HandleFailed()
    {
        MarkFailed();
        NetFailFlashSerial++;

        if (!_isFailRoutineRunning)
            StartCoroutine(CoHandleFailRoutine());

        Log("숫자 입력 퍼즐 실패");
    }

    /// <summary>
    /// 실패 연출 후 입력을 초기화한다.
    /// </summary>
    private IEnumerator CoHandleFailRoutine()
    {
        _isFailRoutineRunning = true;

        float waitSeconds = failFlashInterval * failFlashCount * 2f;
        yield return new WaitForSeconds(waitSeconds);

        if (HasStateAuthority)
            ResetInputStateOnly();

        RefreshInputView();
        _isFailRoutineRunning = false;
    }

    /// <summary>
    /// 입력 상태만 초기화한다.
    /// </summary>
    private void ResetInputStateOnly()
    {
        NetInputCount = 0;
        NetDigit0 = 0;
        NetDigit1 = 0;
        NetDigit2 = 0;
        NetDigit3 = 0;
    }

    /// <summary>
    /// 성공 후 화면 상태를 전환한다.
    /// </summary>
    private void ApplySolvedPresentation()
    {
        if (codeView == null)
            return;

        RefreshStage3HintViewFromNetwork();

        if (_hasStage3HintData)
            codeView.ApplyStage3Hint(_stage3HintData);

        if (showStage3HintImmediately)
        {
            codeView.ShowStage3HintState();
            return;
        }

        codeView.ShowSolvedState();
    }

    /// <summary>
    /// solved 상태가 네트워크로 변경되었을 때 모든 클라이언트에서 호출된다.
    /// </summary>
    protected override void HandleSolvedStateChanged()
    {
        if (!IsSolved)
            return;

        ApplySolvedPresentation();
    }

    /// <summary>
    /// 입력 숫자 상태가 변경되었을 때 화면을 갱신한다.
    /// </summary>
    private void OnInputStateChanged()
    {
        RefreshInputView();
    }

    /// <summary>
    /// 실패 연출 시리얼이 증가했을 때 화면 점멸을 재생한다.
    /// </summary>
    private void OnFailFlashTriggered()
    {
        if (codeView == null)
            return;

        codeView.PlayFailFlash();
    }

    /// <summary>
    /// 현재 입력 상태를 뷰에 반영한다.
    /// </summary>
    private void RefreshInputView()
    {
        if (codeView == null)
            return;

        codeView.ApplyInputDigits(GetCurrentInputDigits());
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[NumericCodePuzzle] {message}", this);
    }
}