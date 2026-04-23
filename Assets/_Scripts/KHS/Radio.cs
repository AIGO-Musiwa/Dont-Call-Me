using UnityEngine;
using Fusion;

public class Radio : NetworkBehaviour, IHoldInteractable
{
    [Header("참조")]
    [SerializeField] private RadioGlassController glassController;
    [SerializeField] private SimpleAudioTrigger audioTrigger;

    [Header("설정")]
    [SerializeField] private float maxRepairTime = 10f;
    [SerializeField] private float lureDuration = 20f;
    [SerializeField] private float safetyLockTime = 1.5f; // 🛠️ 수리 완료 후 대기 시간

    [Networked] public RadioState CurrentState { get; set; }
    [Networked] public float RepairProgress { get; set; }

    [Networked] public TickTimer RepairTimeout { get; set; }
    // 🛠️ [신규 부품] 수리 직후 작동을 방지하는 안전 잠금 타이머
    [Networked] public TickTimer ActivationLockTimer { get; set; }

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasStateAuthority)
        {
            CurrentState = RadioState.Broken;
            RepairProgress = 0f;
            RepairTimeout = TickTimer.None;
            ActivationLockTimer = TickTimer.None;
        }

        RefreshVisuals();
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && CurrentState == RadioState.InProgress)
        {
            if (RepairTimeout.ExpiredOrNotRunning(Runner))
            {
                CurrentState = RadioState.Broken;
                RepairProgress = 0f;
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
                    RefreshVisuals();
                    break;
            }
        }
    }

    private void RefreshVisuals()
    {
        if (glassController != null) glassController.SetRadioState(CurrentState);

        if (audioTrigger != null)
        {
            if (CurrentState == RadioState.Active) audioTrigger.Play();
            else audioTrigger.Stop();
        }
    }

    // ─── [IInteractable 규약] ───

    public bool CanInteract(PlayerController actor)
        => CurrentState != RadioState.Active && CurrentState != RadioState.Disabled;

    public string GetPromptText(PlayerController actor)
    {
        if (CurrentState == RadioState.Broken || CurrentState == RadioState.InProgress)
            return $"수리하기 (좌클릭 유지... {Mathf.RoundToInt((RepairProgress / maxRepairTime) * 100)}%)";

        if (CurrentState == RadioState.Ready)
        {
            // 🛠️ 안전 잠금 시간 동안은 프롬프트에 상태 표시
            if (!ActivationLockTimer.ExpiredOrNotRunning(Runner))
                return "시스템 안정화 중...";

            return "작동시키기 (좌클릭)";
        }

        return string.Empty;
    }

    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority) return;

        if (CurrentState == RadioState.Ready)
        {
            // 🛠️ 안전 잠금이 걸려있으면 작동 스위치 무시
            if (!ActivationLockTimer.ExpiredOrNotRunning(Runner)) return;

            CurrentState = RadioState.Active;
            Debug.Log("<color=green>[라디오]</color> 유인 모드 가동!");
            Invoke(nameof(SetDisabled), lureDuration);
        }
    }

    public void OnHoldInteract(PlayerController actor, float deltaTime)
    {
        if (!HasStateAuthority) return;

        if (CurrentState == RadioState.Broken || CurrentState == RadioState.InProgress)
        {
            CurrentState = RadioState.InProgress;
            RepairProgress += deltaTime;
            RepairTimeout = TickTimer.CreateFromSeconds(Runner, 0.2f);

            if (RepairProgress >= maxRepairTime)
            {
                RepairProgress = maxRepairTime;
                CurrentState = RadioState.Ready;
                RepairTimeout = TickTimer.None;

                // 🛠️ 수리 완료 즉시 안전 잠금 타이머 작동!
                ActivationLockTimer = TickTimer.CreateFromSeconds(Runner, safetyLockTime);
                Debug.Log("<color=cyan>[라디오]</color> 수리 완료. 안전 잠금 활성화.");
            }
        }
    }

    private void SetDisabled() => CurrentState = RadioState.Disabled;
}