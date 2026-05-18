using UnityEngine;

public class WalkieTalkieNoiseFilter : MonoBehaviour
{
    [Header("High Pass Filter")]
    [Tooltip("차단 주파수 비율 (0~1). 높을수록 더 많은 저음 차단")]
    [SerializeField, Range(0f, 1f)] private float highPassCutoff = 0.5f;

    [Header("링 변조")]
    [Tooltip("반송파 주파수 (Hz). 낮을수록 떨림, 높을수록 금속성 전자음")]
    [SerializeField, Range(50f, 3000f)] private float carrierFrequency = 1800f;
    [Tooltip("링 변조 혼합 비율 (0: 원본, 1: 완전 변조")]
    [SerializeField, Range(0f, 1f)] private float ringModDepth = 0.5f;

    [Header("Distortion")]
    [Tooltip("왜곡 강도 (0: 왜곡 x, 1: 최대 왜곡")]
    [SerializeField, Range(0f, 1f)] private float distortionAmount = 0.5f;

    [Header("출력 볼륨 보상")]
    [Tooltip("변조 후 볼륨 보상 (1 = 원본, 1.5 = 50% 증폭")]
    [SerializeField, Range(0.5f, 3f)] private float outputGain = 1.5f;

    // 현재 노이즈 필터 활성 상태
    private bool isActive = false;

    // High Pass Filter 상태값 (채널별)
    // 1차 IIR High Pass: y[n] = α * (y[n-1] + x[n] - x[n-1])
    private float[] hpfPrev;        // 이전 입력값 [채널별]
    private float[] hpfPrevOut;     // 이전 출력값 [채널별]
    private int channelCount;

    private double ringPhase;       // 링 변조 위상 누적값 (사인파를 연속적으로 생성하기 위해 샘플마다 누적)
    private int sampleRate;         // 오디오 샘플링 레이트

    private void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    // ── 외부 API ─────────────────────────────────────────

    // true: 변조 ON / false: 변조 OFF
    public void SetNoiseActive(bool active)
    {
        isActive = active;

        // 비활성화 시 위상 초기화 -> 다음 활성화 시 깔끔하게 시작
        if (!active)
            ringPhase = 0;

    }

    // ── OnAudioFilterRead ─────────────────────────────────

    // Unity 오디오 스레드에서 호출, 실시간으로 오디오 데이터를 처리
    // Speaker AudioSource와 같은 GameObject에 있어야 동작.
    // data: 인터리브된 PCM 샘플 배열 (채널 수만큼 반복)
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isActive) return;

        // ── 일반 상태: HPF + 링변조 + 디스토션 ───────────
        if (hpfPrev == null || channelCount != channels)
        {
            channelCount = channels;
            hpfPrev = new float[channels];
            hpfPrevOut = new float[channels];
        }

        float alpha = 0.5f + highPassCutoff * 0.45f;
        double phaseIncrement = (System.Math.PI * 2.0 * carrierFrequency) / sampleRate;

        for (int i = 0; i < data.Length; i++)
        {
            int ch = i % channels;

            if (ch == 0)
            {
                ringPhase += phaseIncrement;
                if (ringPhase >= System.Math.PI * 2.0)
                    ringPhase -= System.Math.PI * 2.0;
            }

            float carrier = 0.5f + (float)System.Math.Sin(ringPhase) * 0.5f;
            float input = data[i];

            // HPF
            float hpfOut = alpha * (hpfPrevOut[ch] + input - hpfPrev[ch]);
            hpfPrev[ch] = input;
            hpfPrevOut[ch] = hpfOut;

            // 링 변조
            float ringMod = hpfOut * carrier;
            float modulated = Mathf.Lerp(hpfOut, ringMod, ringModDepth);

            // 디스토션 (소프트 클리핑)
            float distorted = modulated;
            if (distortionAmount > 0f)
            {
                float drive = 1f + distortionAmount * 10f;
                distorted = Mathf.Clamp(modulated * drive, -1f, 1f);
                distorted = distorted - (distorted * distorted * distorted) / 3f;
                distorted = Mathf.Lerp(modulated, distorted, distortionAmount);
            }

            data[i] = Mathf.Clamp(distorted * outputGain, -1f, 1f);
        }
    }
}
