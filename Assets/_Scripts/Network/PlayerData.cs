using Fusion;
using System.Runtime.CompilerServices;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    // ── 네트워크 동기화 프로퍼티 ──────────────────────────
    [Networked] public NetworkString<_32> Nickname { get; set; }        // 플레이어 닉네임
    [Networked] public NetworkBool IsReady { get; set; }                // 준비 상태
    [Networked] public NetworkBool IsMicActive { get; set; }            // 마이크 활성화 상태
    [Networked] public int SlotIndex { get; set; } = -1;                // 로비 내 슬롯 인덱스 (0~3, -1은 미할당)
    [Networked] public NetworkBool HasReturnedToLobby { get; set; }     // 로비로 복귀 했는지 확인
    [Networked] public NetworkBool IsHost {  get; set; }                // 호스트인지 확인
    [Networked] public NetworkBool IsReviewingResult {  get; set; }     // 결과 화면을 보고 있는 중인지 여부
    [Networked] public PlayerState FinalPlayerState { get; set; }       // 마지막 플레이어 상태

    // ── 게임 전용 ────────────────────────────────────────
    [Networked] public NetworkId PlayerControllerNetId { get; set; }

    public PlayerController GetPlayerController()
    {
        if (PlayerControllerNetId == default) return null;
        Runner.TryFindObject(PlayerControllerNetId, out NetworkObject obj);
        return obj?.GetComponent<PlayerController>();
    }

    #region FusionLifecycle

    public override void Spawned()
    {
        if (!HasInputAuthority) return;

        string nickname = GameLauncher.Instance != null
            ? GameLauncher.Instance.LocalNickname
            : "Player";

        Rpc_SetNickname(nickname);

        // 호스트는 준비 버튼 없으므로 스폰 즉시 IsReady = true
        if (Runner.IsServer)
        {
            Rpc_SetReady(true);
            Rpc_SetIsHost(true);
        }
        else
        {
            bool isReturning = GameLauncher.Instance?.IsReturningToLobby ?? false;
            if (isReturning)
                Rpc_SetReady(false);

            Rpc_SetIsHost(false);
        }

        // VoiceManager에 로컬 플레이어 등록
        VoiceManager.Instance?.RegisterLocalPlayer(this);

        Debug.Log($"[PlayerData] 스폰 완료 | 닉네임={nickname} | IsHost={Runner.IsServer}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (!HasInputAuthority) return;

        VoiceManager.Instance?.Unregister();
    }

    #endregion

    #region RPC (본인 → Host)

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetNickname(string nickname) => Nickname = nickname;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetReady(NetworkBool isReady) => IsReady = isReady;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetMicActive(NetworkBool isActive) => IsMicActive = isActive;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetHasReturnedToLobby(NetworkBool value) => HasReturnedToLobby = value;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetIsHost(NetworkBool isHost) => IsHost = isHost;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetReviewingResult(NetworkBool value) => IsReviewingResult = value;

    #endregion
}
