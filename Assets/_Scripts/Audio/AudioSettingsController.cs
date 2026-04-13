using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// [기공사 전용] 마스터/SFX/BGM 3개 채널을 제어하는 오디오 중앙 제어반
/// </summary>
public class AudioSettingsController : MonoBehaviour
{
    [Header("중앙 제어반 (Mixer)")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("UI 입력 단자 (Sliders)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider bgmSlider; 

    private void Start()
    {
        // 1. 마스터 볼륨 체결
        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(val => SetVolume("Master_Vol", val));

        // 2. SFX 볼륨 체결
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(val => SetVolume("SFX_Vol", val));

        // 3. BGM 볼륨 체결 (새 통로 연결)
        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(val => SetVolume("BGM_Vol", val));
    }

    /// <summary>
    /// 인간의 청각 특성(로그 스케일)에 맞게 볼륨을 변환하여 전달
    /// 변환 공식: $dB = \log_{10}(\text{sliderValue}) \times 20$
    /// </summary>
    private void SetVolume(string parameterName, float sliderValue)
    {
        // sliderValue(0~1)를 dB(-80~0)로 변환
        // Mathf.Max를 통해 Log10(0)으로 인한 무한대 발산(에러) 방지
        float dB = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20f;

        mainMixer.SetFloat(parameterName, dB);
    }
}