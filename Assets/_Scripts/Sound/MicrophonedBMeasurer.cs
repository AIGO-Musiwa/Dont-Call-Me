using Fusion;
using Photon.Voice.Unity;
using UnityEditor.ShaderGraph.Internal;
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

        // 로컬 플레이어만 Instance 등록
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
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
        float rms = recorder.LevelMeter?.CurrentAvgAmp ?? 0f;

        if (rms < silenceThreshold)
        {
            CurrentNaturaldB = 0f;
            CurrentWalkiedB = 0f;
            CurrentRawdBFS = -96f;

            if (isPTTActive)
            {
                Zone receiverZone = (localPc.NetZone == Zone.ZoneA) ? Zone.ZoneB : Zone.ZoneA;
                SoundEmitter.EmitWalkie(30f, GetReceiverWalkiePosition(), receiverZone, localPc);
            }

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
        {
            Zone receiverZone = (localPc.NetZone == Zone.ZoneA) ? Zone.ZoneB : Zone.ZoneA;
            SoundEmitter.EmitWalkie(walkiedB, GetReceiverWalkiePosition(), receiverZone, localPc);
        }
    }

    private Vector3 GetReceiverWalkiePosition()
    {
        if (WalkieTalkieManager.Instance == null) return transform.position;

        Zone receiverZone = (localPc.NetZone == Zone.ZoneA) ? Zone.ZoneB : Zone.ZoneA;
        WalkieTalkieItem receiverWalkie = WalkieTalkieManager.Instance.GetWalkieTalkieByZone(receiverZone);

        return receiverWalkie != null ? receiverWalkie.transform.position : transform.position;
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
        if (dBfs <= -45f) return 80f;       // 속삭임
        if (dBfs <= -30f) return 80f;       // 일반 대화
        if (dBfs <= -20f) return 80f;       // 큰 목소리
        return 80f;                         // 고함
    }

    private static float dBFSToWalkiedB(float dBfs)
    {
        if (dBfs <= -45f) return 80f;
        if (dBfs <= -30f) return 80f;
        if (dBfs <= -20f) return 80f;
        return 80f;
    }

    #endregion

    private void TryFetchRecorder()
    {
        if (VoiceManager.Instance?.LocalRecorder != null)
            recorder = VoiceManager.Instance.LocalRecorder;
    }
}
