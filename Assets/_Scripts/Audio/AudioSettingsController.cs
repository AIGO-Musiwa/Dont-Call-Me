using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// 로그 계산으로 UI슬라이더의 값을 AudioMixer의 (dB)값으로 변환하는 컨트롤러
/// </summary>
public class AudioSettingsController : MonoBehaviour
{
    [Header("중앙 제어반")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("UI 입력 단자 (Slider)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        // 슬라이더 값이 변할 때마다 믹서에 신호를 쏘도록 자동 체결
        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(val => SetVolume("Master_Vol", val));

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(val => SetVolume("SFX_Vol", val));
    }

    /// <summary>
    /// 선형적인 0~1 볼륨 값을 데시벨(dB) 스케일로 변환 (-80dB ~ 0dB)
    /// </summary>
    private void SetVolume(string parameterName, float sliderValue)
    {
        // sliderValue가 0일 때 발생하는 Mathf.Log10(0)의 무한대 에러 방지 (최소치 0.0001)
        float dB = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20f;

        mainMixer.SetFloat(parameterName, dB);
    }
}
