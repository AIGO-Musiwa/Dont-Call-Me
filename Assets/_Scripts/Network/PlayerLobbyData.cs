using Fusion;
using UnityEngine;

public class PlayerLobbyData : NetworkBehaviour
{
    // ── 네트워크 동기화 프로퍼티 ──────────────────────────
    [Networked] public NetworkString<_32> Nickname { get; set; }        // 플레이어 닉네임
    [Networked] public NetworkBool IsReady { get; set; }                // 준비 상태
    [Networked] public NetworkBool IsMicActive { get; set; }            // 마이크 활성화 상태

    #region FusionLifecycle

    public override void Spawned()
    {
        if (!HasInputAuthority) return;

        
    }


    #endregion
}
