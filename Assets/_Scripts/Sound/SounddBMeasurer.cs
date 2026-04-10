using Photon.Voice.Unity;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

public class SounddBMeasurer : MonoBehaviour
{
    public static SounddBMeasurer Instance { get; private set; }

    [Header("측정 주기 (초)")]
    [SerializeField] private float measureInterval = 0.05f;
    private float _measureTimer;

    private const float silenceThreshold = 0.001f;      // 이 값 미만이면 무음 판정

    private bool isPTTActive;
    private Recorder recorder;

    public float CurrentNaturaldB { get; private set; } = 0f;
    public float CurrentWalkiedB { get; private set; } = 0f;

    // 마이크 진폭을 dBFS로 변환한 연속값
    public float CurrentRawdBFS { get; private set; } = -96f;

    #region Unity LifeCycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
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

        MeasureAndEmit();
    }

    #endregion

    #region 외부 API

    // PTT 상태 변경 시 호출
    public void SetPTTActive(bool isOn)
    {
        isPTTActive = isOn;
        if (!isOn) CurrentWalkiedB = 0f;
    }

    // 코스트 비연동, dB 즉시 비교 방식
    public void EmitEventSound(float voicedB, Vector3 sourcePosition)
    {
        SoundEventBus.Emit(new SoundEvent
        {
            channel = SoundChannel.Walkie,
            voicedB = voicedB,
            sourcePosition = sourcePosition,
            obstaclePenaltydB = 0f
        });
    }

    #endregion

    #region 측정 및 발행

    private void MeasureAndEmit()
    {
        float rms = recorder.LevelMeter?.CurrentAvgAmp ?? 0f;
        
        if (rms < silenceThreshold)
        {
            CurrentNaturaldB = 0f;
            CurrentWalkiedB = 0f;
            CurrentRawdBFS = -96f;
            return;
        }
        
        float dBfs = AmpTodBFS(rms);
        CurrentRawdBFS = dBfs;

        // 자연음 채널
        float naturaldB = dBFSToNaturaldB(dBfs);
        CurrentNaturaldB = naturaldB;

        // 말소리
        if (naturaldB > 0f)
        {
            SoundEventBus.Emit(new SoundEvent
            {
                channel = SoundChannel.Natural,
                voicedB = naturaldB,
                sourcePosition = transform.position,
                obstaclePenaltydB = 0f
            });
        }

        // 무전음 채널
        if (!isPTTActive) return;

        float walkiedB = dBFSToWalkiedB(dBfs);
        CurrentWalkiedB = walkiedB;

        if ( walkiedB > 0f)
        {
            SoundEventBus.Emit(new SoundEvent
            {
                channel = SoundChannel.Walkie,
                voicedB = walkiedB,
                sourcePosition = transform.position,
                obstaclePenaltydB = 0f
            });
        }
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
