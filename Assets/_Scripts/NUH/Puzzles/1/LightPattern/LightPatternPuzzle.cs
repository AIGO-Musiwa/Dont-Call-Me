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
/// - 진행 상태와 시각 상태를 Networked 값으로 공유한다
/// </summary>
public class LightPatternPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int gridCount = 9;     // 3x3 전체 칸 수
    [SerializeField] private int patternLength = 9; // 정답 길이

    [Header("패널 뷰")]
    [SerializeField] private List<LightPatternPanelView> panelViews = new(); // 패널 뷰 9개

    [Header("연출 시간")]
    [SerializeField] private float inputFlashOnTime = 0.2f; // 정답 입력 노랑 유지 시간
    [SerializeField] private float failFlashOnTime = 0.2f;  // 실패 빨강 유지 시간
    [SerializeField] private int failFlashCount = 3;        // 전체 실패 깜빡임 횟수

    [Header("입력 잠금")]
    [SerializeField] private bool lockInputDuringFailEffect = true; // 실패 연출 중 입력 잠금 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerSequence = new(); // 정답 패턴
    private bool _hasAnswerSeed;                        // 시드 적용 여부
    private bool _isFailRoutineRunning;                 // 서버 실패 연출 진행 중 여부

    [Networked] private int NetCurrentStep { get; set; } // 현재 몇 번째까지 맞췄는지
    [Networked, OnChangedRender(nameof(OnVisualPackedChanged))]
    private ulong NetPanelVisualPacked { get; set; } // 9칸 시각 상태(칸당 2비트) 패킹값

    public override void Spawned()
    {
        ApplyPackedVisualToViews();
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
            int panelIndex = rng.NextInt(0, gridCount);
            _answerSequence.Add(panelIndex);
        }

        _hasAnswerSeed = true;

        if (HasStateAuthority)
        {
            NetCurrentStep = 0;
            NetPanelVisualPacked = 0;
        }

        ApplyPackedVisualToViews();

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

        // 틀리면 즉시 실패
        if (interactableId != expectedPanelIndex)
        {
            Log($"오답 입력 | step = {NetCurrentStep} | input = {interactableId} | expected = {expectedPanelIndex}");
            MarkFailed();
            StartCoroutine(CoHandleFail());
            return;
        }

        // 이번 입력이 마지막 정답 입력인지 먼저 계산
        bool willSolve = (NetCurrentStep + 1) >= patternLength;

        NetCurrentStep++;
        Log($"정답 입력 | currentStep = {NetCurrentStep}/{patternLength}");

        // 마지막 입력이면 노랑 연출 없이 바로 성공 처리
        if (willSolve)
        {
            MarkSolved();
            SetAllPanelsVisualState(LightPatternPanelVisualState.SolvedGreen);
            ApplyPackedVisualToViews();
            Log("점등 패턴 퍼즐 성공");
            return;
        }

        // 마지막 입력이 아니면 노랑 연출 진행
        StartCoroutine(CoHandleCorrectInput(interactableId));
    }

    /// <summary>
    /// 정답 입력 시 해당 패널을 잠깐 노랑으로 켠다.
    /// </summary>
    private IEnumerator CoHandleCorrectInput(int panelIndex)
    {
        SetPanelVisualState(panelIndex, LightPatternPanelVisualState.InputYellow);
        ApplyPackedVisualToViews();

        yield return new WaitForSeconds(inputFlashOnTime);

        if (IsSolved)
            yield break;

        SetPanelVisualState(panelIndex, LightPatternPanelVisualState.Off);
        ApplyPackedVisualToViews();
    }

    /// <summary>
    /// 실패 시 전체 패널을 빨간색으로 여러 번 깜빡이고 초기화
    /// </summary>
    private IEnumerator CoHandleFail()
    {
        _isFailRoutineRunning = true;

        for (int i = 0; i < failFlashCount; i++)
        {
            SetAllPanelsVisualState(LightPatternPanelVisualState.FailRed);
            ApplyPackedVisualToViews();

            // TODO: 동일 실패음 재생 지점
            yield return new WaitForSeconds(failFlashOnTime);

            SetAllPanelsVisualState(LightPatternPanelVisualState.Off);
            ApplyPackedVisualToViews();

            yield return new WaitForSeconds(failFlashOnTime);
        }

        NetCurrentStep = 0;
        _isFailRoutineRunning = false;

        Log("점등 패턴 퍼즐 초기화");
    }

    /// <summary>
    /// 패킹값 변경 시 모든 클라이언트에서 뷰 갱신
    /// </summary>
    private void OnVisualPackedChanged()
    {
        ApplyPackedVisualToViews();
    }

    /// <summary>
    /// 패킹된 상태를 각 패널 뷰에 반영
    /// </summary>
    private void ApplyPackedVisualToViews()
    {
        int count = Mathf.Min(panelViews.Count, gridCount);

        for (int i = 0; i < count; i++)
        {
            if (panelViews[i] == null)
                continue;

            panelViews[i].ApplyVisualState(GetPanelVisualState(i));
        }
    }

    /// <summary>
    /// 특정 패널의 시각 상태 기록
    /// 칸당 2비트 사용
    /// </summary>
    private void SetPanelVisualState(int panelIndex, LightPatternPanelVisualState state)
    {
        int shift = panelIndex * 2;
        ulong clearMask = ~((ulong)0b11 << shift);

        NetPanelVisualPacked &= clearMask;
        NetPanelVisualPacked |= ((ulong)state << shift);
    }

    /// <summary>
    /// 특정 패널의 현재 시각 상태 읽기
    /// </summary>
    private LightPatternPanelVisualState GetPanelVisualState(int panelIndex)
    {
        int shift = panelIndex * 2;
        ulong value = (NetPanelVisualPacked >> shift) & 0b11;
        return (LightPatternPanelVisualState)value;
    }

    /// <summary>
    /// 전체 패널 상태 일괄 설정
    /// </summary>
    private void SetAllPanelsVisualState(LightPatternPanelVisualState state)
    {
        for (int i = 0; i < gridCount; i++)
            SetPanelVisualState(i, state);
    }

    private bool IsValidPanelIndex(int index)
    {
        return index >= 0 && index < gridCount;
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    private void LogSequenceDebug()
    {
        if (!enableDebugLog)
            return;

        string answer = string.Join(", ", _answerSequence);
        Debug.Log($"[LightPatternPuzzle] 정답 패턴 = [{answer}]", this);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[LightPatternPuzzle] {message}", this);
    }
}