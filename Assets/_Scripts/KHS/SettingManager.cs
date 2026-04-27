using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using Photon.Voice.Unity;
using Fusion;

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

    [Header("음성 설정 슬라이더")]
    [SerializeField] private Slider micGainSlider;          // 마이크 게인 (0~2)
    [SerializeField] private Slider globalReceiveSlider;   // 다른 플레이어 전체 수신 볼륨 (0~1)

    [Header("조작 설정 (Controls)")]
    [SerializeField] private Slider sensitivitySlider;

    // 데이터 저장을 위한 고유 키값 (오타 방지)
    private const string KeyMaster = "Vol_Master";
    private const string KeyBGM = "Vol_BGM";
    private const string KeySFX = "Vol_SFX";
    private const string KeySens = "Mouse_Sens";

    // 음성 설정 키 (외부 접근 허용
    public const string KeyMicGain = "Mic_Gain";
    public const string KeyGlobalReceiveVolume = "Voice_GlobalReceive";

    private void Start()
    {
        // 1. 저장된 전압(설정값) 로드 (데이터 기반 로드)
        float vMaster = PlayerPrefs.GetFloat(KeyMaster, 0.8f);
        float vBGM = PlayerPrefs.GetFloat(KeyBGM, 0.7f);
        float vSFX = PlayerPrefs.GetFloat(KeySFX, 1.0f);
        float vSens = PlayerPrefs.GetFloat(KeySens, 1.0f);

        // 음성 설정값 로드
        float vMicGain = PlayerPrefs.GetFloat(KeyMicGain, 1.0f);
        float vGlobalRecv = PlayerPrefs.GetFloat(KeyGlobalReceiveVolume, 1.0f);

        // 2. 계기판(UI)에 현재 값 반영
        if (masterSlider) masterSlider.value = vMaster;
        if (bgmSlider) bgmSlider.value = vBGM;
        if (sfxSlider) sfxSlider.value = vSFX;
        if (sensitivitySlider) sensitivitySlider.value = vSens;

        // 음성 UI 초기값 반영
        if (micGainSlider) micGainSlider.value = vMicGain;
        if (globalReceiveSlider) globalReceiveSlider.value = vGlobalRecv;

        // 3. 실제 시스템 회로에 값 인가
        ApplyVolume(KeyMaster, vMaster);
        ApplyVolume(KeyBGM, vBGM);
        ApplyVolume(KeySFX, vSFX);

        // 음성 시스템 초기값 적용
        ApplyMicGain(vMicGain);
        ApplyGlobalReceiveVolume(vGlobalRecv);

        // 감도는 PlayerController 등에서 이 클래스의 정적 변수나 데이터를 참조하게 하면 좋아.

        // 4. 슬라이더 이벤트 배선 연결 (실시간 조절)
        masterSlider?.onValueChanged.AddListener(val => { ApplyVolume(KeyMaster, val); PlayerPrefs.SetFloat(KeyMaster, val); });
        bgmSlider?.onValueChanged.AddListener(val => { ApplyVolume(KeyBGM, val); PlayerPrefs.SetFloat(KeyBGM, val); });
        sfxSlider?.onValueChanged.AddListener(val => { ApplyVolume(KeySFX, val); PlayerPrefs.SetFloat(KeySFX, val); });
        sensitivitySlider?.onValueChanged.AddListener(val => { PlayerPrefs.SetFloat(KeySens, val); });

        // 음성 슬라이더 이벤트 연결
        micGainSlider?.onValueChanged.AddListener(val => { ApplyMicGain(val); PlayerPrefs.SetFloat(KeyMicGain, val); });
        globalReceiveSlider?.onValueChanged.AddListener(val => { ApplyGlobalReceiveVolume(val); PlayerPrefs.SetFloat(KeyGlobalReceiveVolume, val); });
    
        // Runner 생성 시점에 WebRtcAudioDsp 주입
        if (GameLauncher.Instance != null)
            GameLauncher.Instance.OnRunnerCreated += OnRunnerCreated;   
    }

    private void OnDestroy()
    {
        if (GameLauncher.Instance != null)
            GameLauncher.Instance.OnRunnerCreated -= OnRunnerCreated;
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

    // 마이크 볼륨 적용
    private void ApplyMicGain(float value)
    {
        VoiceManager.Instance?.SetMicGain(value);
    }

    private void ApplyGlobalReceiveVolume(float value)
    {
        VoiceManager.Instance?.SetGlobalReceiveVolume(value);
    }

    // WebRtc DSP 초기화
    public void OnRunnerCreated(NetworkRunner runner)
    {
        var webRtcDsp = runner.GetComponent<WebRtcAudioDsp>();

        if (webRtcDsp == null)
            webRtcDsp = FindAnyObjectByType<WebRtcAudioDsp>();

        if (webRtcDsp == null)
        {
            Debug.LogWarning("[SettingsManager] WebRtcAudioDsp를 찾지 못했습니다.");
            return;
        }

        webRtcDsp.NoiseSuppression = true;      // 노이즈 억제
        webRtcDsp.AEC = true;                   // 에코 억제
        webRtcDsp.HighPass = true;              // 저주파 잡음 제거
        webRtcDsp.AGC = false;                  // 자동 볼륨 조절

        Debug.Log("[SettingsManager] WebRtcAudioDsp 초기화 완료 (NS: ON, AEC: ON, AGC: OFF)");
    }

    /// <summary>
    /// 설정창을 닫거나 종료할 때 저장소에 데이터 영구 기록
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.Save();
        Debug.Log("<color=yellow>[시스템]</color> 모든 설정 데이터가 저장소에 기록되었습니다.");
    }

    // 설정창 끄기 / 닫기
    public void ToggleSettingPanel()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}