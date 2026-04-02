using Fusion;
using UnityEngine;

public class PlayerLobbyData : NetworkBehaviour
{
    // ── 네트워크 동기화 프로퍼티 ──────────────────────────
    [Networked] public NetworkString<_32> Nickname { get; set; }        // 플레이어 닉네임
    [Networked] public NetworkBool IsReady { get; set; }                // 준비 상태
    [Networked] public NetworkBool IsMicActive { get; set; }            // 마이크 활성화 상태
    [Networked] public int SlotIndex { get; set; } = -1;                // 로비 내 슬롯 인덱스 (0~3, -1은 미할당)

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
        }

        // TODO: VoiceManager 연동 후 마이크 모니터링 등록

        Debug.Log($"[PlayerLobbyData] 스폰 완료 | 닉네임={nickname} | IsHost={Runner.IsServer}");
    }

    #endregion

    #region RPC (본인 → Host)

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetNickname(string nickname) => Nickname = nickname;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_SetReady(NetworkBool isReady) => IsReady = isReady;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void rpc_SetMicActive(NetworkBool isActive) => IsMicActive = isActive;

    #endregion
}
