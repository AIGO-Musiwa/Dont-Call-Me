using UnityEngine;

public class PlayerVoiceController : MonoBehaviour
{
    private PlayerController playerController;

    void Start()
    {
        playerController = GetComponent<PlayerController>();

        if (playerController == null || !playerController.HasInputAuthority) return;

        VoiceManager.Instance?.SwitchToGameMode(playerController.NetZone);
    }

    private void OnDestroy()
    {
        if (playerController != null && playerController.HasStateAuthority)
        {
            VoiceManager.Instance?.SwitchToLobbyMode();
        }
    }


}
