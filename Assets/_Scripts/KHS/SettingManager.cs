using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

/// <summary>
/// 게임의 모든 환경 설정을 관리하는 중앙 관제 모듈.
/// 오디오 믹서 및 감도 설정을 로컬 저장소(PlayerPrefs)와 동기화한다.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("오디오 믹서 (Audio Mixer)")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("볼륨 슬라이더 (Sliders)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider micSlider;

    [Header("조작 설정 (Controls)")]
    [SerializeField] private Slider sensitivitySlider;

    // 데이터 저장을 위한 고유 키값 (오타 방지)
    private const string KeyMaster = "Vol_Master";
    private const string KeyBGM = "Vol_BGM";
    private const string KeySFX = "Vol_SFX";
    private const string KeyMic = "Vol_Mic";
    private const string KeySens = "Mouse_Sens";

    private void Start()
    {
        // 1. 저장된 전압(설정값) 로드 (데이터 기반 로드)
        float vMaster = PlayerPrefs.GetFloat(KeyMaster, 0.8f);
        float vBGM = PlayerPrefs.GetFloat(KeyBGM, 0.7f);
        float vSFX = PlayerPrefs.GetFloat(KeySFX, 1.0f);
        float vMic = PlayerPrefs.GetFloat(KeyMic, 1.0f);
        float vSens = PlayerPrefs.GetFloat(KeySens, 1.0f);

        // 2. 계기판(UI)에 현재 값 반영
        if (masterSlider) masterSlider.value = vMaster;
        if (bgmSlider) bgmSlider.value = vBGM;
        if (sfxSlider) sfxSlider.value = vSFX;
        if (micSlider) micSlider.value = vMic;
        if (sensitivitySlider) sensitivitySlider.value = vSens;

        // 3. 실제 시스템 회로에 값 인가
        ApplyVolume("MasterParam", vMaster);
        ApplyVolume("BGMParam", vBGM);
        ApplyVolume("SFXParam", vSFX);
        ApplyVolume("MicParam", vMic);
        // 감도는 PlayerController 등에서 이 클래스의 정적 변수나 데이터를 참조하게 하면 좋아.

        // 4. 슬라이더 이벤트 배선 연결 (실시간 조절)
        masterSlider?.onValueChanged.AddListener(val => { ApplyVolume("MasterParam", val); PlayerPrefs.SetFloat(KeyMaster, val); });
        bgmSlider?.onValueChanged.AddListener(val => { ApplyVolume("BGMParam", val); PlayerPrefs.SetFloat(KeyBGM, val); });
        sfxSlider?.onValueChanged.AddListener(val => { ApplyVolume("SFXParam", val); PlayerPrefs.SetFloat(KeySFX, val); });
        micSlider?.onValueChanged.AddListener(val => { ApplyVolume("MicParam", val); PlayerPrefs.SetFloat(KeyMic, val); });
        sensitivitySlider?.onValueChanged.AddListener(val => { PlayerPrefs.SetFloat(KeySens, val); });
    }

    /// <summary>
    /// 오디오 믹서의 파라미터에 볼륨 값을 적용한다. (0~1 값을 데시벨로 변환)
    /// </summary>
    private void ApplyVolume(string paramName, float value)
    {
        if (mainMixer == null) return;

        // 로그 계산을 통해 인간의 청감 특성에 맞는 데시벨(dB)로 변환 (-80dB ~ 20dB)
        float db = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
        mainMixer.SetFloat(paramName, db);
    }

    /// <summary>
    /// 설정창을 닫거나 종료할 때 저장소에 데이터 영구 기록
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.Save();
        Debug.Log("<color=yellow>[시스템]</color> 모든 설정 데이터가 저장소에 기록되었습니다.");
    }
}