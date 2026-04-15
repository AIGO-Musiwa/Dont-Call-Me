using UnityEngine;

public class SoundDetectionLogger : MonoBehaviour
{
    [Header("로그 설정")]
    [Tooltip("자연음 채널 로그 출력 여부")]
    [SerializeField] private bool logNatural = true;
    [Tooltip("무전음 채널 로그 출력 여부")]
    [SerializeField] private bool logWalkie = true;
    [Tooltip("perceivedDb가 이 값 이상일 때만 출력 (0이면 전부 출력)")]
    [SerializeField] private float minPerceivedDbToLog = 0f;

    private CreatureAI _creatureAI;
    private CreatureSensor _sensor;

    private void Awake()
    {
        _creatureAI = GetComponent<CreatureAI>();
        _sensor = GetComponent<CreatureSensor>();

        if (_creatureAI == null)
            Debug.LogError("[SoundDetectionLogger] CreatureAI를 찾을 수 없습니다.");
        if (_sensor == null)
            Debug.LogError("[SoundDetectionLogger] CreatureSensor를 찾을 수 없습니다.");
    }

    private void OnEnable()
    {
        SoundEventBus.OnSoundEmitted += OnSoundReceived;
    }

    private void OnDisable()
    {
        SoundEventBus.OnSoundEmitted -= OnSoundReceived;
    }

    private void OnSoundReceived(SoundEvent soundEvent)
    {
        // 채널 필터
        if (soundEvent.channel == SoundChannel.Natural && !logNatural) return;
        if (soundEvent.channel == SoundChannel.Walkie && !logWalkie) return;

        // 거리 및 체감 dB 계산
        float distance = Vector3.Distance(transform.position, soundEvent.sourcePosition);
        float perceivedDb = _sensor.CalculatePerceivedDb(
            soundEvent.voicedB,
            distance,
            soundEvent.obstaclePenaltydB
        );

        // 최소 dB 필터
        if (perceivedDb < minPerceivedDbToLog) return;

        // dB 구간 판정
        string dbZone;
        if (perceivedDb >= _sensor.criticalThresholdDB)
            dbZone = "Critical";
        else if (perceivedDb >= _sensor.alertThresholdDB)
            dbZone = "Alert";
        else
            dbZone = "Safe";

        if (dbZone == "Safe") return;

        // 크리처 현재 상태
        string creatureState = _creatureAI.currentState.ToString();

        Debug.Log(
            $"[SoundDetectionLogger] ({_creatureAI.myZone})\n" +
            $"  채널       : {soundEvent.channel}\n" +
            $"  발생원 dB  : {soundEvent.voicedB:F1} dB\n" +
            $"  차폐 패널티: -{soundEvent.obstaclePenaltydB:F1} dB\n" +
            $"  거리       : {distance:F1} m\n" +
            $"  체감 dB    : {perceivedDb:F1} dB  [{dbZone}]\n" +
            $"  발생 위치  : {soundEvent.sourcePosition}\n" +
            $"  크리처 상태: {creatureState}"
        );
    }
}
