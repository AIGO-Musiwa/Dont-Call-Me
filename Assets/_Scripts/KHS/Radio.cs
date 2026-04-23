using UnityEngine;
using Fusion;

/// <summary>
/// 고장난 라디오 상호작용 오브젝트.
/// 
/// 동작 규칙:
/// - Broken / InProgress 상태에서는 Hold 상호작용으로 수리한다.
/// - Ready 상태에서는 Click 상호작용으로 작동시킨다.
/// - Active / Disabled 상태에서는 상호작용할 수 없다.
/// </summary>
public class Radio : NetworkBehaviour, IHoldInteractable
{
    [Header("참조")]
    [SerializeField] private RadioGlassController glassController; // 라디오 유리 시각 상태 제어기
    [SerializeField] private SimpleAudioTrigger audioTrigger;      // 작동 상태 음향 제어기

    [Header("설정")]
    [SerializeField] private float maxRepairTime = 10f;     // 수리에 필요한 총 시간
    [SerializeField] private float lureDuration = 20f;      // 작동 후 유인 상태 지속 시간
    [SerializeField] private float safetyLockTime = 1.5f;   // 수리 완료 후 즉시 작동 방지 잠금 시간

    [Networked] public RadioState CurrentState { get; set; }       // 현재 라디오 상태
    [Networked] public float RepairProgress { get; set; }          // 현재 수리 진행도
    [Networked] public TickTimer RepairTimeout { get; set; }       // Hold 중단 시 수리 실패로 되돌리는 타이머
    [Networked] public TickTimer ActivationLockTimer { get; set; } // 수리 완료 직후 작동 방지 잠금 타이머

    private ChangeDetector _changeDetector; // 상태 변경 감지기

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState); // 시뮬레이션 상태 변경 감지기 생성

        if (Object.HasStateAuthority)
        {
            CurrentState = RadioState.Broken;     // 시작 상태는 고장
            RepairProgress = 0f;                  // 수리 진행도 초기화
            RepairTimeout = TickTimer.None;       // 수리 타이머 초기화
            ActivationLockTimer = TickTimer.None; // 안전 잠금 초기화
        }

        RefreshVisuals(); // 시작 시 시각/음향 반영
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return; // 상태 권한 없는 쪽은 처리 안 함

        if (CurrentState == RadioState.InProgress)
        {
            if (RepairTimeout.ExpiredOrNotRunning(Runner))
            {
                CurrentState = RadioState.Broken; // 일정 시간 Hold가 끊기면 다시 고장 상태
                RepairProgress = 0f;              // 진행도 초기화
            }
        }
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(CurrentState):
                    RefreshVisuals(); // 상태가 바뀌면 시각/음향 갱신
                    break;
            }
        }
    }

    /// <summary>
    /// 현재 상태에 맞게 시각/음향을 갱신한다.
    /// </summary>
    private void RefreshVisuals()
    {
        if (glassController != null)
            glassController.SetRadioState(CurrentState); // 유리 상태 표시 갱신

        if (audioTrigger != null)
        {
            if (CurrentState == RadioState.Active)
                audioTrigger.Play(); // 작동 중이면 소리 재생
            else
                audioTrigger.Stop(); // 그 외 상태면 소리 정지
        }
    }

    /// <summary>
    /// 현재 상태에서 상호작용이 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        return CurrentState != RadioState.Active && CurrentState != RadioState.Disabled; // 작동 중/완전 종료 상태가 아니면 상호작용 허용
    }

    /// <summary>
    /// 현재 상태에 맞는 프롬프트를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        if (CurrentState == RadioState.Broken || CurrentState == RadioState.InProgress)
            return $"수리하기 (좌클릭 유지... {Mathf.RoundToInt((RepairProgress / maxRepairTime) * 100)}%)"; // Hold 수리 안내

        if (CurrentState == RadioState.Ready)
        {
            if (!ActivationLockTimer.ExpiredOrNotRunning(Runner))
                return "시스템 안정화 중..."; // 잠금 중이면 작동 불가 상태 안내

            return "작동시키기 (좌클릭)"; // Ready 상태면 클릭 작동 안내
        }

        return string.Empty; // 그 외 상태는 프롬프트 없음
    }

    /// <summary>
    /// Click 상호작용 처리.
    /// Ready 상태일 때만 실제로 작동한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority)
            return; // 상태 권한 없는 쪽은 처리 안 함

        if (CurrentState != RadioState.Ready)
            return; // Ready 상태가 아니면 클릭 작동 무시

        if (!ActivationLockTimer.ExpiredOrNotRunning(Runner))
            return; // 안전 잠금 시간 중에는 작동 무시

        CurrentState = RadioState.Active; // 라디오 작동 상태로 변경
        Debug.Log("<color=green>[라디오]</color> 유인 모드 가동!");
        Invoke(nameof(SetDisabled), lureDuration); // 일정 시간 뒤 종료 상태로 전환
    }

    /// <summary>
    /// Hold 상호작용 처리.
    /// Broken / InProgress 상태에서만 수리 진행도를 누적한다.
    /// </summary>
    public void OnHoldInteract(PlayerController actor, float deltaTime)
    {
        if (!HasStateAuthority)
            return; // 상태 권한 없는 쪽은 처리 안 함

        if (CurrentState != RadioState.Broken && CurrentState != RadioState.InProgress)
            return; // 수리 가능한 상태가 아니면 Hold 무시

        CurrentState = RadioState.InProgress; // 수리 진행 상태로 전환
        RepairProgress += deltaTime; // 이번 틱 수리 진행도 누적
        RepairTimeout = TickTimer.CreateFromSeconds(Runner, 0.2f); // 잠깐이라도 Hold가 끊기면 되돌아가도록 갱신

        if (RepairProgress >= maxRepairTime)
        {
            RepairProgress = maxRepairTime; // 최대 진행도 고정
            CurrentState = RadioState.Ready; // 수리 완료 상태로 전환
            RepairTimeout = TickTimer.None;  // 타임아웃 종료
            ActivationLockTimer = TickTimer.CreateFromSeconds(Runner, safetyLockTime); // 안전 잠금 시작

            Debug.Log("<color=cyan>[라디오]</color> 수리 완료. 안전 잠금 활성화.");
        }
    }

    /// <summary>
    /// 유인 모드가 끝난 뒤 라디오를 완전 종료 상태로 전환한다.
    /// </summary>
    private void SetDisabled()
    {
        CurrentState = RadioState.Disabled; // 최종 종료 상태 전환
    }
}