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

        ApplyGroup(pendingGroup);
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
            ApplyGroup(groupId);
        }
        else
        {
            pendingGroup = groupId;
            hasPendingGroup = true;
            Debug.Log($"[VoiceManager] Voice 룸 입장 대기 중. Group {groupId} 예약.");
        }
    }

    private void ApplyGroup(byte groupId)
    {
        // null -> 기존 구독 전부 해제 후 새 그룹만 구독
        voiceConnection.Client.OpChangeGroups(null, new byte[] { groupId });
        Debug.Log($"[VoiceManager] Voice Group 적용 → {groupId}");
    }

    #endregion

    #region 내부 유틸

    private void FetchComponents()
    {
        var runner = GameLauncher.Instance?.Runner;
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
