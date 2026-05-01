using Photon.Voice;
using Photon.Voice.Unity;
using Photon.Voice.Unity.UtilityScripts;
using UnityEngine;
using UnityEngine.Audio;

public class MicAudioProcessor : MonoBehaviour
{
    public static MicAudioProcessor Instance { get; private set; }

    // ── MicAmplifierShort 인스턴스 ────────────────────────────────
    private MicAmplifierShort amplifier;

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // 기본 게인 1f
        float savedGain = PlayerPrefs.GetFloat(Constants.KEY_MIC_GAIN, 1f);
        amplifier = new MicAmplifierShort(Mathf.Clamp(savedGain, 0f, 2f));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        amplifier?.Dispose();
    }

    #endregion

    #region Photon Voice 파이프라인 등록

    // Recorder와 같은 게임 오브젝트에 있을 때
    // Photon Voice가 로컬 보이스 스트림 생성 시 자동으로 호출
    private void PhotonVoiceCreated(PhotonVoiceCreatedParams p)
    {
        // p.Voice 실제 타입은 LocalVoiceAudioFloat => 캐스팅 후 등록
        if (p.Voice is LocalVoiceAudioShort shortVoice)
        {
            shortVoice.AddPreProcessor(amplifier);
            Debug.Log("[MicAudioProcessor] LocalVoice 파이프라인 등록 완료");
        }
        else
        {
            Debug.LogWarning("[MicAudioProcessor] LocalVoiceAudioFloat 캐스팅 실패. 마이크 처리 비활성화.");
        }
    }

    #endregion

    #region 외부 API

    // 마이크 게인 설정
    public void SetMicGain(float gain)
    {
        if (amplifier == null) return;
        amplifier.AmplificationFactor = Mathf.Clamp(gain, 0f, 2f);
    }

    #endregion
}
