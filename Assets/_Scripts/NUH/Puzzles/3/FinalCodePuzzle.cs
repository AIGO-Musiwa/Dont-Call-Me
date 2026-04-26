using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3단계 최종 6자리 코드 입력 퍼즐 본체.
/// 
/// 역할
/// - seed 기반으로 최종 정답/힌트 데이터를 재구성한다.
/// - 숫자 버튼 입력을 최대 6개까지 받는다.
/// - 한 글자 지우기를 처리한다.
/// - 숫자 6개가 모두 입력되면 자동으로 정답 판정한다.
/// - 오답이면 Error - 불일치 점멸 연출 후 입력을 초기화한다.
/// - 정답이면 퍼즐 성공 처리 후 키카드를 스폰한다.
/// </summary>
public class FinalCodePuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private FinalCodeView finalCodeView; // 6자리 입력 / 실패 연출 / 성공 화면 담당 뷰
    [SerializeField] private Transform keycardSpawnPoint; // 키카드 생성 위치

    [Header("키카드 프리팹")]
    [SerializeField] private NetworkObject normalKeycardPrefab; // 일반 키카드 프리팹
    [SerializeField] private NetworkObject masterKeycardPrefab; // 마스터 키카드 프리팹

    [Header("설정")]
    [SerializeField] private int digitCount = 6; // 최종 입력 자릿수
    [SerializeField] private bool lockInputDuringFailEffect = true; // 실패 연출 중 입력 잠금 여부

    [Header("실패 연출 대기")]
    [SerializeField] private float failFlashInterval = 0.15f; // FinalCodeView와 동일 값 권장
    [SerializeField] private int failFlashCount = 3; // FinalCodeView와 동일 값 권장

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private FinalCodeAnswerGenerator.FinalCodeAnswerData _answerData; // seed 기반 최종 정답/힌트 데이터
    private bool _hasAnswerSeed; // 시드 적용 완료 여부
    private bool _isFailRoutineRunning; // 실패 연출 코루틴 실행 중 여부
    private NetworkObject _spawnedKeycard; // 이미 생성된 키카드 참조
    private Zone _puzzleZone; // 스폰 매니저가 주입하는 실제 소속 Zone

    [Networked, OnChangedRender(nameof(OnInputStateChanged))]
    private int NetInputCount { get; set; } // 현재 입력된 숫자 개수

    [Networked] private int NetDigit0 { get; set; } // 1번째 입력 숫자
    [Networked] private int NetDigit1 { get; set; } // 2번째 입력 숫자
    [Networked] private int NetDigit2 { get; set; } // 3번째 입력 숫자
    [Networked] private int NetDigit3 { get; set; } // 4번째 입력 숫자
    [Networked] private int NetDigit4 { get; set; } // 5번째 입력 숫자
    [Networked] private int NetDigit5 { get; set; } // 6번째 입력 숫자

    [Networked, OnChangedRender(nameof(OnFailFlashTriggered))]
    private int NetFailFlashSerial { get; set; } // 오답 점멸 이벤트 카운터

    public override void Spawned()
    {
        base.Spawned();

        RefreshInputView();

        if (IsSolved)
            ApplySolvedPresentation();
    }

    /// <summary>
    /// 스폰 매니저가 이 최종 퍼즐의 소속 Zone을 주입한다.
    /// </summary>
    public void SetSpawnZone(Zone zone)
    {
        _puzzleZone = zone;
        Log($"소속 Zone 설정 완료 | zone={_puzzleZone}");
    }

    /// <summary>
    /// seed 기반으로 최종 정답/힌트 데이터를 재구성하고 입력 상태를 초기화한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = FinalCodeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;
        _isFailRoutineRunning = false;

        if (HasStateAuthority)
            ResetInputStateOnly();

        if (finalCodeView != null)
            finalCodeView.ResetToDefault();

        RefreshInputView();

        if (IsSolved)
            ApplySolvedPresentation();

        Log($"최종 코드 시드 적용 완료 | seed={seed} | code={_answerData.GetFinalCodeString()}");
    }

    /// <summary>
    /// 외부 숫자 버튼에서 호출하는 숫자 입력 함수.
    /// 숫자 6개가 채워지면 자동으로 정답 판정을 실행한다.
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

        Log($"최종 코드 숫자 입력 | value={digitValue} | count={NetInputCount}/{digitCount}");

        if (NetInputCount >= digitCount)
            EvaluateInput(); // 6자리 입력 완료 시 자동 판정
    }

    /// <summary>
    /// 외부 제어 버튼에서 호출하는 한 글자 삭제 함수.
    /// </summary>
    public void OnBackspacePressed()
    {
        if (!HasStateAuthority)
            return;

        if (!CanEditInput())
            return;

        if (NetInputCount <= 0)
            return;

        RemoveLastDigit();
        RefreshInputView();

        Log($"최종 코드 한 글자 삭제 | count={NetInputCount}/{digitCount}");
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
    /// 현재 입력을 편집(삭제)할 수 있는지 반환한다.
    /// </summary>
    public bool CanEditInput()
    {
        if (IsSolved)
            return false;

        if (!_hasAnswerSeed)
            return false;

        if (_isFailRoutineRunning && lockInputDuringFailEffect)
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
        if (NetInputCount >= 5) digits.Add(NetDigit4);
        if (NetInputCount >= 6) digits.Add(NetDigit5);

        return digits;
    }

    /// <summary>
    /// 현재 퍼즐의 최종 정답 6자리를 반환한다.
    /// </summary>
    public int[] GetFinalAnswerDigits()
    {
        if (_answerData == null || _answerData.FinalDigits == null)
            return null;

        int[] clone = new int[digitCount];
        for (int i = 0; i < digitCount; i++)
            clone[i] = _answerData.FinalDigits[i];

        return clone;
    }

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
            case 0: NetDigit0 = digitValue; break;
            case 1: NetDigit1 = digitValue; break;
            case 2: NetDigit2 = digitValue; break;
            case 3: NetDigit3 = digitValue; break;
            case 4: NetDigit4 = digitValue; break;
            case 5: NetDigit5 = digitValue; break;
            default: return;
        }

        NetInputCount++;
    }

    /// <summary>
    /// 마지막으로 입력한 숫자 하나를 제거한다.
    /// </summary>
    private void RemoveLastDigit()
    {
        switch (NetInputCount)
        {
            case 1: NetDigit0 = 0; break;
            case 2: NetDigit1 = 0; break;
            case 3: NetDigit2 = 0; break;
            case 4: NetDigit3 = 0; break;
            case 5: NetDigit4 = 0; break;
            case 6: NetDigit5 = 0; break;
            default: return;
        }

        NetInputCount--;
    }

    /// <summary>
    /// 현재 입력 6자리와 정답 6자리를 비교한다.
    /// </summary>
    private void EvaluateInput()
    {
        if (_answerData == null || _answerData.FinalDigits == null || _answerData.FinalDigits.Length < digitCount)
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
        TrySpawnRewardKeycard();

        Log("최종 코드 퍼즐 성공");
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

        Log("최종 코드 퍼즐 실패");
    }

    /// <summary>
    /// 실패 연출이 끝난 뒤 입력을 초기화한다.
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
        NetDigit4 = 0;
        NetDigit5 = 0;
    }

    /// <summary>
    /// 성공 시 화면 상태를 전환한다.
    /// </summary>
    private void ApplySolvedPresentation()
    {
        if (finalCodeView == null)
            return;

        finalCodeView.ShowSolvedState();
    }

    /// <summary>
    /// 현재 입력 상태를 뷰에 반영한다.
    /// </summary>
    private void RefreshInputView()
    {
        if (finalCodeView == null)
            return;

        finalCodeView.ApplyInputDigits(GetCurrentInputDigits());
    }

    protected override void HandleSolvedStateChanged()
    {
        if (!IsSolved)
            return;

        ApplySolvedPresentation();
    }

    private void OnInputStateChanged()
    {
        RefreshInputView();
    }

    private void OnFailFlashTriggered()
    {
        if (finalCodeView == null)
            return;

        finalCodeView.PlayFailFlash();
    }

    /// <summary>
    /// 퍼즐 성공 보상 키카드를 스폰한다.
    /// </summary>
    private void TrySpawnRewardKeycard()
    {
        if (!HasStateAuthority)
            return;

        if (_spawnedKeycard != null)
            return;

        if (Runner == null)
            return;

        if (keycardSpawnPoint == null)
        {
            LogWarning("keycardSpawnPoint가 비어 있어 키카드를 생성할 수 없습니다.");
            return;
        }

        bool shouldSpawnMaster = false;

        if (StageManager.Instance != null)
            shouldSpawnMaster = StageManager.Instance.ShouldSpawnMasterKeycardForZone(_puzzleZone);

        NetworkObject rewardPrefab = shouldSpawnMaster ? masterKeycardPrefab : normalKeycardPrefab;

        if (rewardPrefab == null)
        {
            LogWarning($"보상 키카드 프리팹이 비어 있습니다. | master={shouldSpawnMaster}");
            return;
        }

        NetworkObject spawned = Runner.Spawn(
            rewardPrefab,
            keycardSpawnPoint.position,
            keycardSpawnPoint.rotation,
            null);

        if (spawned == null)
        {
            LogWarning("키카드 Spawn 실패");
            return;
        }

        _spawnedKeycard = spawned;

        Log($"키카드 생성 완료 | zone={_puzzleZone} | master={shouldSpawnMaster}");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[FinalCodePuzzle] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[FinalCodePuzzle] {message}", this);
    }
}