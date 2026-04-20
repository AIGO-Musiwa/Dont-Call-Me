using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 점등 패턴 복사 퍼즐 본체
/// - 시드 기반으로 길이 9 패턴 생성
/// - 버튼 입력 시 즉시 현재 단계 판정
/// - 오답이면 즉시 실패 후 전체 패널 3회 빨간 깜빡임
/// - 9개를 모두 맞추면 전체 패널이 초록색으로 계속 켜진다
/// - 이해하기 쉬운 네트워크 구조로 리팩토링:
///   진행 상태는 Networked 값,
///   짧은 연출은 Networked 이벤트 카운터로 동기화
/// </summary>
public class LightPatternPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int gridCount = 9;         // 3x3 전체 칸 수
    [SerializeField] private int patternLength = 9;     // 정답 길이

    [Header("패널 뷰")]
    [SerializeField] private List<LightPatternPanelView> panelViews = new(); // 패널 뷰 9개

    [Header("실패 깜빡임")]
    [SerializeField] private float failFlashOnTime = 0.2f; // 실패 시 각 깜빡임 유지 시간
    [SerializeField] private int failFlashCount = 3;       // 전체 깜빡임 횟수

    [Header("입력 잠금")]
    [SerializeField] private bool lockInputDuringFailEffect = true; // 실패 연출 중 입력 잠금 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerSequence = new(); // 정답 패턴
    private bool _hasAnswerSeed;                        // 시드 적용 여부
    private bool _isFailRoutineRunning;                 // 서버 측 실패 연출 중 여부

    // ===== 진행 상태 =====
    [Networked]
    private int NetCurrentStep { get; set; } // 현재 몇 번째 입력까지 맞췄는지

    [Networked, OnChangedRender(nameof(OnSolvedAllChanged))]
    private NetworkBool NetSolvedAll { get; set; } // 성공 후 전체 초록 유지 여부

    // ===== 짧은 입력 연출 동기화 =====
    [Networked]
    private int NetInputFlashPanelIndex { get; set; } // 노랑 연출 대상 패널

    [Networked, OnChangedRender(nameof(OnInputFlashTriggered))]
    private int NetInputFlashSerial { get; set; } // 노랑 연출 이벤트 발생 카운터

    // ===== 짧은 실패 연출 동기화 =====
    [Networked, OnChangedRender(nameof(OnFailFlashTriggered))]
    private int NetFailFlashSerial { get; set; } // 빨강 연출 이벤트 발생 카운터

    public override void Spawned()
    {
        // 스폰 시 현재 네트워크 상태 기준으로 뷰 초기화
        ApplySolvedPresentation();
    }

    /// <summary>
    /// 같은 seed로 길이 9 정답 패턴 생성
    /// 패널 인덱스는 0~8, 중복 허용
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerSequence.Clear();

        SeedRandom rng = new SeedRandom(seed);

        for (int i = 0; i < patternLength; i++)
        {
            int panelIndex = rng.NextInt(0, gridCount); // 0~8, 중복 허용
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
            NetFailFlashSerial = 0;
        }

        ApplySolvedPresentation();

        Log($"정답 시드 적용 완료 | seed = {seed}");
        LogSequenceDebug();
    }

    /// <summary>
    /// 패널 입력 시 즉시 판정
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

        // 오답이면 즉시 실패
        if (interactableId != expectedPanelIndex)
        {
            Log($"오답 입력 | step = {NetCurrentStep} | input = {interactableId} | expected = {expectedPanelIndex}");
            MarkFailed();
            StartCoroutine(CoHandleFail());
            return;
        }

        // 이번 입력이 마지막 정답인지 먼저 계산
        bool willSolve = (NetCurrentStep + 1) >= patternLength;

        NetCurrentStep++;
        Log($"정답 입력 | currentStep = {NetCurrentStep}/{patternLength}");

        // 마지막 입력이면 노랑 연출 없이 바로 성공
        if (willSolve)
        {
            MarkSolved();
            NetSolvedAll = true;
            ApplySolvedPresentation();
            Log("점등 패턴 퍼즐 성공");
            return;
        }

        // 마지막 입력이 아니면 노랑 연출 이벤트 발생
        TriggerInputFlash(interactableId);
    }

    /// <summary>
    /// 실패 시 전체 패널을 여러 번 빨간색으로 깜빡이고 초기화
    /// </summary>
    private IEnumerator CoHandleFail()
    {
        _isFailRoutineRunning = true;

        for (int i = 0; i < failFlashCount; i++)
        {
            TriggerFailFlash();
            yield return new WaitForSeconds(failFlashOnTime);
        }

        NetCurrentStep = 0;
        _isFailRoutineRunning = false;

        // 실패 끝난 뒤 성공 상태가 아니라면 모두 끔
        if (!NetSolvedAll)
            TurnOffAllPanelsImmediate();

        Log("점등 패턴 퍼즐 초기화");
    }

    /// <summary>
    /// 노랑 입력 연출 이벤트를 발생시킨다.
    /// </summary>
    private void TriggerInputFlash(int panelIndex)
    {
        NetInputFlashPanelIndex = panelIndex;
        NetInputFlashSerial++;

        // 호스트 즉시 반영
        PlayInputFlashOnPanel(panelIndex);
    }

    /// <summary>
    /// 빨강 실패 연출 이벤트를 발생시킨다.
    /// </summary>
    private void TriggerFailFlash()
    {
        NetFailFlashSerial++;

        // 호스트 즉시 반영
        PlayFailFlashOnAllPanels();
    }

    /// <summary>
    /// InputFlash 이벤트가 들어오면 모든 클라이언트에서 해당 패널 노랑 연출
    /// </summary>
    private void OnInputFlashTriggered()
    {
        if (NetSolvedAll)
            return;

        PlayInputFlashOnPanel(NetInputFlashPanelIndex);
    }

    /// <summary>
    /// FailFlash 이벤트가 들어오면 모든 클라이언트에서 전체 패널 빨강 연출
    /// </summary>
    private void OnFailFlashTriggered()
    {
        if (NetSolvedAll)
            return;

        PlayFailFlashOnAllPanels();
    }

    /// <summary>
    /// 성공 상태 변경 시 전체 초록/끄기 반영
    /// </summary>
    private void OnSolvedAllChanged()
    {
        ApplySolvedPresentation();
    }

    /// <summary>
    /// 현재 성공 상태를 전체 패널에 반영
    /// </summary>
    private void ApplySolvedPresentation()
    {
        if (NetSolvedAll)
            SetSolvedAllPanels();
        else
            TurnOffAllPanelsImmediate();
    }

    /// <summary>
    /// 특정 패널 하나만 노랑 연출 재생
    /// </summary>
    private void PlayInputFlashOnPanel(int panelIndex)
    {
        if (!IsValidPanelIndex(panelIndex))
            return;

        if (panelIndex >= panelViews.Count)
            return;

        if (panelViews[panelIndex] == null)
            return;

        panelViews[panelIndex].PlayInputFlash();
    }

    /// <summary>
    /// 전체 패널 빨강 연출 재생
    /// </summary>
    private void PlayFailFlashOnAllPanels()
    {
        for (int i = 0; i < panelViews.Count; i++)
        {
            if (panelViews[i] == null)
                continue;

            panelViews[i].PlayFailFlash();
        }
    }

    /// <summary>
    /// 모든 패널을 즉시 끈다.
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
    /// 모든 패널을 성공 상태(초록색 유지)로 전환한다.
    /// </summary>
    private void SetSolvedAllPanels()
    {
        for (int i = 0; i < panelViews.Count; i++)
        {
            if (panelViews[i] == null)
                continue;

            panelViews[i].SetSolvedOn();
        }
    }

    /// <summary>
    /// 유효한 패널 인덱스인지 검사
    /// </summary>
    private bool IsValidPanelIndex(int panelIndex)
    {
        return panelIndex >= 0 && panelIndex < gridCount;
    }

    /// <summary>
    /// 루트 퍼즐 자체는 직접 상호작용하지 않음
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    /// <summary>
    /// 디버그용 정답 패턴 로그 출력
    /// </summary>
    private void LogSequenceDebug()
    {
        if (!enableDebugLog)
            return;

        string answer = string.Join(", ", _answerSequence);
        Debug.Log($"[LightPatternPuzzle] 정답 패턴 = [{answer}]", this);
    }

    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[LightPatternPuzzle] {message}", this);
    }
}