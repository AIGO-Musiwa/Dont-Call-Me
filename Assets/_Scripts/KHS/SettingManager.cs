using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider micVolumeSlider;

    [Header("Controls")]
    [SerializeField] private Slider sensitivitySlider;

    private const string MasterVolKey = "MasterVolume";
    private const string MicVolKey = "MicVolume";
    private const string SensKey = "MouseSensitivity";

    private void Start()
    {
        // 1. 기존 설정값 로드 (없으면 기본값 사용)
        float savedMasterVol = PlayerPrefs.GetFloat(MasterVolKey, 0.75f);
        float savedMicVol = PlayerPrefs.GetFloat(MicVolKey, 0.75f);
        float savedSens = PlayerPrefs.GetFloat(SensKey, 1.0f);

        // 2. UI UI 반영
        masterVolumeSlider.value = savedMasterVol;
        micVolumeSlider.value = savedMicVol;
        sensitivitySlider.value = savedSens;

        // 3. 실제 시스템에 적용
        SetMasterVolume(savedMasterVol);
        SetMouseSensitivity(savedSens);

        // 리스너 등록
        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        sensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
    }

    public void SetMasterVolume(float value)
    {
        // Mixer는 데시벨(dB) 단위를 쓰므로 계산 필요 (-80 ~ 20)
        float db = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20;
        mainMixer.SetFloat("MasterParam", db);
        PlayerPrefs.SetFloat(MasterVolKey, value);
    }

    public void SetMouseSensitivity(float value)
    {
        // PlayerController나 LookView의 감도 변수에 전달
        // 예: localPlayer.LookView.Sensitivity = value;
        PlayerPrefs.SetFloat(SensKey, value);
    }

    // 설정창을 닫을 때 저장
    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }
}