using Fusion;
using Photon.Realtime;
using Photon.Voice.Unity;
using System.Data;
using UnityEngine;
using static Unity.Collections.Unicode;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    // ── 내부 ──────────────────────────────────────────────
    private Recorder recorder;
    private VoiceConnection voiceConnection;

    // 보이스 그룹
    private byte pendingGroup = 0;
    private bool hasPendingGroup;       // 입장 전에 요청이 왔는지 확인

    // 현재 로컬 플레이어 구역
    private Zone localZone;

    // 관전 중인 대상
    private PlayerController spectatingTarget;
    private byte[] pendingSpectatorGroups;
    private bool hasPendingSpectatorGroups;

    public PlayerController GetSpectatingTarget() => spectatingTarget;

    public Recorder LocalRecorder => recorder;

    #region Unity LifeCycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        spectatingTarget = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        TryApplyPendingGroup();
    }

    // Voice 룸 입장 대기 후 그룹 적용
    private void TryApplyPendingGroup()
    {
        if (voiceConnection == null) FetchComponents();
        if (voiceConnection == null) return;
        if (voiceConnection.Client.State != ClientState.Joined) return;

        if (hasPendingSpectatorGroups && pendingSpectatorGroups != null)
        {
            ApplyGroup(pendingSpectatorGroups);
            hasPendingSpectatorGroups = false;
            pendingSpectatorGroups = null;
        }
        else if (hasPendingGroup)
        {
            ApplyGroup(new byte[] { pendingGroup });
            hasPendingGroup = false;
        }
    }

    #endregion

    #region 로비 API

    // 로컬 플레이어 데이터 등록 및 Voice 룸에 연결
    public void RegisterLocalPlayer(PlayerData data)
    {
        FetchComponents();
        ApplySavedMicDevice();
        InitializeWebRtcDsp();  // 저장된 마이크 장치 적용
        SwitchToLobbyMode();
    }

    // 방 퇴장 시 상태 초기화
    public void Unregister()
    {
        recorder = null;
        voiceConnection = null;
    }

    #endregion

    #region WebRtc DSP 초기화

    private void InitializeWebRtcDsp()
    {
        var runner = GameLauncher.Instance?.Runner ?? FindAnyObjectByType<NetworkRunner>();
        if (runner == null)
        {
            Debug.LogWarning("[VoiceManager] InitializeWebRtcDsp: NetworkRunner를 찾지 못했습니다.");
            return;
        }

        var webRtcDsp = runner.GetComponent<WebRtcAudioDsp>();

        if (webRtcDsp == null)
            webRtcDsp = FindAnyObjectByType<WebRtcAudioDsp>();

        if (webRtcDsp == null)
        {
            Debug.LogWarning("[SettingsManager] WebRtcAudioDsp를 찾지 못했습니다.");
            return;
        }

        webRtcDsp.NoiseSuppression = true;      // 노이즈 억제
        webRtcDsp.AEC = true;                   // 에코 억제
        webRtcDsp.HighPass = true;              // 저주파 잡음 제거
        webRtcDsp.AGC = false;                  // 자동 볼륨 조절

        Debug.Log("[SettingsManager] WebRtcAudioDsp 초기화 완료 (NS: ON, AEC: ON, AGC: OFF)");
    }

    #endregion

    #region Voice Group 전환

    // 로비 모드 - 전원의 목소리가 서로 들림 (Group 0)
    public void SwitchToLobbyMode()
    {
        SetVoiceGroup(Constants.GROUP_LOBBY);
    }

    // 관전 모드 - 송신 그룹 전환
    public void SetSpectatotrSendGroup()
    {
        if (recorder == null) FetchComponents();
        if (recorder == null) return;

        recorder.InterestGroup = Constants.GROUP_SPECTATOR;
        Debug.Log("[VoiceManager] GROUP_SPECTATOR 송신 전환");
    }

    // 관전 모드 - 사망/탈출한 플레이어끼리만 소통
    public void SwitchToSpectatorMode(Zone targetZone)
    {
        if (recorder == null) FetchComponents();
        if (recorder == null) return;

        UpdateSpectatorZone(targetZone);

        Debug.Log($"[VoiceManager] 관전 모드 → {targetZone} 구독 + GROUP_SPECTATOR 송신");
    }

    // 관전 대상 구역 변경 시 구독 갱신
    public void UpdateSpectatorZone(Zone targetZone)
    {
        byte targetGroup = targetZone == Zone.ZoneA ? Constants.GROUP_ZONE_A : Constants.GROUP_ZONE_B;
        byte[] groups = new byte[] {targetGroup, Constants.GROUP_WALKIE, Constants.GROUP_SPECTATOR};

        if (voiceConnection?.Client != null && voiceConnection.Client.State == ClientState.Joined)
        {
            ApplyGroup(groups);
        }
        else
        {
            pendingSpectatorGroups = groups;
            hasPendingSpectatorGroups = true;
        }
    }

    // 관전 대상 위치 저장
    public void SetSpectatingTarget(PlayerController target)
    {
        spectatingTarget = target;
    }

    // 인게임 모드 - 같은 구역 플레이어끼리만 소통
    public void SwitchToGameMode(Zone zone)
    {
        localZone = zone;
        byte groupId = zone == Zone.ZoneA ? Constants.GROUP_ZONE_A : Constants.GROUP_ZONE_B;
        SetVoiceGroup(groupId);
    }

    private void SetVoiceGroup(byte groupId)
    {
        if (recorder == null || voiceConnection == null)
            FetchComponents();

        if (recorder != null)
            recorder.InterestGroup = groupId;

        if (voiceConnection?.Client != null && voiceConnection.Client.State == ClientState.Joined)
        {
            ApplyGroup(new byte[] { groupId });
        }
        else
        {
            pendingGroup = groupId;
            hasPendingGroup = true;
            Debug.Log($"[VoiceManager] Voice 룸 입장 대기 중. Group {groupId} 예약. 현재 상태: {voiceConnection?.Client?.State}");
        }
    }

    #endregion

    #region 무전기 API

    // PTT on/off
    public void SetPTT(bool isOn)
    {
        if (recorder == null) FetchComponents();
        if (recorder == null) return;

        byte myGroup = localZone == Zone.ZoneA
            ? Constants.GROUP_ZONE_A
            : Constants.GROUP_ZONE_B;

        if (isOn)
        {
            // GROUP_WALKIE로 송신 전환
            recorder.InterestGroup = Constants.GROUP_WALKIE;

            // 같은 구역 팀원도 GROUP_WALKIE 구독 추가
            ApplyGroup(new byte[] { myGroup, Constants.GROUP_WALKIE });
            Debug.Log("[VoiceManager] PTT ON → GROUP_WALKIE 송신 + 팀원 구독 추가");
        }
        else
        {
            // 본인 구역 Group으로 복귀
            recorder.InterestGroup = myGroup;

            // 팀원 GROUP_WALKIE 구독 해제
            ApplyGroup(new byte[] { myGroup });
            Debug.Log($"[VoiceManager] PTT OFF → {localZone} Group 복귀");
        }
    }

    // 수신자 GROUP_WALKIE 구독 추가/해제
    public void SetRemotePTT(bool isOn, Zone senderZone)
    {
        if (recorder == null) FetchComponents();
        if (voiceConnection?.Client == null) return;

        // 송신자 구역 무시
        if (localZone == senderZone) return;

        byte myGroup = GetMyZoneGroup();

        ApplyGroup(isOn
            ? new byte[] { myGroup, Constants.GROUP_WALKIE }
            : new byte[] { myGroup });
        Debug.Log($"[VoiceManager] 원격 PTT {(isOn ? "시작" : "종료")} → GROUP_WALKIE {(isOn ? "구독 추가" : "구독 해제")}");
    }

    // 팀원이 수신자 근처에 들어오거나 벗어날 때 호출
    public void SetTeammateGroup(bool isNear)
    {
        if (recorder == null) FetchComponents();
        if (voiceConnection?.Client == null) return;

        byte myGroup = GetMyZoneGroup();

        ApplyGroup(isNear
            ? new byte[] { myGroup, Constants.GROUP_WALKIE }
            : new byte[] { myGroup });

        Debug.Log($"[VoiceManager] 팀원 근접 {(isNear ? "진입" : "이탈")} → GROUP_WALKIE {(isNear ? "구독 추가" : "구독 해제")}");
    }

    // 팀원이 송신자 근처에 들어오거나 벗어날 때 호출
    public void SetTeammateSenderGroup(bool isNear)
    {
        if (recorder == null) FetchComponents();
        if (voiceConnection?.Client == null) return;

        byte myGroup = GetMyZoneGroup();

        recorder.InterestGroup = isNear ? Constants.GROUP_WALKIE : myGroup;

        ApplyGroup(new byte[] { myGroup, Constants.GROUP_WALKIE });

        Debug.Log($"[VoiceManager] 송신자 구역 팀원 A범위 {(isNear ? "진입" : "이탈")} " +
            $"→ Recorder {(isNear ? "GROUP_WALKIE" : $"{localZone} Group")}");
    }

    public void ResetSenderZoneTeammate()
    {
        if (recorder == null) FetchComponents();
        if (voiceConnection?.Client == null) return;

        byte myGroup = GetMyZoneGroup();

        recorder.InterestGroup = myGroup;
        ApplyGroup(new byte[] { myGroup });

        Debug.Log($"[VoiceManager] 송신자 구역 팀원 PTT 종료 → {localZone} Group 완전 복귀");
    }

    #endregion

    #region 사운드 설정 API

    // 마이크 게인 설정 (0 ~ 2) - 1이 원본, 1 초과 시 증폭
    public void SetMicGain(float gain)
    {
        MicAudioProcessor.Instance?.SetMicGain(gain);
    }

    // 다른 플레이어 수신 볼륨 설정 (0 ~ 1)
    public void SetGlobalReceiveVolume(float volume)
    {
        var allControllers = FindObjectsByType<PlayerVoiceController>(FindObjectsSortMode.None);
        bool hasRemoteController = false;

        foreach (var controller in allControllers)
        {
            if (!controller.HasInputAuthority) continue;
            controller.SetGlobalVolume(volume);
            hasRemoteController = true;
        }

        // 로비 씬: PlayerData의 Speaker AudioSource 직접 조절
        if (!hasRemoteController)
        {
            var allSpeakers = FindObjectsByType<Speaker>(FindObjectsSortMode.None);
            foreach(var speaker in allSpeakers)
            {
                var audioSource = speaker.GetComponent<AudioSource>();
                if (audioSource != null)
                    audioSource.volume = volume;
            }
        }
    }

    #endregion

    #region 내부 유틸
    private void ApplyGroup(byte[] groups)
    {
        // null -> 기존 구독 전부 해제 후 새 그룹만 구독
        voiceConnection.Client.OpChangeGroups(new byte[0], groups);
        Debug.Log($"[VoiceManager] Voice Group 적용 → [{string.Join(", ", groups)}]");
    }

    private byte GetMyZoneGroup()
    {
        return localZone == Zone.ZoneA
            ? Constants.GROUP_ZONE_A
            : Constants.GROUP_ZONE_B;
    }

    // PlayerPrefs에 저장된 마이크 장치를 Recorder에 적용
    private void ApplySavedMicDevice()
    {
        if (recorder == null) return;

        string savedDevice = PlayerPrefs.GetString("Mic_Device", "");
        if (string.IsNullOrEmpty(savedDevice)) return;

        foreach (var device in Microphone.devices)
        {
            if (device == savedDevice)
            {
                recorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(savedDevice);
                Debug.Log($"[VoiceManager] 저장된 마이크 장치 적용 → {savedDevice}");
                return;
            }
        }
        Debug.LogWarning($"[VoiceManager] 저장된 마이크 장치를 찾을 수 없음 → {savedDevice}");
        PlayerPrefs.DeleteKey("Mic_Device");
    }

    private void FetchComponents()
    {
        var runner = GameLauncher.Instance?.Runner ?? FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        recorder = runner.GetComponent<Recorder>();
        voiceConnection = runner.GetComponent<VoiceConnection>();

        if (recorder == null)
            Debug.LogWarning("[VoiceManager] Recorder를 찾지 못했습니다.");
        if (voiceConnection == null)
            Debug.LogWarning("[VoiceManager] VoiceConnection을 찾지 못했습니다.");
    }

    #endregion
}
