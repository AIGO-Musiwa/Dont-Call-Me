using UnityEngine;

public class WalkieTalkieNoiseFilter : MonoBehaviour
{
    [Header("High Pass Filter")]
    [Tooltip("차단 주파수 비율 (0~1). 높을수록 더 많은 저음 차단")]
    [SerializeField, Range(0f, 1f)] private float highPassCutoff = 0.3f;

    [Header("화이트 노이즈")]
    [Tooltip("노이즈 혼합 비율 (0: 노이즈 x, 1: 원본 x")]
    [SerializeField, Range(0f, 0.3f)] private float noiseAmount = 0.04f;

    [Header("Distortion")]
    [Tooltip("왜곡 강도 (0: 왜곡 x, 1: 최대 왜곡")]
    [SerializeField, Range(0f, 1f)] private float distortionAmount = 0.2f;

    // 현재 노이즈 필터 활성 상태
    private bool isActive;

    // High Pass Filter 상태값 (채널별)
    // 1차 IIR High Pass: y[n] = α * (y[n-1] + x[n] - x[n-1])
    private float[] hpfPrev;        // 이전 입력값 [채널별]
    private float[] hpfPrevOut;     // 이전 출력값 [채널별]
    private int channelCount;

    private System.Random sysRandom = new System.Random();

    // ── 외부 API ─────────────────────────────────────────

    // true: 변조 ON / false: 변조 OFF
    public void SetNoiseActive(bool active)
    {
        isActive = active;
    }

    // ── OnAudioFilterRead ─────────────────────────────────

    // Unity 오디오 스레드에서 호출, 실시간으로 오디오 데이터를 처리
    // Speaker AudioSource와 같은 GameObject에 있어야 동작.
    // data: 인터리브된 PCM 샘플 배열 (채널 수만큼 반복)
    private void OnAudioFilterRead(float[] data, int channels)
    {
        Debug.Log("여기 노이즈 적용 전");
        if (!isActive) return;
        Debug.Log("노이즈ㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡ");

        float before = data[0];

        // 채널 수 변경 시 상태값 ㅊ기화
        if (hpfPrev == null || channelCount != channels)
        {
            channelCount = channels;
            hpfPrev = new float[channels];
            hpfPrevOut = new float[channels];
        }

        // α 계산: 높을수록 저음 차단이 강해짐 (0.5 ~ 0.95 범위 권장)
        float alpha = 0.5f + highPassCutoff * 0.45f;

        for (int i = 0; i < data.Length; i++)
        {
            int ch = i % channels;
            float input = data[i];

            // high pass filter
            float hpfOut = alpha * (hpfPrevOut[ch] + input - hpfPrev[ch]);
            hpfPrev[ch] = input;
            hpfPrevOut[ch] = hpfOut;

            // Distortion (소프트 클리핑)
            float distorted = hpfOut;
            if (distortionAmount > 0f)
            {
                float drive = 1f + distortionAmount * 10f;
                distorted = Mathf.Clamp(hpfOut * drive, -1f, 1f);
                // 소프트 클리핑으로 부드럽게 왜곡
                distorted = distorted - (distorted * distorted * distorted) / 3f;
                // 원본과 왜곡 신호 블렌딩
                distorted = Mathf.Lerp(hpfOut, distorted, distortionAmount);
            }

            // white noise
            float noise = (float)(sysRandom.NextDouble() * 2.0 - 1.0) * noiseAmount;

            data[i] = Mathf.Clamp(distorted + noise, -1f, 1f);
        }
        Debug.Log($"[NoiseFilter] before: {before:F4} / after: {data[0]:F4}");
    }
}
