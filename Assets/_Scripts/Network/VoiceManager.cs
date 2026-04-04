using Photon.Realtime;
using Photon.Voice.Unity;
using System.Collections;
using UnityEngine;

public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    // 마이크 입력 음량의 기준값
    [Header("발화 감지 임계값 (0.001 ~ 0.1")]
    [SerializeField][Range(0.001f, 0.1f)] private float speakingThreshold = 0.02f;

    // ── 내부 ──────────────────────────────────────────────
    private PlayerLobbyData _localData;
    private Recorder _recorder;
    private bool _wasSpeaking;

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
        if (_localData == null || _recorder == null) return;

        float level = _recorder.LevelMeter?.CurrentAvgAmp ?? 0f;
        bool isSpeaking = level > speakingThreshold;

        if (isSpeaking == _wasSpeaking) return;

        _wasSpeaking = isSpeaking;
        _localData.Rpc_SetMicActive(isSpeaking);
    }

    #endregion

    #region 공개 API

    // 로컬 플레이어 데이터 등록 및 Voice 룸에 연결
    public void RegisterLocalPlayer(PlayerLobbyData data)
    {
        _localData = data;

        // NetworkRunner 프리팹에 붙어있는 Recorder를 런타임에 가져옴
        if (GameLauncher.Instance?.Runner != null)
        {
            _recorder = GameLauncher.Instance.Runner.GetComponent<Recorder>();
        }

        if (_recorder == null)
        {
            Debug.LogWarning("[VoiceManager] Recorder를 찾지 못했습니다. NetworkRunner 프리팹에 Recorder가 있는지 확인하세요.");
        }
    }

    // 방 퇴장 시 상태 초기화
    public void Unregister()
    {
        _localData = null;
        _recorder = null;
        _wasSpeaking = false;
    }

    #endregion
}
