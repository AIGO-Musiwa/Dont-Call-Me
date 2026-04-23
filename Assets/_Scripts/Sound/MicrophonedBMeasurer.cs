using Photon.Voice.Unity;
using UnityEngine;

public class MicrophonedBMeasurer : MonoBehaviour
{
    public static MicrophonedBMeasurer Instance { get; private set; }

    [Header("측정 주기 (초)")]
    [SerializeField] private float measureInterval = 0.05f;

    private float _measureTimer;
    private bool isPTTActive;
    private Recorder recorder;
    private PlayerController localPc;

    private const float silenceThreshold = 0.001f;      // 이 값 미만이면 무음 판정

    public float CurrentNaturaldB { get; private set; } = 0f;
    public float CurrentWalkiedB { get; private set; } = 0f;

    // 마이크 진폭을 dBFS로 변환한 연속값
    public float CurrentRawdBFS { get; private set; } = -96f;
    public bool IsPTTActive => isPTTActive;

    #region Unity LifeCycle

    private void Start()
    {
        localPc = GetComponent<PlayerController>();

        if (localPc == null)
        {
            Debug.LogError("[MicrophonedBMeasurer] PlayerController를 찾을 수 없습니다.");
            return;
        }

        if (!localPc.HasInputAuthority) return;

        // 로컬 플레이어만 Instance 등록
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 로컬 플레이어가 아니면 실행 안함
        if (Instance != this) return;

        if (recorder == null)
        {
            TryFetchRecorder();
            return;
        }

        _measureTimer += Time.deltaTime;
        if (_measureTimer < measureInterval) return;
        _measureTimer = 0f;

        Measure();
    }

    #endregion

    #region 외부 API

    // PTT 상태 변경 시 호출
    public void SetPTTActive(bool isOn)
    {
        isPTTActive = isOn;
        if (!isOn) CurrentWalkiedB = 0f;
    }

    #endregion

    #region 측정 및 발행

    private void Measure()
    {
        // 관전자 상태면 소리 발행 안함
        if (localPc.NetPlayerState == PlayerState.Dead ||
            localPc.NetPlayerState == PlayerState.Escaped) return;

        float rms = recorder.LevelMeter?.CurrentAvgAmp ?? 0f;

        if (rms < silenceThreshold)
        {
            CurrentNaturaldB = 0f;
            CurrentWalkiedB = 0f;
            CurrentRawdBFS = -96f;

            if (isPTTActive)
                EmitWalkieSoundFromZoneWalkie(30f);

            return;
        }

        float dBfs = AmpTodBFS(rms);
        CurrentRawdBFS = dBfs;

        // 자연음 (플레이어 음성)
        float naturaldB = dBFSToNaturaldB(dBfs);
        CurrentNaturaldB = naturaldB;

        if (naturaldB > 0f)
            SoundEmitter.EmitNatural(naturaldB, transform.position, localPc.NetZone, localPc);

        // 무전음
        if (!isPTTActive) return;

        float walkiedB = dBFSToWalkiedB(dBfs);
        CurrentWalkiedB = walkiedB;

        if (walkiedB > 0f)
            EmitWalkieSoundFromZoneWalkie(walkiedB);
    }

    // 송신 구역 무전기에서 수신 구역 무전기 위치로 소리 이벤트 발행
    private void EmitWalkieSoundFromZoneWalkie(float voicedB)
    {
        if (WalkieTalkieManager.Instance == null) return;

        WalkieTalkieItem senderWalkie = WalkieTalkieManager.Instance.GetWalkieTalkieByZone(localPc.NetZone);
        if (senderWalkie == null) return;

        senderWalkie.RPC_EmitWalkieSound(voicedB);
    }
   

    #endregion

    #region dB 변환 유틸

    private static float AmpTodBFS(float amp)
    {
        if (amp <= 0f) return -96f;
        return 20f * Mathf.Log10(amp);
    }

    private static float dBFSToNaturaldB(float dBfs)
    {
        if (dBfs <= -45f) return 30f;       // 속삭임
        if (dBfs <= -30f) return 38f;       // 일반 대화
        if (dBfs <= -20f) return 41f;       // 큰 목소리
        return 43f;                         // 고함
    }

    private static float dBFSToWalkiedB(float dBfs)
    {
        if (dBfs <= -45f) return 36f;
        if (dBfs <= -30f) return 44f;
        if (dBfs <= -20f) return 47f;
        return 49f;
    }

    #endregion

    private void TryFetchRecorder()
    {
        if (VoiceManager.Instance?.LocalRecorder != null)
            recorder = VoiceManager.Instance.LocalRecorder;
    }
}
