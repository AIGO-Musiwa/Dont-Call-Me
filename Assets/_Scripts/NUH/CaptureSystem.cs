using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 포획/구출/사망 상태 머신
///
/// 상태 전이:
///   Normal → Captured  : 크리처 충돌
///   Captured → Restrained : 구제장소 도착
///   Restrained → Normal   : 구출
///   Restrained → Dead     : 사망 게이지 100%
///   Normal/Restrained → Escaped : 탈출 성공
/// </summary>
public class CaptureSystem : NetworkBehaviour
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────
    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public PlayerState CurrentState { get; private set; }

    /// <summary>
    /// 반복 포획 횟수 — DeathGauge 속도 배율에 사용
    /// </summary>
    [Networked] public int CaptureCount { get; private set; }

    /// <summary>
    /// 끌려가는 목적지 (구제장소)
    /// </summary>
    [Networked] public NetworkObject RestrainTarget { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("구제장소")]
    [SerializeField] private Transform restrainPoint;  // 묶이는 위치

    // ─────────────────────────────────────────
    // 로컬 참조
    // ─────────────────────────────────────────
    private PlayerController        _playerController;
    private FirstPersonController   _fpc;
    private HandSystem              _handSystem;
    private DeathGauge              _deathGauge;

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        _playerController = GetComponent<PlayerController>();
        _fpc              = GetComponent<FirstPersonController>();
        _handSystem       = GetComponent<HandSystem>();
        _deathGauge       = GetComponent<DeathGauge>();

        CurrentState = PlayerState.Normal;
    }

    // ─────────────────────────────────────────
    // 포획 진입 (크리처에서 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// 크리처 충돌 시 호출
    /// StateAuthority(Host)에서 실행
    /// </summary>
    public void OnCaptured(NetworkObject captureDestination)
    {
        if (!Runner.IsServer) return;
        if (CurrentState != PlayerState.Normal) return;

        RestrainTarget = captureDestination;
        RPC_SetCaptureState(PlayerState.Captured);
    }

    // ─────────────────────────────────────────
    // 구제장소 도착 (크리처 AI에서 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// 크리처가 플레이어를 구제장소까지 끌고 온 뒤 호출
    /// </summary>
    public void OnArrivedAtRestrainPoint()
    {
        if (!Runner.IsServer) return;
        if (CurrentState != PlayerState.Captured) return;

        // 묶인 위치로 이동
        if (restrainPoint != null)
            transform.position = restrainPoint.position;

        RPC_SetCaptureState(PlayerState.Restrained);
    }

    // ─────────────────────────────────────────
    // 구출 (RestrainRoom에서 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// 다른 플레이어가 구출 클릭 1회 시 호출
    /// </summary>
    public void OnRescued()
    {
        if (!Runner.IsServer) return;
        if (CurrentState != PlayerState.Restrained) return;

        _deathGauge?.StopTick();
        _deathGauge?.ResetGauge();

        RPC_SetCaptureState(PlayerState.Normal);
    }

    // ─────────────────────────────────────────
    // 사망 (DeathGauge에서 호출)
    // ─────────────────────────────────────────
    public void OnDeath()
    {
        if (!Runner.IsServer) return;
        RPC_SetCaptureState(PlayerState.Dead);
    }

    // ─────────────────────────────────────────
    // 탈출 (탈출 트리거에서 호출)
    // ─────────────────────────────────────────
    public void OnEscaped()
    {
        if (!Runner.IsServer) return;
        RPC_SetCaptureState(PlayerState.Escaped);
    }

    // ─────────────────────────────────────────
    // 상태 전환 RPC
    // ─────────────────────────────────────────
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetCaptureState(PlayerState newState)
    {
        CurrentState                    = newState;
        _playerController.CurrentState = newState;
    }

    /// <summary>
    /// [Networked] 상태 변경 감지 → 각 상태 진입 처리
    /// </summary>
    private void OnStateChanged()
    {
        switch (CurrentState)
        {
            case PlayerState.Normal:
                EnterNormal();
                break;
            case PlayerState.Captured:
                EnterCaptured();
                break;
            case PlayerState.Restrained:
                EnterRestrained();
                break;
            case PlayerState.Dead:
                EnterDead();
                break;
            case PlayerState.Escaped:
                EnterEscaped();
                break;
        }
    }

    // ─────────────────────────────────────────
    // 각 상태 진입 처리
    // ─────────────────────────────────────────
    private void EnterNormal()
    {
        // 이동 + 시야 모두 복구
        LockInput(lockMovement: false, lockLook: false);
    }

    private void EnterCaptured()
    {
        // 양손 아이템 드랍
        _handSystem?.DropAllItems();

        // 이동 + 시야 완전 차단
        LockInput(lockMovement: true, lockLook: true);

        // 포획 횟수 증가
        if (Runner.IsServer)
            CaptureCount++;

        // 캐비닛 안에 있었다면 강제 퇴장
        CabinetDoor cabinet = FindOccupiedCabinet();
        cabinet?.ForceExit();

        // 책상 숨기 해제
        DeskHideZone desk = GetComponentInParent<DeskHideZone>();
        desk?.CheckHideValidity(_playerController);
    }

    private void EnterRestrained()
    {
        // 이동만 차단, 시야는 허용
        LockInput(lockMovement: true, lockLook: false);

        // 사망 게이지 시작
        _deathGauge?.StartTick(CaptureCount);
    }

    private void EnterDead()
    {
        // 이동 + 시야 모두 차단
        LockInput(lockMovement: true, lockLook: true);

        // 관전 모드 진입
        GetComponent<SpectatorSystem>()?.EnterSpectatorMode();
    }

    private void EnterEscaped()
    {
        // 이동 + 시야 모두 차단 (탈출 후 조작 불가)
        LockInput(lockMovement: true, lockLook: true);

        // 관전 모드 진입
        GetComponent<SpectatorSystem>()?.EnterSpectatorMode();
    }

    // ─────────────────────────────────────────
    // 입력 잠금
    // ─────────────────────────────────────────
    private void LockInput(bool lockMovement, bool lockLook)
    {
        _fpc?.LockInput(lockMovement, lockLook);
    }

    // ─────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────

    /// <summary>
    /// 이 플레이어가 들어가 있는 캐비닛 탐색
    /// 포획 시 강제 퇴장에 사용
    /// </summary>
    private CabinetDoor FindOccupiedCabinet()
    {
        foreach (CabinetDoor door in FindObjectsByType<CabinetDoor>(FindObjectsSortMode.None))
        {
            if (door.Occupant == Object.InputAuthority)
                return door;
        }
        return null;
    }

    public bool IsAlive()      => CurrentState != PlayerState.Dead;
    public bool IsControllable() => CurrentState == PlayerState.Normal;
}

/// <summary>
/// 플레이어 상태 열거형
/// PlayerController.CurrentState와 공유
/// </summary>
public enum PlayerState
{
    Normal,      // 일반 상태 — 자유 조작
    Captured,    // 끌려가는 중 — 완전 조작 불가
    Restrained,  // 구제장소에 묶임 — 시선만 가능, 사망 게이지 진행
    Dead,        // 사망 — 관전 모드 진입
    Escaped      // 탈출 성공 — 관전 모드 진입
}
