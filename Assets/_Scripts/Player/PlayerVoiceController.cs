using Fusion;
using Photon.Voice.Unity;
using UnityEditor.Search;
using UnityEngine;

public class PlayerVoiceController : NetworkBehaviour
{
    private PlayerController playerController;

    [Header("근접 음성 감쇠 설정")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 25f;

    private bool _audioSourceInitialized;

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();

        // 네트워크 권한과 데이터가 확실히 세팅된 시점
        if (playerController == null || !HasInputAuthority) return;

        VoiceManager.Instance?.SwitchToGameMode(playerController.NetZone);
    }

    private void Update()
    {
        if (_audioSourceInitialized) return;

        var speaker = GetComponent<Speaker>();
        if (speaker == null) return;

        var audioSource = speaker.GetComponent<AudioSource>();
        if (audioSource == null) return;

        audioSource.spatialBlend = 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.dopplerLevel = 0f;

        _audioSourceInitialized = true;
        Debug.Log("[PlayerVoiceController] Speaker AudioSource 3D 설정 완료");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            VoiceManager.Instance?.SwitchToLobbyMode();
        }
    }


}
