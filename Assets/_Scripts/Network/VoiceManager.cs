using Photon.Voice.Unity;
using UnityEngine;

// 음성 연결 및 마이크 상태 관리
// NetworkManager와 같은 오브젝트에 추가, DontDestroyOnLoad로 씬 간 유지
//
// [NetworkRunner 프리팹 필수 설정]
// 1. FusionVoiceClient 컴포넌트 추가 (UsePrimaryRecorder = true)
// 2. Recorder 컴포넌트 추가 → FusionVoiceClient.PrimaryRecorder에 연결
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    [Header("마이크 감지")]
    [Tooltip("이 값 이상이면 마이크 활성화로 판단")]
    [SerializeField] private float micDetectThreshold = 0.02f;

    private Recorder       _recorder;
    private PlayerLobbyData _myLobbyData;
    private bool           _lastMicActive;

    // ── 생명주기 ───────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_recorder == null) return;
        MonitorMicLevel();
    }

    // ── 초기화 (NetworkManager에서 Runner 스폰 후 호출) ───────
    public void Init(Recorder recorder)
    {
        _recorder = recorder;

        if (_recorder == null)
            Debug.LogError("[VoiceManager] Recorder를 찾을 수 없습니다. NetworkRunner 프리팹을 확인하세요.");
        else
            Debug.Log("[VoiceManager] Recorder 연결 완료");
    }

    // ── 대기실에서 내 PlayerLobbyData 등록 ───────────────────
    // PlayerLobbyData.Spawned()에서 오너 클라이언트가 호출
    public void SetMyLobbyData(PlayerLobbyData data)
    {
        _myLobbyData = data;
        _lastMicActive = false;
    }

    public void ClearLobbyData() => _myLobbyData = null;

    // ── 내부 ──────────────────────────────────────────────────
    private void MonitorMicLevel()
    {
        if (_recorder.LevelMeter == null) return;

        float level    = _recorder.LevelMeter.CurrentAvgAmp;
        bool  isActive = level > micDetectThreshold;

        if (isActive == _lastMicActive) return;

        _lastMicActive = isActive;
        _myLobbyData?.RPC_SetMicActive(isActive);
    }
}
