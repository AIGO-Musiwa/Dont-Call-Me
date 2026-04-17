using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 다이얼 퍼즐 본체
/// - 시드 기반으로 숫자 6개 생성
/// - 첫 단계는 좌측 1~4
/// - 이후 단계에서는 방향 반전, 0을 지나가야 하는 최소값 규칙 적용
/// - 입력 방향/횟수를 즉시 판정
/// - 진행 상태와 현재 다이얼 위치를 Networked로 공유한다
/// - 6단계를 모두 맞추면 즉시 성공
/// - 다른 플레이어도 현재 위치를 보고 이어서 풀 수 있다
/// </summary>
public class DialPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int totalSteps = 6;

    [Header("Dial View.cs")]
    [SerializeField] private DialView dialView;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerStepCounts = new(); // 정답 숫자열
    private bool _hasAnswerSeed;                          // 시드 적용 완료 여부

    [Networked] private int NetCurrentStepIndex { get; set; }      // 현재 몇 번째 단계인지
    [Networked] private int NetCurrentStepProgress { get; set; }   // 현재 단계에서 몇 번 눌렀는지
    [Networked, OnChangedRender(nameof(OnSignedPositionChanged))]
    private int NetSignedPosition { get; set; }                    // 현재 다이얼 위치값
    [Networked] private RotationDirection NetExpectedDirection { get; set; } // 현재 기대 방향

    private void Awake()
    {
        // Awake에서는 Networked 값 접근 금지
        if (dialView != null)
            dialView.ResetToStartImmediate();
    }

    public override void Spawned()
    {
        ApplySignedPositionToView();
    }

    /// <summary>
    /// 시드 기반 숫자열 생성
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerStepCounts.Clear();
        _answerStepCounts.AddRange(DialAnswerGenerator.GenerateStepCounts(seed, totalSteps));

        _hasAnswerSeed = true;

        if (HasStateAuthority)
            ResetPuzzleStateOnly();

        ApplySignedPositionToView();

        Log($"다이얼 정답 시드 적용 완료 | seed = {seed}");
        LogStepsDebug();
    }

    /// <summary>
    /// 좌/우 버튼 입력 시 즉시 판정
    /// </summary>
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

        if (NetCurrentStepIndex < 0 || NetCurrentStepIndex >= _answerStepCounts.Count)
            return;

        // 기대 방향과 다르면 즉시 실패
        if (inputDirection != NetExpectedDirection)
        {
            Log($"오답 방향 입력 | step={NetCurrentStepIndex} | input={inputDirection} | expected={NetExpectedDirection}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        // 입력 1회마다 현재 위치 즉시 반영
        if (inputDirection == RotationDirection.Left)
            NetSignedPosition -= 1;
        else
            NetSignedPosition += 1;

        NetCurrentStepProgress++;

        // 호스트 즉시 반영
        ApplySignedPositionToView();

        int expectedCount = _answerStepCounts[NetCurrentStepIndex];

        // 목표 횟수 초과면 즉시 실패
        if (NetCurrentStepProgress > expectedCount)
        {
            Log($"오답 횟수 초과 | step={NetCurrentStepIndex} | progress={NetCurrentStepProgress} | expected={expectedCount}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        Log($"정답 진행 중 | step={NetCurrentStepIndex} | progress={NetCurrentStepProgress}/{expectedCount}");

        // 이번 단계 목표 횟수를 정확히 채우면 다음 단계로 이동
        if (NetCurrentStepProgress == expectedCount)
            AdvanceStep();
    }

    /// <summary>
    /// 현재 단계를 마무리하고 다음 단계로 넘어간다.
    /// 현재 위치는 이미 입력할 때마다 NetSignedPosition에 반영되어 있다.
    /// </summary>
    private void AdvanceStep()
    {
        NetCurrentStepIndex++;
        NetCurrentStepProgress = 0;

        if (NetCurrentStepIndex >= _answerStepCounts.Count)
        {
            MarkSolved();
            Log("다이얼 퍼즐 성공");
            return;
        }

        NetExpectedDirection = GetDirectionByStepIndex(NetCurrentStepIndex);

        Log($"다음 단계 이동 | nextStep={NetCurrentStepIndex} | expectedDir={NetExpectedDirection} | signedPos={NetSignedPosition}");
    }

    /// <summary>
    /// 실패 시 상태와 시각을 초기화
    /// </summary>
    private void ResetPuzzle()
    {
        ResetPuzzleStateOnly();
        ApplySignedPositionToView();
        Log("다이얼 퍼즐 초기화");
    }

    /// <summary>
    /// 진행 상태 초기화
    /// </summary>
    private void ResetPuzzleStateOnly()
    {
        NetCurrentStepIndex = 0;
        NetCurrentStepProgress = 0;
        NetSignedPosition = 0;
        NetExpectedDirection = RotationDirection.Left;
    }

    /// <summary>
    /// NetSignedPosition 변경 시 모든 클라이언트에서 다이얼 각도 갱신
    /// </summary>
    private void OnSignedPositionChanged()
    {
        ApplySignedPositionToView();
    }

    /// <summary>
    /// 현재 네트워크 위치값을 다이얼 뷰에 반영
    /// </summary>
    private void ApplySignedPositionToView()
    {
        if (dialView != null)
            dialView.SetSignedPositionImmediate(NetSignedPosition);
    }

    /// <summary>
    /// 단계 인덱스로 기대 방향 계산
    /// 짝수 단계 = Left / 홀수 단계 = Right
    /// </summary>
    private RotationDirection GetDirectionByStepIndex(int stepIndex)
    {
        return stepIndex % 2 == 0 ? RotationDirection.Left : RotationDirection.Right;
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
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