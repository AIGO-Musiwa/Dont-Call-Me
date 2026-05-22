using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MicCalibrationUI : MonoBehaviour
{
    // ── 캘리브레이션 진행 중 여부 (외부 접근용) ──────────
    public static bool IsCalibrating { get; private set; } = false;
 
    // ── 마이크 장치 선택 ──────────────────────────────────
    [Header("마이크 장치 선택")]
    [SerializeField] private TMP_Dropdown micDeviceDropdown;

    // ── 수동 게인 슬라이더 ────────────────────────────────
    [Header("수동 게인 슬라이더 (0~2)")]
    [SerializeField] private Slider micGainSlider;
    [SerializeField] private TextMeshProUGUI micGainLabel;

    // ── 자동 캘리브레이션 ─────────────────────────────────
    [Header("자동 캘리브레이션")]
    [SerializeField] private Button autoCalibButton;
    [SerializeField] private TextMeshProUGUI autoCalibStatusText;   // 상태 안내 텍스트
    [SerializeField] private TextMeshProUGUI calibSentenceText;     // 읽을 문장 표시 택스트
    [SerializeField] private CalibrationScriptSO calibScript;       // 랜덤 문장 SO
    [SerializeField] private float readTime = 2f;                   // 측정 대기 시간
    [SerializeField] private float calibDuration = 5f;              // 측정 시간

    // ── 입력 차단 패널 ────────────────────────────────────
    [Header("입력 차단 패널 (투명 Raycast Target 패널)")]
    [SerializeField] private GameObject blockPanel;

    // ── 레벨 UI 참조 ─────────────────────────────────────
    [Header("레벨 UI")]
    [SerializeField] private MicLevelUI micLevelUI;

    // ── 자동 캘리브레이션 목표 dBFS ───────────────────────
    private const float calibTargetdBFS = -37.5f;   // 속삭임 구간 중간

    #region Unity Lifecycle

    private void OnEnable()
    {
        InitMicDeviceDropdown();
        LoadSavedGain();

        if (calibSentenceText != null)
            calibSentenceText.gameObject.SetActive(false);

        if (SceneManager.GetActiveScene().buildIndex == SceneNames.GAME_INDEX)
            autoCalibButton.interactable = false;

        // 패널 초기 비활성화
        SetBlockPanel(false);
    }

    private void OnDisable()
    {
        // 패널 켜진 채로 비활성화되는 경우 방지
        SetBlockPanel(false);
        IsCalibrating = false;
    }

    #endregion

    #region 초기화

    private void InitMicDeviceDropdown()
    {
        if (micDeviceDropdown == null) return;

        micDeviceDropdown.ClearOptions();

        var devices = Microphone.devices;
        if (devices.Length == 0)
        {
            micDeviceDropdown.AddOptions(new List<string> { "마이크 없음" });
            micDeviceDropdown.interactable = false;
            return;
        }

        micDeviceDropdown.AddOptions(new List<string>(devices));
        micDeviceDropdown.interactable = true;

        string currentDevice = VoiceManager.Instance?.LocalRecorder?.MicrophoneDevice.Name 
            ?? PlayerPrefs.GetString("Mic_Device", devices[0]);

        for (int i = 0; i< devices.Length; i++)
        {
            if (devices[i] == currentDevice)
            {
                micDeviceDropdown.SetValueWithoutNotify(i);
                break;
            }
        }

        micDeviceDropdown.onValueChanged.RemoveAllListeners();
        micDeviceDropdown.onValueChanged.AddListener(OnMicDeviceChanged);
    }

    private void LoadSavedGain()
    {
        if (micGainSlider == null) return;

        float saved = PlayerPrefs.GetFloat(Constants.KEY_MIC_GAIN, 1f);
        micGainSlider.minValue = 0f;
        micGainSlider.maxValue = 3f;
        micGainSlider.value = saved;

        UpdateGainLabel(saved);

        micGainSlider.onValueChanged.RemoveAllListeners();
        micGainSlider.onValueChanged.AddListener(OnGainSlideerChanged);

        autoCalibButton?.onClick.RemoveAllListeners();
        autoCalibButton?.onClick.AddListener(OnAutoCalibButtonClicked);
    }
    
    #endregion

    #region 마이크 장치 변경

    private void OnMicDeviceChanged(int index)
    {
        var devices = Microphone.devices;
        if (index < 0 || index >= devices.Length) return;

        string selectedDevice = devices[index];
        PlayerPrefs.SetString("Mic_Device", selectedDevice);

        var recorder = VoiceManager.Instance?.LocalRecorder;
        if (recorder != null)
        {
            // 로비/인게임: Recorder에 즉시 적용 (자동으로 RestartRecording 호출됨)
            recorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(selectedDevice);
            Debug.Log($"[MicCalibrationUI] 마이크 장치 변경 → {selectedDevice}");
        }
        else
        {
            // 타이틀: MicLevelUI의 Microphone 재시작
            micLevelUI?.RestartTitleMicrophone(selectedDevice);
            Debug.Log($"[MicCalibrationUI] 타이틀 마이크 장치 변경 → {selectedDevice}");
        }
    }

    #endregion

    #region 수동 게인 슬라이더

    private void OnGainSlideerChanged(float value)
    {
        ApplyGain(value);
        UpdateGainLabel(value);
        PlayerPrefs.SetFloat(Constants.KEY_MIC_GAIN, value);
    }

    private void UpdateGainLabel(float value)
    {
        if (micGainLabel != null)
            micGainLabel.text = $"{value:F2}x";
    }

    #endregion

    #region 자동 캘리브레이션
    
    private void OnAutoCalibButtonClicked()
    {
        if (IsCalibrating) return;
        StartCoroutine(AutoCalibRoutine());
    }

    // 자동 측정 시작
    private IEnumerator AutoCalibRoutine()
    {
        if (micLevelUI == null)
        {
            Debug.LogWarning("[MicCalibrationUI] MicLevelUI가 연결되지 않았습니다.");
            yield break;
        }

        IsCalibrating = true;
        SetBlockPanel(true);

        // 랜덤 문장 표시
        string sentence = calibScript != null ? calibScript.GetRandom() : "마이크에 대고 조용히 말해주세요.";
        ShowSentence(sentence);
        SetStatusText($"{readTime}초 후, 아래 문장을 조용히 속삭이듯 읽어주세요.");

        yield return new WaitForSeconds(readTime);

        // 원본 신호 기준으로 측정 시작
        ApplyGain(1.5f);

        float elapsed = 0f;
        float sum = 0f;
        int count = 0;

        while (elapsed < calibDuration)
        {
            elapsed += Time.deltaTime;

            float raw = micLevelUI.GetCurrentRawdBFS();
            if (raw > -96f)
            {
                sum += raw;
                count++;
            }

            SetStatusText($"측정 중... ({calibDuration - elapsed:F1}초)");
            yield return null;
        }

        // 결과 적용
        HideSentence();

        if (count == 0)
        {
            SetStatusText("마이크 입력이 없습니다. 다시 시도해주세요.");
        }
        else
        {
            float avgdBFS = sum / count;
            float gainDelta = Mathf.Pow(10f, (calibTargetdBFS / avgdBFS) / 20f);
            float newGain = Mathf.Clamp(gainDelta, 0f, 3f);

            ApplyGain(newGain);
            micGainSlider.value = newGain;
            UpdateGainLabel(newGain);
            PlayerPrefs.SetFloat(Constants.KEY_MIC_GAIN, newGain);

            SetStatusText($"설정 완료! 게인: {newGain:F2}x (평균: {avgdBFS:F1} dBFS)");
            Debug.Log($"[MicCalibrationUI] 캘리브레이션 완료 → avgdBFS={avgdBFS:F1}, newGain={newGain:F2}");
        }

        IsCalibrating = false;
        SetBlockPanel(false);
    }

    private void ShowSentence(string sentence)
    {
        if (calibSentenceText == null) return;
        calibSentenceText.text = sentence;
        calibSentenceText.gameObject.SetActive(true);
    }

    private void HideSentence()
    {
        if (calibSentenceText == null) return;
        calibSentenceText.gameObject.SetActive(false);
    }

    private void SetStatusText(string msg)
    {
        if (autoCalibStatusText != null)
            autoCalibStatusText.text = msg;
    }

    #endregion

    #region 내부 유틸
    
    // 입력 차단 패널
    private void SetBlockPanel(bool active)
    {
        if (blockPanel != null)
            blockPanel.SetActive(active);
    }

    private void ApplyGain(float gain)
    {
        VoiceManager.Instance?.SetMicGain(gain);
    }

    #endregion
}
