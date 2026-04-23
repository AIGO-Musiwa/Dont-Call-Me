using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoiceController : NetworkBehaviour
{
    private PlayerController playerController;

    [Header("근접 음성 감쇠 설정")]
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 25f;

    // 같은 구역 팀원 캐시
    private PlayerController teammatePc;
    private AudioSource teammateAudioSource;

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();

        // 네트워크 권한과 데이터가 확실히 세팅된 시점
        if (playerController == null || !HasInputAuthority) return;

        VoiceManager.Instance?.SwitchToGameMode(playerController.NetZone);
    }

    private void Update()
    {
        if (!HasInputAuthority) return;
        if (teammatePc == null || teammateAudioSource == null) return;

        // 관전 중이면 관전 대상 위치 기준, 아니면 내 위치 기준
        Vector3 listenerPos = VoiceManager.Instance != null
            ? VoiceManager.Instance.GetListenerPosition(transform.position)
            : transform.position;

        float dist = Vector3.Distance(transform.position, teammatePc.transform.position);

        float volume = dist >= maxDistance
            ? 0f
            : Mathf.Clamp01(minDistance / Mathf.Max(dist, minDistance));
        teammateAudioSource.volume = volume;
    }
    
    public void OnNetPlayerStateChanged()
    {
        if (!HasInputAuthority) return;

        if (playerController.NetPlayerState == PlayerState.Dead ||
            playerController.NetPlayerState == PlayerState.Escaped)
        {
            VoiceManager.Instance?.SwitchToSpectatorMode(playerController.NetZone);
        }
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            VoiceManager.Instance?.SwitchToLobbyMode();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void Rpc_SetTeammateForVoice(NetworkId teammateId)
    {
        if (!Runner.TryFindObject(teammateId, out NetworkObject teammateObj)) return;

        teammatePc = teammateObj.GetComponent<PlayerController>();
        if (teammatePc == null) return;

        teammateAudioSource = teammatePc.GetComponent<AudioSource>();
        if (teammateAudioSource != null)
        {
            teammateAudioSource.spatialBlend = 0f;
            teammateAudioSource.dopplerLevel = 0f;
        }

        Debug.Log($"[PlayerVoiceController] 팀원 등록 완료 → {teammateObj.name}");
    }
}
