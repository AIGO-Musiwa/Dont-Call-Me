using Fusion;

public class PlayerVoiceController : NetworkBehaviour
{
    private PlayerController playerController;

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();

        // 네트워크 권한과 데이터가 확실히 세팅된 시점
        if (playerController == null || !HasInputAuthority) return;

        VoiceManager.Instance?.SwitchToGameMode(playerController.NetZone);
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            VoiceManager.Instance?.SwitchToLobbyMode();
        }
    }


}
