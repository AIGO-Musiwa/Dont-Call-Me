using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 점등 패턴 복사 퍼즐 본체.
/// - 시드 기반으로 길이 9 패턴 생성
/// - 버튼 입력 시 즉시 현재 단계 판정
/// - 정답 입력 시 해당 패드 Emission만 잠깐 On/Off
/// - 오답이면 퍼즐 램프 Emission을 실패 색으로 변경 후 초기화
/// - 성공하면 패드 Emission을 모두 끄고 퍼즐 램프 Emission을 클리어 색으로 유지
/// </summary>
public class LightPatternPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int gridCount = 9;                          // 3x3 전체 칸 수
    [SerializeField] private int patternLength = 9;                      // 정답 길이

    [Header("패드 View")]
    [SerializeField] private List<LightPatternPuzzleView> panelViews = new(); // 패드 Emission View 9개

    [Header("퍼즐 램프 View")]
    [SerializeField] private LightPatternPuzzleView puzzleLampView;      // 퍼즐 상태 Bulb View

    [Header("실패 연출")]
    [SerializeField] private float failLampHoldSeconds = 0.6f;           // 실패 색 유지 시간

    [Header("입력 잠금")]
    [SerializeField] private bool lockInputDuringFailEffect = true;      // 실패 연출 중 입력 잠금 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                 // 디버그 로그 여부

    private readonly List<int> _answerSequence = new();                  // 정답 패턴
    private bool _hasAnswerSeed;                                         // 시드 적용 여부
    private bool _isFailRoutineRunning;                                  // 서버 측 실패 연출 중 여부

    [Networked]
    private int NetCurrentStep { get; set; }                             // 현재 몇 번째 입력까지 맞췄는지

    [Networked, OnChangedRender(nameof(OnSolvedAllChanged))]
    private NetworkBool NetSolvedAll { get; set; }                       // 최종 성공 여부

    [Networked]
    private int NetInputFlashPanelIndex { get; set; }                    // 입력 Emission 대상 패널

    [Networked, OnChangedRender(nameof(OnInputFlashTriggered))]
    private int NetInputFlashSerial { get; set; }                        // 입력 Emission 이벤트 카운터

    [Networked, OnChangedRender(nameof(OnLampStateChanged))]
    private LightPatternLampState NetLampState { get; set; }             // 퍼즐 램프 상태

    public override void Spawned()
    {
        base.Spawned();

        ApplyPuzzlePresentation();
    }

    /// <summary>
    /// 같은 seed로 길이 9 정답 패턴을 생성한다.
    /// 패널 인덱스는 0~8, 중복 허용.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerSequence.Clear();

        SeedRandom rng = new SeedRandom(seed);

        for (int i = 0; i < patternLength; i++)
        {
            int panelIndex = rng.NextInt(0, gridCount);
            _answerSequence.Add(panelIndex);
        }

        _hasAnswerSeed = true;
        _isFailRoutineRunning = false;

        if (HasStateAuthority)
        {
            NetCurrentStep = 0;
            NetSolvedAll = false;
            NetInputFlashPanelIndex = -1;
            NetInputFlashSerial = 0;
            NetLampState = LightPatternLampState.Normal;
        }

        ApplyPuzzlePresentation();

        Log($"정답 시드 적용 완료 | seed = {seed}");
        LogSequenceDebug();
    }

    /// <summary>
    /// 패널 입력 시 즉시 판정한다.
    /// </summary>
    public void OnPanelPressed(int interactableId)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (!_hasAnswerSeed)
        {
            Log("아직 정답 시드가 적용되지 않아 무시");
            return;
        }

        if (_isFailRoutineRunning && lockInputDuringFailEffect)
            return;

        if (!IsValidPanelIndex(interactableId))
            return;

        int expectedPanelIndex = _answerSequence[NetCurrentStep];

        if (interactableId != expectedPanelIndex)
        {
            Log($"오답 입력 | step = {NetCurrentStep} | input = {interactableId} | expected = {expectedPanelIndex}");
            MarkFailed();
            StartCoroutine(CoHandleFail());
            return;
        }

        bool willSolve = (NetCurrentStep + 1) >= patternLength;

        NetCurrentStep++;
        Log($"정답 입력 | currentStep = {NetCurrentStep}/{patternLength}");

        if (willSolve)
        {
            MarkSolved();
            NetSolvedAll = true;
            NetLampState = LightPatternLampState.Solved;

            TurnOffAllPanelsImmediate();
            ApplyLampPresentation();

            Log("점등 패턴 퍼즐 성공");
            return;
        }

        TriggerInputFlash(interactableId);
    }

    /// <summary>
    /// 실패 시 퍼즐 램프를 실패 색으로 변경하고 진행 상태를 초기화한다.
    /// 패드 실패 빨간불은 사용하지 않는다.
    /// </summary>
    private IEnumerator CoHandleFail()
    {
        _isFailRoutineRunning = true;

        TurnOffAllPanelsImmediate();

        NetLampState = LightPatternLampState.Failed;
        ApplyLampPresentation();

        yield return new WaitForSeconds(failLampHoldSeconds);

        NetCurrentStep = 0;

        if (!NetSolvedAll)
        {
            NetLampState = LightPatternLampState.Normal;
            ApplyLampPresentation();
        }

        _isFailRoutineRunning = false;

        Log("점등 패턴 퍼즐 초기화");
    }

    /// <summary>
    /// 패드 입력 Emission 이벤트를 발생시킨다.
    /// </summary>
    private void TriggerInputFlash(int panelIndex)
    {
        NetInputFlashPanelIndex = panelIndex;
        NetInputFlashSerial++;

        PlayInputFlashOnPanel(panelIndex);
    }

    /// <summary>
    /// 입력 Emission 이벤트가 들어오면 모든 클라이언트에서 해당 패드 Emission을 잠깐 켠다.
    /// </summary>
    private void OnInputFlashTriggered()
    {
        if (NetSolvedAll)
            return;

        PlayInputFlashOnPanel(NetInputFlashPanelIndex);
    }

    /// <summary>
    /// 성공 상태 변경 시 패드와 램프 상태를 반영한다.
    /// </summary>
    private void OnSolvedAllChanged()
    {
        ApplyPuzzlePresentation();
    }

    /// <summary>
    /// 램프 상태 변경 시 퍼즐 램프 View를 갱신한다.
    /// </summary>
    private void OnLampStateChanged()
    {
        ApplyLampPresentation();
    }

    /// <summary>
    /// 현재 네트워크 상태 기준으로 전체 시각 상태를 반영한다.
    /// </summary>
    private void ApplyPuzzlePresentation()
    {
        if (NetSolvedAll)
        {
            TurnOffAllPanelsImmediate();
            NetLampState = LightPatternLampState.Solved;
        }

        ApplyLampPresentation();
    }

    /// <summary>
    /// 현재 램프 상태를 퍼즐 Bulb Emission에 반영한다.
    /// </summary>
    private void ApplyLampPresentation()
    {
        if (puzzleLampView == null)
            return;

        puzzleLampView.SetPuzzleLampState(NetLampState);
    }

    /// <summary>
    /// 특정 패드 하나의 입력 Emission을 잠깐 켠다.
    /// </summary>
    private void PlayInputFlashOnPanel(int panelIndex)
    {
        if (!IsValidPanelIndex(panelIndex))
            return;

        if (panelIndex >= panelViews.Count)
            return;

        if (panelViews[panelIndex] == null)
            return;

        panelViews[panelIndex].PlayPanelInputFlash();
    }

    /// <summary>
    /// 모든 패드 Emission을 즉시 끈다.
    /// </summary>
    private void TurnOffAllPanelsImmediate()
    {
        for (int i = 0; i < panelViews.Count; i++)
        {
            if (panelViews[i] == null)
                continue;

            panelViews[i].TurnOffImmediate();
        }
    }

    /// <summary>
    /// 유효한 패널 인덱스인지 검사한다.
    /// </summary>
    private bool IsValidPanelIndex(int panelIndex)
    {
        return panelIndex >= 0 && panelIndex < gridCount;
    }

    /// <summary>
    /// 루트 퍼즐 자체는 직접 상호작용하지 않는다.
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    /// <summary>
    /// 디버그용 정답 패턴 로그를 출력한다.
    /// </summary>
    private void LogSequenceDebug()
    {
        if (!enableDebugLog)
            return;

        string answer = string.Join(", ", _answerSequence);
        Debug.Log($"[LightPatternPuzzle] 정답 패턴 = [{answer}]", this);
    }

    /// <summary>
    /// 디버그 로그를 출력한다.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[LightPatternPuzzle] {message}", this);
    }
}