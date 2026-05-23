using Fusion;
using UnityEngine;

public class PlayerVoiceController : NetworkBehaviour
{
    private PlayerController playerController;

    [Header("근접 음성 감쇠 설정")]
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 10f;

    [Header("근접 음성 방향성 설정")]
    [SerializeField, Range(0f, 1f)] private float proximityPanRange = 0.8f;

    [Header("벽 감쇠 설정")]
    [Tooltip("벽/지형 레이어 마스크 -> 이것만 벽으로 인식")]
    [SerializeField] private LayerMask obstructionMask;

    [Tooltip("벽 1개당 볼륨 배율")]
    [SerializeField, Range(0f, 1f)] private float perWallAttenuation = 0.75f;

    [Tooltip("감쇠를 적용할 최대 벽 개수")]
    [SerializeField] private int maxWallCount = 3;

    // 같은 구역 팀원 캐시
    private PlayerController teammatePc;
    private AudioSource teammateAudioSource;

    private Transform soundOrigin;

    private float globalVolume = 1f;        // 다른 플레이어 전체 수신 볼륨 배율 (0 ~ 1)

    public override void Spawned()
    {
        playerController = GetComponent<PlayerController>();

        // 네트워크 권한과 데이터가 확실히 세팅된 시점
        if (playerController == null || !HasInputAuthority) return;

        // PlayerPrefs에서 globalVolume 불러오기
        globalVolume = PlayerPrefs.GetFloat(Constants.KEY_GLOBAL_RECEIVE_VOLUME, 1f);

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

        float distanceVolume = dist >= maxDistance
            ? 0f
            : Mathf.Clamp01(minDistance / Mathf.Max(dist, minDistance));

        // 벽 감쇠 계산
        float obstructionMultiplier = VoiceObstructionDetector.GetObstructionMultiplier(
            transform.position, teammatePc.transform.position, obstructionMask, perWallAttenuation, maxWallCount);

        // 최종 볼륨 계산
        teammateAudioSource.volume = globalVolume * distanceVolume * obstructionMultiplier;

        // 좌우 방향성
        teammateAudioSource.panStereo = VoicePanCalculator.Calculate(transform, teammatePc.transform.position, proximityPanRange);
    }

    private void UpdateSpectatorVolume()
    {
        PlayerController target = VoiceManager.Instance?.GetSpectatingTarget();
        if (target == null) return;
        if (soundOrigin == null) return;

        // 관전 대상 AudioSource -> globalVolume 적용
        AudioSource targetAudio = target.GetComponent<AudioSource>();
        if (targetAudio != null)
        {
            targetAudio.volume = globalVolume;

            // 좌우 방향성
            targetAudio.panStereo = VoicePanCalculator.Calculate(soundOrigin, target.transform.position, proximityPanRange);
        }

        // 관전 대상 팀원 찾기
        PlayerController teammate = FindTeammateOf(target);
        if (teammate == null) return;

        // 관전 대상 팀원 AudioSource 관전 대상 위치 기준 소리 감쇠
        AudioSource teammateAudio = teammate.GetComponent<AudioSource>();
        if (teammateAudio == null) return;

        float dist = Vector3.Distance(soundOrigin.position, teammate.transform.position);
        float distanceVolume = dist >= maxDistance
            ? 0f
            : Mathf.Clamp01(minDistance / Mathf.Max(dist, minDistance));

        // 벽 감쇠
        float obstructionMultiplier = VoiceObstructionDetector.GetObstructionMultiplier(
            soundOrigin.position, teammate.transform.position, obstructionMask, perWallAttenuation, maxWallCount);

        teammateAudio.volume = globalVolume * distanceVolume * obstructionMultiplier;

        // 좌우 방향성
        teammateAudio.panStereo = VoicePanCalculator.Calculate(soundOrigin, teammate.transform.position, proximityPanRange);
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

    public void SetListenerTransform(Transform origin)
    {
        soundOrigin = origin;
    }

    // 전체 수신 볼륨 설정
    public void SetGlobalVolume(float volume)
    {
        globalVolume = Mathf.Clamp(volume, 0f, 3f);
    }

    // PlayerController.OnPlayerStateChanged에서 호출
    public void OnNetPlayerStateChanged()
    {
        if (!HasInputAuthority) return;

        if (playerController.NetPlayerState == PlayerState.Dead ||
            playerController.NetPlayerState == PlayerState.Escaped)
        {
            VoiceManager.Instance?.SetSpectatotrSendGroup();
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
