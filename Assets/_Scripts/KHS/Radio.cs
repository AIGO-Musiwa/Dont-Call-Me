using UnityEngine;
using Fusion;

/// <summary>
/// Fusion 2.0 규격을 적용한 멀티플레이어 라디오 코어.
/// 데드맨 스위치(타임아웃)를 적용하여 손을 떼면 수리 진행도가 초기화된다.
/// </summary>
public class Radio : NetworkBehaviour, IHoldInteractable
{
    [Header("참조")]
    [SerializeField] private RadioGlassController glassController;
    [SerializeField] private SimpleAudioTrigger audioTrigger;

    [Header("설정")]
    [SerializeField] private float maxRepairTime = 10f; // 수리 완료에 필요한 총 시간
    [SerializeField] private float lureDuration = 20f;  // 유인 작동 지속 시간

    // 📡 네트워크 동기화 변수 (Fusion 2.0)
    [Networked] public RadioState CurrentState { get; set; }
    [Networked] public float RepairProgress { get; set; }

    // 🛠️ [신규 부품] 수리 중단 감지 타이머 (데드맨 스위치)
    [Networked] public TickTimer RepairTimeout { get; set; }

    // 🔍 상태 변화 감지기
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasStateAuthority)
        {
            CurrentState = RadioState.Broken;
            RepairProgress = 0f;
            RepairTimeout = TickTimer.None;
        }

        RefreshVisuals();
    }

    // ─── [엔진 틱 업데이트 (서버 권한 로직)] ─────────────────────
    public override void FixedUpdateNetwork()
    {
        // 🛠️ 서버에서만 수리 중단 여부를 판별
        if (HasStateAuthority && CurrentState == RadioState.InProgress)
        {
            // 만약 0.2초 이상 클라이언트로부터 Hold 신호가 안 들어왔다면 (손을 뗐다면)
            if (RepairTimeout.ExpiredOrNotRunning(Runner))
            {
                // 수리 실패 -> 빨간색(Broken)으로 돌아가고 게이지 초기화
                CurrentState = RadioState.Broken;
                RepairProgress = 0f;
            }
        }
    }

    // ─── [Fusion 2.0 시각/청각 동기화] ─────────────────────────────
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
        if (glassController != null)
        {
            glassController.SetRadioState(CurrentState);
        }

        if (audioTrigger != null)
        {
            if (CurrentState == RadioState.Active)
                audioTrigger.Play();
            else
                audioTrigger.Stop();
        }
    }

    // ─── [IInteractable 기본 규약] ──────────────────────────────

    public bool CanInteract(PlayerController actor)
        => CurrentState != RadioState.Active && CurrentState != RadioState.Disabled;

    public string GetPromptText(PlayerController actor)
    {
        if (CurrentState == RadioState.Broken || CurrentState == RadioState.InProgress)
            return $"수리하기 (좌클릭 유지... {Mathf.RoundToInt((RepairProgress / maxRepairTime) * 100)}%)";
        if (CurrentState == RadioState.Ready)
            return "작동시키기 (좌클릭)";

        return string.Empty;
    }

    // ─── [단발성 상호작용 (Click) - 작동 전용] ───────────────────
    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority) return;

        if (CurrentState == RadioState.Ready)
        {
            CurrentState = RadioState.Active;
            Debug.Log("<color=green>[라디오]</color> 유인 신호 발생 시작!");
            Invoke(nameof(SetDisabled), lureDuration);
        }
    }

    // ─── [지속성 상호작용 (Hold) - 수리 전용] ───────────────────
    public void OnHoldInteract(PlayerController actor, float deltaTime)
    {
        if (!HasStateAuthority) return;

        if (CurrentState == RadioState.Broken || CurrentState == RadioState.InProgress)
        {
            // 수리 중(파란색)으로 전환 및 진행도 상승
            CurrentState = RadioState.InProgress;
            RepairProgress += deltaTime;

            // 🛠️ [핵심] 신호가 들어올 때마다 타임아웃을 0.2초로 계속 연장함
            RepairTimeout = TickTimer.CreateFromSeconds(Runner, 0.2f);

            if (RepairProgress >= maxRepairTime)
            {
                RepairProgress = maxRepairTime;
                CurrentState = RadioState.Ready;
                RepairTimeout = TickTimer.None; // 수리 완료 시 타이머 끔
                Debug.Log("<color=cyan>[라디오]</color> 수리 완료! 대기 상태 진입.");
            }
        }
    }

    private void SetDisabled()
    {
        CurrentState = RadioState.Disabled;
        Debug.Log("<color=red>[라디오]</color> 전력 방전. 재사용 불가.");
    }
}