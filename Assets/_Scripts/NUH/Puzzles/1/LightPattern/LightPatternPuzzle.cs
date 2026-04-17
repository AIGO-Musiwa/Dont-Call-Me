using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 점등 패턴 복사 퍼즐 본체
/// - 시드 기반으로 길이 9 패턴 생성
/// - 버튼 입력 시 즉시 현재 단계 판정
/// - 오답이면 즉시 실패 후 전체 패널 3회 깜빡임
/// - 9개를 모두 맞추면 즉시 성공
/// </summary>
public class LightPatternPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int gridCount = 9;         // 3x3 전체 칸 수
    [SerializeField] private int patternLength = 9;     // 정답 길이

    [Header("패널 뷰")]
    [SerializeField] private List<LightPatternPanelView> panelViews = new(); // 패널 뷰 9개

    [Header("실패 깜빡임")]
    [SerializeField] private float failFlashOnTime = 0.15f; // 실패 시 각 깜빡임 켜짐 시간
    [SerializeField] private int failFlashCount = 3;        // 전체 깜빡임 횟수

    [Header("입력 잠금")]
    [SerializeField] private bool lockInputDuringFailEffect = true; // 실패 연출 중 입력 잠금 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerSequence = new(); // 정답 패턴
    private int _currentStep;                           // 현재 입력 단계
    private bool _hasAnswerSeed;                        // 시드 적용 여부
    private bool _isFailRoutineRunning;                 // 실패 연출 중 여부

    /// <summary>
    /// 같은 seed로 길이 9 정답 패턴 생성
    /// 패널 인덱스는 0~8, 중복 허용
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerSequence.Clear();
        _currentStep = 0;

        SeedRandom rng = new SeedRandom(seed);

        for (int i = 0; i < patternLength; i++)
        {
            int panelIndex = rng.NextInt(0, gridCount); // 0~8, 중복 허용
            _answerSequence.Add(panelIndex);
        }

        _hasAnswerSeed = true;
        _isFailRoutineRunning = false;

        TurnOffAllPanelsImmediate();

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

        if (interactableId < 0 || interactableId >= gridCount)
            return;

        int expectedPanelIndex = _answerSequence[_currentStep];

        // 입력됐다는 피드백은 먼저 재생
        if (interactableId < panelViews.Count && panelViews[interactableId] != null)
            panelViews[interactableId].PlayInputFlash();

        // 틀리면 즉시 실패 처리
        if (interactableId != expectedPanelIndex)
        {
            Log($"오답 입력 | step = {_currentStep} | input = {interactableId} | expected = {expectedPanelIndex}");
            MarkFailed();
            StartCoroutine(CoHandleFail());
            return;
        }

        _currentStep++;

        Log($"정답 입력 | currentStep = {_currentStep}/{patternLength}");

        if (_currentStep >= patternLength)
        {
            MarkSolved();
            TurnOffAllPanelsImmediate();
            Log("점등 패턴 퍼즐 성공");
        }
    }

    /// <summary>
    /// 실패 시 전체 패널을 여러 번 깜빡이고 초기화
    /// </summary>
    private IEnumerator CoHandleFail()
    {
        _isFailRoutineRunning = true;

        for (int i = 0; i < failFlashCount; i++)
        {
            for (int p = 0; p < panelViews.Count; p++)
            {
                if (panelViews[p] == null)
                    continue;

                panelViews[p].PlayFailFlash();
            }

            // TODO: 동일 실패음 재생 지점
            yield return new WaitForSeconds(failFlashOnTime);
        }

        _currentStep = 0;
        TurnOffAllPanelsImmediate();
        _isFailRoutineRunning = false;

        Log("점등 패턴 퍼즐 초기화");
    }

    /// <summary>
    /// 모든 패널 라이트를 즉시 끈다.
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