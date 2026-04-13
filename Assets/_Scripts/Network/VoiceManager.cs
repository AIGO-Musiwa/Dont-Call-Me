using Fusion;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    // 마이크 입력 음량의 기준값
    [Header("발화 감지 임계값 (0.001 ~ 0.1")]
    [SerializeField][Range(0.001f, 0.1f)] private float speakingThreshold = 0.02f;

    // ── 내부 ──────────────────────────────────────────────
    private PlayerLobbyData localData;
    private Recorder recorder;
    private VoiceConnection voiceConnection;
    private bool wasSpeaking;

    // 보이스 그룹
    private byte pendingGroup = 0;
    private bool hasPendingGroup;       // 입장 전에 요청이 왔는지 확인

    // 현재 로컬 플레이어 구역
    private Zone localZone;

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
        UpdateLobbyMicIcon();
        TryApplyPendingGroup();
    }

    private void UpdateLobbyMicIcon()
    {
        if (localData == null || recorder == null) return;

        float level = recorder.LevelMeter?.CurrentAvgAmp ?? 0f;
        bool isSpeaking = level > speakingThreshold;

        if (isSpeaking == wasSpeaking) return;

        wasSpeaking = isSpeaking;
        localData.Rpc_SetMicActive(isSpeaking);
    }

    // Voice 룸 입장 대기 후 그룹 적용
    private void TryApplyPendingGroup()
    {
        if (!hasPendingGroup) return;
        if (voiceConnection == null) FetchComponents();
        if (voiceConnection == null) return;

        if (voiceConnection.Client.State != ClientState.Joined) return;

        ApplyGroup(new byte[] { pendingGroup });
        hasPendingGroup = false;
    }

    #endregion

    #region 로비 API

    // 로컬 플레이어 데이터 등록 및 Voice 룸에 연결
    public void RegisterLocalPlayer(PlayerLobbyData data)
    {
        localData = data;
        FetchComponents();
        SwitchToLobbyMode();
    }

    // 방 퇴장 시 상태 초기화
    public void Unregister()
    {
        localData = null;
        recorder = null;
        voiceConnection = null;
        wasSpeaking = false;
    }

    #endregion

    #region Voice Group 전환

    // 로비 모드 - 전원의 목소리가 서로 들림 (Group 0)
    public void SwitchToLobbyMode()
    {
        SetVoiceGroup(Constants.GROUP_LOBBY);
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
        {
            FetchComponents();
        }

        if (recorder != null)
        {
            recorder.InterestGroup = groupId;
        }

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

        byte myGroup = localZone == Zone.ZoneA
            ? Constants.GROUP_ZONE_A
            : Constants.GROUP_ZONE_B;

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

        byte myGroup = localZone == Zone.ZoneA
            ? Constants.GROUP_ZONE_A
            : Constants.GROUP_ZONE_B;

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

        byte myGroup = localZone == Zone.ZoneA
            ? Constants.GROUP_ZONE_A
            : Constants.GROUP_ZONE_B;

        recorder.InterestGroup = isNear ? Constants.GROUP_WALKIE : myGroup;

        ApplyGroup(isNear
            ? new byte[] { myGroup, Constants.GROUP_WALKIE }
            : new byte[] { myGroup });
    }

    #endregion

    private void ApplyGroup(byte[] groups)
    {
        // null -> 기존 구독 전부 해제 후 새 그룹만 구독
        voiceConnection.Client.OpChangeGroups(new byte[0], groups);
        Debug.Log($"[VoiceManager] Voice Group 적용 → [{string.Join(", ", groups)}]");
    }

    #endregion

    #region 내부 유틸

    private void FetchComponents()
    {
        var runner = GameLauncher.Instance?.Runner ?? FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        recorder = runner.GetComponent<Recorder>();
        voiceConnection = runner.GetComponent<VoiceConnection>();

        if (recorder == null)
        {
            Debug.LogWarning("[VoiceManager] Recorder를 찾지 못했습니다.");
        }
        if (voiceConnection == null)
        {
            Debug.LogWarning("[VoiceManager] VoiceConnection을 찾지 못했습니다.");
        }
    }

    #endregion
}
