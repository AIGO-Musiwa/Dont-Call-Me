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
/// </summary>
public class NumericCodePuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private NumericCodeView codeView; // 하단 입력 / 실패 연출 / 성공 화면 담당 뷰

    [Header("설정")]
    [SerializeField] private int digitCount = 4;                 // 최종 입력 자릿수
    [SerializeField] private bool lockInputDuringFailEffect = true; // 실패 연출 중 입력 잠금 여부
    [SerializeField] private bool showStage3HintImmediately = true; // 성공 직후 3단계 힌트 화면으로 바로 전환할지 여부

    [Header("실패 연출 대기")]
    [SerializeField] private float failFlashInterval = 0.15f; // NumericCodeView와 동일 값 권장
    [SerializeField] private int failFlashCount = 3;          // NumericCodeView와 동일 값 권장

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private NumericCodeAnswerGenerator.NumericCodeAnswerData _answerData; // seed 기반 정답 데이터
    private bool _hasAnswerSeed;                                          // 시드 적용 완료 여부
    private bool _isFailRoutineRunning;                                   // 실패 연출 중 입력 잠금 여부

    [Networked, OnChangedRender(nameof(OnInputStateChanged))]
    private int NetInputCount { get; set; } // 현재 입력된 숫자 개수

    [Networked]
    private int NetDigit0 { get; set; } // 1번째 입력 숫자

    [Networked]
    private int NetDigit1 { get; set; } // 2번째 입력 숫자

    [Networked]
    private int NetDigit2 { get; set; } // 3번째 입력 숫자

    [Networked]
    private int NetDigit3 { get; set; } // 4번째 입력 숫자

    [Networked, OnChangedRender(nameof(OnFailFlashTriggered))]
    private int NetFailFlashSerial { get; set; } // 실패 연출 이벤트 카운터

    public override void Spawned()
    {
        base.Spawned(); // 부모 기본 Spawned 로직 실행

        RefreshInputView(); // 현재 입력 상태를 화면에 반영

        if (IsSolved)
            ApplySolvedPresentation(); // 이미 solved 상태로 스폰되었으면 성공 화면 반영
    }

    /// <summary>
    /// seed 기반으로 정답 데이터를 재구성하고 입력 상태를 초기화한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = NumericCodeAnswerGenerator.Generate(seed); // seed 기반 정답 데이터 생성
        _hasAnswerSeed = true;                                   // 시드 적용 완료 표시
        _isFailRoutineRunning = false;                           // 실패 연출 상태 초기화

        if (HasStateAuthority)
            ResetInputStateOnly(); // 권한 쪽에서 입력 상태 초기화

        if (codeView != null)
            codeView.ResetToDefault(); // 화면 뷰 기본 상태로 초기화

        RefreshInputView(); // 입력 표시 갱신

        if (IsSolved)
            ApplySolvedPresentation(); // seed 적용 시 이미 solved 상태면 성공 화면 반영

        Log($"정답 시드 적용 완료 | seed = {seed} | code = {_answerData.GetFinalCodeString()}");
    }

    /// <summary>
    /// 외부 숫자 버튼에서 호출하는 입력 함수.
    /// </summary>
    public void OnDigitPressed(int digitValue)
    {
        if (!HasStateAuthority)
            return; // 상태 권한 없는 쪽은 실제 입력 처리 불가

        if (!CanAcceptDigitInput())
            return; // 현재 입력 불가 상태면 종료

        if (digitValue < 0 || digitValue > 9)
            return; // 0~9 범위 밖 입력 방어

        AppendDigit(digitValue); // 네트워크 입력 상태에 숫자 추가
        RefreshInputView();      // 화면 갱신

        Log($"숫자 입력 | value = {digitValue} | inputCount = {NetInputCount}/{digitCount}");

        if (IsInputComplete())
            EvaluateInput(); // 자릿수 다 찼으면 정답 판정
    }

    /// <summary>
    /// 현재 퍼즐이 숫자 입력을 받을 수 있는지 반환한다.
    /// 버튼 interactable이 이 함수를 참고할 수 있다.
    /// </summary>
    public bool CanAcceptDigitInput()
    {
        if (IsSolved)
            return false; // solved 상태면 입력 불가

        if (!_hasAnswerSeed)
            return false; // 정답 시드 미적용 상태면 입력 불가

        if (_isFailRoutineRunning && lockInputDuringFailEffect)
            return false; // 실패 연출 중 잠금 옵션이면 입력 불가

        if (NetInputCount >= digitCount)
            return false; // 이미 입력이 가득 찼으면 입력 불가

        return true; // 그 외에는 입력 가능
    }

    /// <summary>
    /// 현재 입력된 숫자들을 순서대로 반환한다.
    /// 예: [5, 3] -> 화면은 _ _ 5 3 형태로 표시
    /// </summary>
    public List<int> GetCurrentInputDigits()
    {
        List<int> digits = new List<int>(NetInputCount); // 현재 입력 수만큼 리스트 생성

        if (NetInputCount >= 1) digits.Add(NetDigit0); // 첫 번째 입력 추가
        if (NetInputCount >= 2) digits.Add(NetDigit1); // 두 번째 입력 추가
        if (NetInputCount >= 3) digits.Add(NetDigit2); // 세 번째 입력 추가
        if (NetInputCount >= 4) digits.Add(NetDigit3); // 네 번째 입력 추가

        return digits; // 현재 입력 숫자 목록 반환
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
                NetDigit0 = digitValue; // 첫 번째 자리에 저장
                break;
            case 1:
                NetDigit1 = digitValue; // 두 번째 자리에 저장
                break;
            case 2:
                NetDigit2 = digitValue; // 세 번째 자리에 저장
                break;
            case 3:
                NetDigit3 = digitValue; // 네 번째 자리에 저장
                break;
            default:
                return; // 그 외는 방어적으로 종료
        }

        NetInputCount++; // 현재 입력 개수 증가
    }

    /// <summary>
    /// 현재 입력이 4자리 완성되었는지 검사한다.
    /// </summary>
    private bool IsInputComplete()
    {
        return NetInputCount >= digitCount; // 목표 자릿수 도달 여부 반환
    }

    /// <summary>
    /// 현재 입력 4자리와 정답 4자리를 비교한다.
    /// </summary>
    private void EvaluateInput()
    {
        if (_answerData == null || _answerData.FinalDigits.Count < digitCount)
            return; // 정답 데이터가 비정상이면 종료

        List<int> inputDigits = GetCurrentInputDigits(); // 현재 입력 숫자 목록 가져오기

        bool isCorrect = true; // 정답 여부 가정

        for (int i = 0; i < digitCount; i++)
        {
            if (inputDigits[i] != _answerData.FinalDigits[i])
            {
                isCorrect = false; // 하나라도 다르면 오답
                break;
            }
        }

        if (isCorrect)
        {
            HandleSolved(); // 정답 처리
            return;
        }

        HandleFailed(); // 오답 처리
    }

    /// <summary>
    /// 성공 처리.
    /// </summary>
    private void HandleSolved()
    {
        MarkSolved(); // solved 상태를 네트워크에 반영

        ApplySolvedPresentation(); // 권한 쪽은 즉시 성공 화면 반영

        Log("숫자 입력 퍼즐 성공");
    }

    /// <summary>
    /// 실패 처리.
    /// </summary>
    private void HandleFailed()
    {
        MarkFailed(); // 공통 실패 훅 호출

        StartCoroutine(CoHandleFail()); // 실패 연출 코루틴 시작

        Log("숫자 입력 퍼즐 실패");
    }

    /// <summary>
    /// 실패 시 빨간 화면 3회 점멸 후 입력 초기화.
    /// </summary>
    private IEnumerator CoHandleFail()
    {
        _isFailRoutineRunning = true; // 실패 연출 중 표시

        TriggerFailFlash(); // 실패 점멸 이벤트 발생

        float waitTime = failFlashInterval * failFlashCount * 2f; // 빨강/흰색 왕복 총 대기 시간
        yield return new WaitForSeconds(waitTime); // 연출 끝날 때까지 대기

        ResetInputStateOnly(); // 입력 상태 초기화
        RefreshInputView();    // 화면 입력 표시 갱신

        if (codeView != null)
            codeView.ResetToDefault(); // 화면 기본 상태로 초기화

        _isFailRoutineRunning = false; // 실패 연출 종료 표시

        Log("오답 연출 종료 후 입력 초기화");
    }

    /// <summary>
    /// 실패 연출 이벤트를 모든 클라이언트에 발생시킨다.
    /// </summary>
    private void TriggerFailFlash()
    {
        NetFailFlashSerial++; // 실패 연출 시리얼 증가로 렌더 콜백 유도

        if (codeView != null)
            codeView.PlayFailFlash(); // 권한 쪽은 즉시 실패 점멸 반영
    }

    /// <summary>
    /// 실패 연출 이벤트가 들어왔을 때 뷰에 반영한다.
    /// </summary>
    private void OnFailFlashTriggered()
    {
        if (codeView == null)
            return; // 뷰 없으면 종료

        if (IsSolved)
            return; // 이미 solved면 실패 연출 무시

        codeView.PlayFailFlash(); // 실패 점멸 반영
    }

    /// <summary>
    /// 입력 상태 변화 시 하단 숫자 표시를 갱신한다.
    /// </summary>
    private void OnInputStateChanged()
    {
        RefreshInputView(); // 현재 입력 상태를 화면에 다시 반영
    }

    /// <summary>
    /// 현재 Networked 입력 상태를 뷰에 반영한다.
    /// </summary>
    private void RefreshInputView()
    {
        if (codeView == null)
            return; // 뷰 없으면 종료

        codeView.ApplyInputDigits(GetCurrentInputDigits()); // 현재 입력 숫자를 화면에 반영
    }

    /// <summary>
    /// 입력 상태만 초기화한다.
    /// </summary>
    private void ResetInputStateOnly()
    {
        NetInputCount = 0; // 입력 개수 초기화
        NetDigit0 = 0;     // 첫 번째 숫자 초기화
        NetDigit1 = 0;     // 두 번째 숫자 초기화
        NetDigit2 = 0;     // 세 번째 숫자 초기화
        NetDigit3 = 0;     // 네 번째 숫자 초기화
    }

    /// <summary>
    /// 성공 후 화면 표시 상태를 적용한다.
    /// </summary>
    private void ApplySolvedPresentation()
    {
        if (codeView == null)
            return; // 뷰 없으면 종료

        if (showStage3HintImmediately)
        {
            codeView.ShowStage3HintState(); // 바로 3단계 힌트 화면으로 전환
            return;
        }

        codeView.ShowSolvedState(); // 성공 화면 표시
    }

    /// <summary>
    /// solved 상태가 네트워크로 변경되었을 때 모든 클라이언트에서 호출된다.
    /// Host/Client 관계없이 성공 화면 전환을 동일하게 반영한다.
    /// </summary>
    protected override void HandleSolvedStateChanged()
    {
        if (!IsSolved)
            return; // solved가 아닌 상태 변화는 무시

        ApplySolvedPresentation(); // 성공 화면 반영
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return; // 로그 꺼져 있으면 종료

        Debug.Log($"[NumericCodePuzzle] {message}", this); // 디버그 로그 출력
    }
}