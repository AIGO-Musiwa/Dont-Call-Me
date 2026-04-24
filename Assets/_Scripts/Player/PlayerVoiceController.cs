using Fusion;
using UnityEngine;

public class PlayerVoiceController : NetworkBehaviour
{
    private PlayerController playerController;

    [Header("근접 음성 감쇠 설정")]
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 10f;

    // 같은 구역 팀원 캐시
    private PlayerController teammatePc;
    private AudioSource teammateAudioSource;

    private Transform listenerTransform;

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

        if (playerController.IsSpectatorState())
            UpdateSpectatorVolume();
        else
            UpdateTeammateVolume();
    }
    
    // Normal 상태 - 동일 구역 팀원 목소리 감쇠
    private void UpdateTeammateVolume()
    {
        if (teammatePc == null || teammateAudioSource == null) return;

        float dist = Vector3.Distance(transform.position, teammatePc.transform.position);

        float volume = dist >= maxDistance
            ? 0f
            : Mathf.Clamp01(minDistance / Mathf.Max(dist, minDistance));
        teammateAudioSource.volume = volume;
    }

    private void UpdateSpectatorVolume()
    {
        PlayerController target = VoiceManager.Instance?.GetSpectatingTarget();
        if (target == null) return;
        if (listenerTransform == null) return;

        // 관전 대상 AudioSource 항상 최대 볼륨
        AudioSource targetAudio = target.GetComponent<AudioSource>();
        if (targetAudio != null)
            targetAudio.volume = 1f;

        // 관전 대상 팀원 찾기
        PlayerController teammate = FindTeammateOf(target);
        if (teammate == null) return;

        // 관전 대상 팀원 AudioSource 관전 대상 위치 기준 소리 감쇠
        AudioSource teammateAudio = teammate.GetComponent<AudioSource>();
        if (teammateAudio == null) return;

        float dist = Vector3.Distance(listenerTransform.position, teammate.transform.position);
        float volume = dist >= maxDistance
            ? 0f
            : Mathf.Clamp01(minDistance / Mathf.Max(dist, minDistance));
        teammateAudio.volume = volume;
    }

    // spectatingTarget과 같은 구역이면서 살아있는 팀원 반환
    private PlayerController FindTeammateOf(PlayerController target)
    {
        var allPlayers = WalkieTalkieManager.Instance?.GetCachedPlayers();
        if (allPlayers == null) return null;

        foreach (var pc in allPlayers)
        {
            if (pc == null) continue;
            if (pc == target) continue;
            if (pc.NetZone != target.NetZone) continue;
            if (pc.NetPlayerState == PlayerState.Dead || pc.NetPlayerState == PlayerState.Escaped) continue;

            return pc;
        }
        return null;
    }

    // ── 외부 API ────────────────────────────────────────────────

    public void SetListenerTransform(Transform listenerTransform)
    {
        this.listenerTransform = listenerTransform;
    }

    // PlayerController.OnPlayerStateChanged에서 호출
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
            VoiceManager.Instance?.SwitchToLobbyMode();
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
