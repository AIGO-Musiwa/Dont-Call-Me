using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingUI : MonoBehaviour
{
    [Header("오디오 믹서 (Audio Mixer)")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("볼륨 슬라이더 (Sliders)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("음성 설정 슬라이더")]
    [SerializeField] private Slider globalReceiveSlider;    // 다른 플레이어 전체 수신 볼륨 (0~1)

    private void Start()
    {
        // 저장된 값 로드
        float vMaster = PlayerPrefs.GetFloat(Constants.KEY_MASTER, 0.8f);
        float vBGM = PlayerPrefs.GetFloat(Constants.KEY_BGM, 0.7f);
        float vSFX = PlayerPrefs.GetFloat(Constants.KEY_SFX, 1.0f);
        float vGlobalRecv = PlayerPrefs.GetFloat(Constants.KEY_GLOBAL_RECEIVE_VOLUME, 1.0f);

        // 2. UI에 초기값 반영
        if (masterSlider) masterSlider.value = vMaster;
        if (bgmSlider) bgmSlider.value = vBGM;
        if (sfxSlider) sfxSlider.value = vSFX;
        if (globalReceiveSlider) globalReceiveSlider.value = vGlobalRecv;

        // 실제 시스템에 즉시 적용
        ApplyVolume(Constants.KEY_MASTER, vMaster);
        ApplyVolume(Constants.KEY_BGM, vBGM);
        ApplyVolume(Constants.KEY_SFX, vSFX);
        ApplyGlobalReceiveVolume(vGlobalRecv);

        // 슬라이더 이벤트 연결
        masterSlider?.onValueChanged.AddListener(val =>
        {
            ApplyVolume(Constants.KEY_MASTER, val);
            PlayerPrefs.SetFloat(Constants.KEY_MASTER, val);
        });

        bgmSlider?.onValueChanged.AddListener(val =>
        {
            ApplyVolume(Constants.KEY_BGM, val);
            PlayerPrefs.SetFloat(Constants.KEY_BGM, val);
        });
        sfxSlider?.onValueChanged.AddListener(val =>
        {
            ApplyVolume(Constants.KEY_SFX, val);
            PlayerPrefs.SetFloat(Constants.KEY_SFX, val);
        });
        globalReceiveSlider?.onValueChanged.AddListener(val =>
        {
            ApplyGlobalReceiveVolume(val);
            PlayerPrefs.SetFloat(Constants.KEY_GLOBAL_RECEIVE_VOLUME, val);
        });

    }

    // 0~1 선형 값을 dB로 변환해 AudioMixer 파라미터에 적용
    private void ApplyVolume(string paramName, float value)
    {
        if (mainMixer == null) return;

        // 로그 계산을 통해 인간의 청감 특성에 맞는 데시벨(dB)로 변환 (-80dB ~ 20dB)
        float db = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
        mainMixer.SetFloat(paramName, db);
    }

    // 다른 플레이어 목소리 크기 조절
    private void ApplyGlobalReceiveVolume(float value)
    {
        VoiceManager.Instance?.SetGlobalReceiveVolume(value);
    }
}
