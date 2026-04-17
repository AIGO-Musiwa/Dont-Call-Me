using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 다이얼 퍼즐 본체
/// - 시드 기반으로 숫자 6개 생성
/// - 첫 단계는 좌측 1~4
/// - 이후 단계에서는 방향 반전, 0을 지나가야 하는 최소값 규칙 적용
/// - 입력 방향/ 횟수를 즉시 판정
/// - 6단계를 모두 맞추면 즉시 성공
/// </summary>
public class DialPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int totalSteps = 6;

    [Header("Dial View.cs")]
    [SerializeField] private DialView dialView;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerStepCounts = new();
    private int _currentStepIndex;
    private int _currentStepProgress;
    private int _currentSignedPosition;
    private RotationDirection _currentExpectedDirection;
    private bool _hasAnswerSeed;

    private void Awake()
    {
        ResetPuzzleStateOnly();
        ResetPuzzleVisualOnly();
    }

    public void ApplyAnswerSeed(int seed)
    {
        _answerStepCounts.Clear();
        _answerStepCounts.AddRange(DialAnswerGenerator.GenerateStepCounts(seed, totalSteps));

        _hasAnswerSeed = true;

        ResetPuzzleStateOnly();
        ResetPuzzleVisualOnly();

        Log($"다이얼 정답 시드 적용 완료 | seed = {seed}");
        LogStepsDebug();
    }

    /// <summary>
    /// 좌/우 버튼 입력 시 즉시 판정
    /// </summary>
    /// <param name="inputDirection"></param>
    public void OnRotateInput(RotationDirection inputDirection)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (!_hasAnswerSeed)
        {
            Log("아직 정답 시드가 적용되지 않아 입력 무시");
            return;
        }

        if (_currentStepIndex < 0 || _currentStepIndex >= _answerStepCounts.Count)
            return;

        // 1. 기대 방향과 다르면 즉시 실패
        if(inputDirection != _currentExpectedDirection)
        {
            Log($"오답 방향 입력 | step={_currentStepIndex} | input={inputDirection} | expected={_currentExpectedDirection}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        // 2. 방향이 맞으면 시각 회전 먼저 반영
        if (dialView != null)
            dialView.ApplyStepRotation(inputDirection);

        // 3. 현재 단계 진행 횟수 증가
        _currentStepProgress++;

        int expectedCount = _answerStepCounts[_currentStepIndex];

        // 4. 목표 횟수 초과면 즉시 실패
        if(_currentStepProgress > expectedCount)
        {
            Log($"오답 횟수 초과 | step={_currentStepIndex} | progress={_currentStepProgress} | expected={expectedCount}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        Log($"정답 진행 중 | step={_currentStepIndex} | progress={_currentStepProgress}/{expectedCount}");

        // 5. 이번 단계 목표 횟수를 정확히 채우면 다음 단계로 이동
        if (_currentStepProgress == expectedCount)
            AdvanceStep();
    }


    /// <summary>
    /// 현재 단계를 마무리하고 signed position을 갱신한 뒤 다음 단계로 넘어가기
    /// </summary>
    private void AdvanceStep()
    {
        int completedCount = _answerStepCounts[_currentStepIndex];

        // 방금 완료한 단계 방향에 따라 signed position 반영
        if (_currentExpectedDirection == RotationDirection.Left)
            _currentSignedPosition -= completedCount;
        else
            _currentSignedPosition += completedCount;

        _currentStepIndex++;
        _currentStepProgress = 0;

        // 마지막 단계까지 끝났으면 즉시 성공
        if(_currentStepIndex >= _answerStepCounts.Count)
        {
            MarkSolved();
            Log("다이얼 퍼즐 성공");
            return;
        }

        // 다음 단계 방향은 항상 반전
        _currentExpectedDirection = GetDirectionByStepIndex(_currentStepIndex);

        Log($"다음 단계 이동 | nextStep={_currentStepIndex} | expectedDir={_currentExpectedDirection} | signedPos={_currentSignedPosition}");
    }

    /// <summary>
    /// 실패 시 퍼즐 상태와 시각을 모두 초기화
    /// </summary>
    private void ResetPuzzle()
    {
        ResetPuzzleStateOnly();
        ResetPuzzleVisualOnly();
        Log("다이얼 퍼즐 초기화");
    }

    /// <summary>
    /// 내부 상태만 초기화
    /// </summary>
    private void ResetPuzzleStateOnly()
    {
        _currentStepIndex = 0;
        _currentStepProgress = 0;
        _currentSignedPosition = 0;
        _currentExpectedDirection = RotationDirection.Left;
    }

    /// <summary>
    /// 다이얼 비주얼만 초기화
    /// </summary>
    private void ResetPuzzleVisualOnly()
    {
        if (dialView != null)
            dialView.ResetToStartImmediate();
    }

    /// <summary>
    /// 단계 인덱스로 기대 방향 계산
    /// 짝수 단계 = Left / 홀수 단계 = Right
    /// </summary>
    /// <param name="stepIndex"></param>
    /// <returns></returns>
    private RotationDirection GetDirectionByStepIndex(int stepIndex)
    {
        return stepIndex % 2 == 0 ? RotationDirection.Left : RotationDirection.Right;
    }


    protected override void ServerInteract(PlayerController actor)
    {
        //
    }

    private void LogStepsDebug()
    {
        if (!enableDebugLog)
            return;

        string stepString = string.Join(", ", _answerStepCounts);
        Debug.Log($"[DialPuzzle] 정답 숫자열 = [{stepString}]", this);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[DialPuzzle] {message}", this);
    }
}
