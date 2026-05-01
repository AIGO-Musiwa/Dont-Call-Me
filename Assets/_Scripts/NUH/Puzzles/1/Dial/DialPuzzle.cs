using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1단계 다이얼 금고 퍼즐 본체.
/// - 시드 기반으로 첫 방향과 숫자 6개 생성
/// - 첫 방향은 좌/우 랜덤
/// - 이후 방향은 반대로 번갈아 진행
/// - 36도 기준 10칸 다이얼 규칙 사용
/// - 다이얼 완료 시 금고 문을 부드럽게 연다
/// - 금고 안 버튼을 눌러야 최종 퍼즐 클리어 처리한다
/// </summary>
public class DialPuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int totalSteps = 6;                         // 다이얼 입력 단계 수

    [Header("View")]
    [SerializeField] private DialView dialView;                          // 다이얼 회전 뷰
    [SerializeField] private SafeDoorView safeDoorView;                  // 금고 문 뷰
    [SerializeField] private DialInsideButtonView insideButtonView;      // 내부 버튼 뷰

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                 // 디버그 로그 여부

    private readonly List<int> _answerStepCounts = new();                // 정답 숫자열
    private bool _hasAnswerSeed;                                         // 시드 적용 완료 여부

    [Networked] private int NetCurrentStepIndex { get; set; }            // 현재 몇 번째 단계인지
    [Networked] private int NetCurrentStepProgress { get; set; }         // 현재 단계에서 몇 번 눌렀는지

    [Networked, OnChangedRender(nameof(OnSignedPositionChanged))]
    private int NetSignedPosition { get; set; }                          // 현재 다이얼 위치값

    [Networked] private RotationDirection NetFirstDirection { get; set; } // 첫 입력 방향
    [Networked] private RotationDirection NetExpectedDirection { get; set; } // 현재 기대 방향

    [Networked, OnChangedRender(nameof(OnDialUnlockedChanged))]
    private NetworkBool NetDialUnlocked { get; set; }                    // 다이얼 해제 완료 여부

    [Networked, OnChangedRender(nameof(OnSafeDoorOpenedChanged))]
    private NetworkBool NetSafeDoorOpened { get; set; }                  // 금고 문 열림 여부

    [Networked, OnChangedRender(nameof(OnInsideButtonPressedChanged))]
    private NetworkBool NetInsideButtonPressed { get; set; }             // 내부 버튼 눌림 여부

    /// <summary>
    /// 다이얼이 해제되었는지 외부 버튼에서 확인할 때 사용한다.
    /// </summary>
    public bool IsDialUnlocked => NetDialUnlocked;

    /// <summary>
    /// 내부 버튼이 눌렸는지 외부 버튼에서 확인할 때 사용한다.
    /// </summary>
    public bool IsInsideButtonPressed => NetInsideButtonPressed;

    

    private void Awake()
    {
        // Awake에서는 Networked 값 접근 금지.
        if (dialView != null)
            dialView.ResetToStartImmediate();

        if (safeDoorView != null)
            safeDoorView.SetOpenedImmediate(false);
    }

    public override void Spawned()
    {
        base.Spawned();

        ApplySignedPositionToView();
        ApplyDoorOpenedToViewImmediate();
        ApplyInsideButtonToView();
    }

    /// <summary>
    /// 시드 기반으로 첫 방향과 숫자열을 생성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        DialAnswerData data = DialAnswerGenerator.GenerateAnswerData(seed, totalSteps);

        _answerStepCounts.Clear();
        _answerStepCounts.AddRange(data.StepCounts);

        _hasAnswerSeed = true;

        if (HasStateAuthority)
        {
            NetFirstDirection = data.FirstDirection;
            ResetPuzzleStateOnly();
        }

        ApplySignedPositionToView();
        ApplyDoorOpenedToViewImmediate();
        ApplyInsideButtonToView();

        Log($"다이얼 정답 시드 적용 완료 | seed = {seed} | firstDir = {data.FirstDirection}");
        LogStepsDebug();
    }

    /// <summary>
    /// 좌/우 버튼 입력 시 즉시 판정한다.
    /// </summary>
    public void OnRotateInput(RotationDirection inputDirection)
    {
        if (!HasStateAuthority)
            return;

        if (IsSolved)
            return;

        if (NetDialUnlocked)
            return;

        if (!_hasAnswerSeed)
        {
            Log("아직 정답 시드가 적용되지 않아 입력 무시");
            return;
        }

        if (NetCurrentStepIndex < 0 || NetCurrentStepIndex >= _answerStepCounts.Count)
            return;

        if (inputDirection != NetExpectedDirection)
        {
            Log($"오답 방향 입력 | step={NetCurrentStepIndex} | input={inputDirection} | expected={NetExpectedDirection}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        if (inputDirection == RotationDirection.Left)
            NetSignedPosition -= 1;
        else
            NetSignedPosition += 1;

        NetCurrentStepProgress++;

        ApplySignedPositionToView();

        //TODO_Sound - 다이얼 회전 사운드
        if (audioModule != null) audioModule.PlaySound(SoundType.InteractLight); // '금고 다이얼 회전

        int expectedCount = _answerStepCounts[NetCurrentStepIndex];

        if (NetCurrentStepProgress > expectedCount)
        {
            Log($"오답 횟수 초과 | step={NetCurrentStepIndex} | progress={NetCurrentStepProgress} | expected={expectedCount}");
            MarkFailed();
            ResetPuzzle();
            return;
        }

        Log($"정답 진행 중 | step={NetCurrentStepIndex} | progress={NetCurrentStepProgress}/{expectedCount}");

        if (NetCurrentStepProgress == expectedCount)
            AdvanceStep();
    }

    /// <summary>
    /// 금고 내부 버튼을 눌렀을 때 최종 퍼즐 클리어를 처리한다.
    /// </summary>
    public void ServerPressInsideButton(PlayerController actor)
    {
        if (!HasStateAuthority)
            return;

        if (actor == null)
            return;

        if (IsSolved)
            return;

        if (!NetDialUnlocked)
            return;

        if (NetInsideButtonPressed)
            return;

        NetInsideButtonPressed = true;

        //TODO_Sound - 내부 버튼 입력 사운드
        if (audioModule != null) audioModule.PlaySound(SoundType.InteractLight); //  - 금고 내부 버튼 입력

        ApplyInsideButtonToView();

        MarkSolved();

        Log($"금고 내부 버튼 입력으로 퍼즐 최종 클리어 | actor={actor.name}");
    }

    /// <summary>
    /// 현재 단계를 마무리하고 다음 단계로 넘어간다.
    /// 마지막 단계가 끝나면 문을 열고 내부 버튼 대기 상태로 전환한다.
    /// </summary>
    private void AdvanceStep()
    {
        NetCurrentStepIndex++;
        NetCurrentStepProgress = 0;

        if (NetCurrentStepIndex >= _answerStepCounts.Count)
        {
            UnlockDialAndOpenDoor();
            return;
        }

        NetExpectedDirection = GetDirectionByStepIndex(NetCurrentStepIndex);

        Log($"다음 단계 이동 | nextStep={NetCurrentStepIndex} | expectedDir={NetExpectedDirection} | signedPos={NetSignedPosition}");
    }

    /// <summary>
    /// 다이얼 해제 완료 후 금고 문을 연다.
    /// 이 시점에는 아직 퍼즐 최종 클리어가 아니다.
    /// </summary>
    private void UnlockDialAndOpenDoor()
    {
        NetDialUnlocked = true;
        NetSafeDoorOpened = true;


        //TODO_Sound - 금고 문 열림 사운드
        if (audioModule != null) audioModule.PlaySound(SoundType.MechanicalMove); //  금고 문 열림

        ApplyDoorOpenedToViewAnimated();

        Log("다이얼 해제 완료. 금고 문 열림. 내부 버튼 입력 대기");
    }

    /// <summary>
    /// 실패 시 상태와 시각을 초기화한다.
    /// </summary>
    private void ResetPuzzle()
    {
        ResetPuzzleStateOnly();
        ApplySignedPositionToView();
        ApplyDoorOpenedToViewImmediate();
        ApplyInsideButtonToView();

        Log("다이얼 퍼즐 초기화");
    }

    /// <summary>
    /// 진행 상태를 초기화한다.
    /// </summary>
    private void ResetPuzzleStateOnly()
    {
        NetCurrentStepIndex = 0;
        NetCurrentStepProgress = 0;
        NetSignedPosition = 0;

        NetDialUnlocked = false;
        NetSafeDoorOpened = false;
        NetInsideButtonPressed = false;

        NetExpectedDirection = GetDirectionByStepIndex(0);
    }

    /// <summary>
    /// NetSignedPosition 변경 시 모든 클라이언트에서 다이얼 각도를 갱신한다.
    /// </summary>
    private void OnSignedPositionChanged()
    {
        ApplySignedPositionToView();
    }

    /// <summary>
    /// 다이얼 해제 상태 변경 시 필요한 시각 상태를 갱신한다.
    /// </summary>
    private void OnDialUnlockedChanged()
    {
        // 다이얼 해제 여부 자체로는 별도 시각 처리 없음.
    }

    /// <summary>
    /// 문 열림 상태 변경 시 금고 문 뷰를 애니메이션으로 갱신한다.
    /// </summary>
    private void OnSafeDoorOpenedChanged()
    {
        ApplyDoorOpenedToViewAnimated();
    }

    /// <summary>
    /// 내부 버튼 상태 변경 시 버튼 뷰를 갱신한다.
    /// </summary>
    private void OnInsideButtonPressedChanged()
    {
        ApplyInsideButtonToView();
    }

    /// <summary>
    /// 현재 네트워크 위치값을 다이얼 뷰에 반영한다.
    /// </summary>
    private void ApplySignedPositionToView()
    {
        if (dialView != null)
            dialView.SetSignedPositionImmediate(NetSignedPosition);
    }

    /// <summary>
    /// 현재 문 열림 상태를 금고 문 뷰에 즉시 반영한다.
    /// 스폰/초기화용이다.
    /// </summary>
    private void ApplyDoorOpenedToViewImmediate()
    {
        if (safeDoorView != null)
            safeDoorView.SetOpenedImmediate(NetSafeDoorOpened);
    }

    /// <summary>
    /// 현재 문 열림 상태를 금고 문 뷰에 애니메이션으로 반영한다.
    /// 실제 문 열림/닫힘 이벤트용이다.
    /// </summary>
    private void ApplyDoorOpenedToViewAnimated()
    {
        if (safeDoorView != null)
            safeDoorView.SetOpenedAnimated(NetSafeDoorOpened);
    }

    /// <summary>
    /// 현재 내부 버튼 눌림 상태를 버튼 뷰에 반영한다.
    /// </summary>
    private void ApplyInsideButtonToView()
    {
        if (insideButtonView != null)
            insideButtonView.SetPressedImmediate(NetInsideButtonPressed);
    }

    /// <summary>
    /// 단계 인덱스로 기대 방향을 계산한다.
    /// 첫 방향은 seed로 랜덤 결정되고 이후는 반대로 번갈아 간다.
    /// </summary>
    private RotationDirection GetDirectionByStepIndex(int stepIndex)
    {
        return DialAnswerGenerator.GetDirectionByStepIndex(NetFirstDirection, stepIndex);
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음.
    }

    /// <summary>
    /// 정답 숫자열을 로그로 출력한다.
    /// </summary>
    private void LogStepsDebug()
    {
        if (!enableDebugLog)
            return;

        string stepString = string.Join(", ", _answerStepCounts);
        Debug.Log($"[DialPuzzle] 시작 방향 = {NetFirstDirection} | 정답 숫자열 = [{stepString}]", this);
    }

    /// <summary>
    /// 디버그 로그를 출력한다.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[DialPuzzle] {message}", this);
    }
}