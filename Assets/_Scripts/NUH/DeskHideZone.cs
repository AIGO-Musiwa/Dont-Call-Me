using Fusion;
using UnityEngine;

/// <summary>
/// 책상 아래 숨기 구역
/// 앉기(Crouch) 상태로 트리거 진입 시 숨기 상태 활성화
/// 문 잠금 구조 없음 — 앉기 해제 시 자동으로 숨기 해제
///
/// 씬 설정:
///   책상 오브젝트 하위에 Trigger Collider를 가진 빈 GameObject에 부착
/// </summary>
public class DeskHideZone : NetworkBehaviour
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────
    [Networked] public bool IsHiding  { get; private set; }
    [Networked] public PlayerRef HidingPlayer { get; private set; }

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private PlayerController _playerInZone;  // 현재 트리거 안에 있는 플레이어

    // ─────────────────────────────────────────
    // 트리거 감지
    // ─────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (!player.HasInputAuthority) return;

        // 앉기 상태일 때만 숨기 진입 허용
        if (player.FPController.IsCrouching)
        {
            _playerInZone = player;
            RPC_SetHiding(player.Object.InputAuthority, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;
        if (!player.HasInputAuthority) return;

        if (HidingPlayer == player.Object.InputAuthority)
        {
            _playerInZone = null;
            RPC_SetHiding(PlayerRef.None, false);
        }
    }

    // ─────────────────────────────────────────
    // 앉기 해제 감지 — 매 틱 확인
    // ─────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        if (!IsHiding) return;
        if (_playerInZone == null) return;

        // 앉기 해제 시 강제 퇴출
        if (!_playerInZone.FPController.IsCrouching)
        {
            CheckHideValidity(_playerInZone);
        }
    }

    /// <summary>
    /// 앉기 해제 또는 포획 시 숨기 강제 해제
    /// </summary>
    public void CheckHideValidity(PlayerController player)
    {
        if (!IsHiding) return;
        if (HidingPlayer != player.Object.InputAuthority) return;

        _playerInZone = null;
        RPC_SetHiding(PlayerRef.None, false);
    }

    // ─────────────────────────────────────────
    // RPC
    // ─────────────────────────────────────────
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetHiding(PlayerRef playerRef, bool hiding)
    {
        IsHiding     = hiding;
        HidingPlayer = playerRef;
    }
}
