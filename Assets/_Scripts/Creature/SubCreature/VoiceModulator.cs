using Photon.Voice.Unity;
using UnityEngine;

public class VoiceModulator : MonoBehaviour
{
    [Header("피치 시프트 설정")]
    [Tooltip("피치 배율. 0.5 = 낮고 느린 목소리, 2.0 = 높고 빠른 목소리")]
    [SerializeField, Range(0.1f, 4.0f)] private float pitchFactor = 0.5f;

    // ── 상태 ──────────────────────────────────────────────
    private bool isEnhanced = false;
    private Speaker speaker;

    // 피치 시프트용 임시 버퍼
    private float[] tempBuffer;

    // ── 초기화 ────────────────────────────────────────────

    private void Awake()
    {
        speaker = GetComponent<Speaker>();
    }

    private void Update()
    {
        // Speaker가 RemoteVoice에 링크되면 핸들러 등록
        // Link()는 internal이라 직접 호출 불가 → 매 프레임 IsLinked 체크
        TryRegisterHandler();
    }

    private void OnDestroy()
    {
        UnregisterHandler();
    }

    // ── 핸들러 등록/해제 ──────────────────────────────────

    private bool isHandlerRegistered = false;

    private void TryRegisterHandler()
    {
        if (isHandlerRegistered) return;
        if (speaker == null || !speaker.IsLinked) return;

        speaker.RemoteVoice.FloatFrameDecoded += OnAudioFrameDecoded;
        isHandlerRegistered = true;
        Debug.Log($"[VoiceModulator] {gameObject.name} 핸들러 등록 완료");
    }

    private void UnregisterHandler()
    {
        if (!isHandlerRegistered) return;
        if (speaker?.RemoteVoice == null) return;

        speaker.RemoteVoice.FloatFrameDecoded -= OnAudioFrameDecoded;
        isHandlerRegistered = false;
    }

    // ── 외부 API ──────────────────────────────────────────

    /// <summary>
    /// 피치 변조를 활성/비활성한다.
    /// NoiseEnhancerGimmick → WalkieTalkieItem → 송신자 플레이어의 VoiceModulator에 호출.
    /// </summary>
    public void SetEnhanced(bool enhanced)
    {
        isEnhanced = enhanced;
        Debug.Log($"[VoiceModulator] {gameObject.name} SetEnhanced={enhanced}");
    }

    // ── 오디오 프레임 변조 ────────────────────────────────

    // FloatFrameDecoded 이벤트 핸들러.
    // Speaker가 AudioClip 버퍼에 데이터를 넣기 전에 호출된다.
    // frame.Buf를 직접 수정하면 재생 소리가 바뀐다.
    private void OnAudioFrameDecoded(Photon.Voice.FrameOut<float> frame)
    {
        if (!isEnhanced) return;
        if (frame.Buf == null || frame.Buf.Length == 0) return;

        ApplyPitchShift(frame.Buf);
    }

    // 임시 버퍼 방식 피치 시프트.
    private void ApplyPitchShift(float[] buf)
    {
        int length = buf.Length;

        // 임시 버퍼 크기 확인
        if (tempBuffer == null || tempBuffer.Length < length)
            tempBuffer = new float[length];

        // 원본 복사
        System.Array.Copy(buf, tempBuffer, length);

        for (int i = 0; i < length; i++)
        {
            // pitchFactor 배율로 읽을 원본 위치 계산
            float srcPos = i * pitchFactor;
            int idx0 = Mathf.Clamp((int)srcPos, 0, length - 1);
            int idx1 = Mathf.Clamp(idx0 + 1, 0, length - 1);
            float frac = srcPos - idx0;

            buf[i] = Mathf.Lerp(tempBuffer[idx0], tempBuffer[idx1], frac);
        }
    }
}
