using Fusion;
using UnityEngine;

// 플레이어 1인당 스폰되는 대기실 데이터 오브젝트
// 스폰: LobbyStateManager.OnPlayerJoined (Host)
// 닉네임/준비상태/마이크 상태 변경: RPC로 Host에게 요청
public class PlayerLobbyData : NetworkBehaviour
{
    [Networked] public NetworkString<_32> Nickname    { get; set; }
    [Networked] public NetworkBool        IsReady     { get; set; }
    [Networked] public NetworkBool        IsMicActive { get; set; }
    [Networked] public PlayerRef          Owner       { get; set; }

    // ── 스폰 직후 ─────────────────────────────────────────────
    public override void Spawned()
    {
        if (!HasInputAuthority) return;

        // 닉네임 전송
        RPC_SetNickname(NetworkManager.Instance.LocalNickname);

        // VoiceManager에 등록 → 마이크 레벨 모니터링 시작
        VoiceManager.Instance?.SetMyLobbyData(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
            VoiceManager.Instance?.ClearLobbyData();
    }

    // ── RPC (Client → Host) ───────────────────────────────────
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetNickname(NetworkString<_32> nickname) => Nickname = nickname;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool isReady) => IsReady = isReady;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetMicActive(NetworkBool isActive) => IsMicActive = isActive;
}
