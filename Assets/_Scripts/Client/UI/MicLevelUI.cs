using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MicLevelUI : MonoBehaviour
{
    [Header("방향 설정")]
    [SerializeField] private GaugeDirection direction = GaugeDirection.Horizontal;

    [Header("UI")]
    [SerializeField] private Image levelBar;

    [Header("구간 마커 (levelBar의 자식 오브젝트")]
    [SerializeField] private RectTransform markerWhisper;
    [SerializeField] private RectTransform markerNormal;
    [SerializeField] private RectTransform markerLoud;

    [Header("게이지 색상")]
    [SerializeField] private Color colorSafe = Color.green;
    [SerializeField] private Color colorAlert = Color.yellow;
    [SerializeField] private Color colorCritical = Color.red;

    [Header("게이지 dBFS 범위 (낮출수록 작은 소리에도 반응)")]
    [Tooltip("이 dBFS 이하는 게이지 0 (기본 -60: 주변 소음 무시)")]
    [SerializeField] private float minDBFS = -60f;
    [Tooltip("이 dBFS 이상은 게이지 최대 (기본 -10: 고함 수준)")]
    [SerializeField] private float maxDBFS = -10f;

    [Header("보간 속도")]
    [SerializeField] private float smoothSpeed = 10f;

    // dBFS 구간 기준
    private const float dBFS_Whisper = -45f;
    private const float dBFS_Normal = -30f;
    private const float dBFS_Loud = -20f;

    // 타이틀 Microphone 직접 읽기
    private AudioClip titleMicClip;
    private string titleMicDevice;
    private const int sampleWindow = 1024;

    private float currentFill = 0f;
    private bool markerPositionSet = false;

    private const float silenceThreshold = 0.001f;

    private void OnEnable()
    {
        // 타이틀에서만 Microphone 직접 열기
        if (IsInTitleScene())
            StartTitleMicrophone();
    }

    private void OnDisable()
    {
        StopTitleMicrophone();
    }

    private void Start()
    {
        // 씬 시작 시 Safe 색상으로 초기화 → 빨간색 시작 방지
        if (levelBar == null) return;

        levelBar.fillAmount = 0f;
        levelBar.color = colorSafe;
        levelBar.type = Image.Type.Filled;

        // 방향에 따라 fillMethod 설정
        if (direction == GaugeDirection.Horizontal)
        {
            levelBar.fillMethod = Image.FillMethod.Horizontal;
            levelBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        else
        {
            levelBar.fillMethod = Image.FillMethod.Vertical;
            levelBar.fillOrigin = (int)Image.OriginVertical.Bottom;
        }
    }

    private void LateUpdate()
    {
        if (!markerPositionSet)
            TrySetMarkerPositions();

        UpdateLevelBar();
    }

    #region 타이틀 Microphone 직접 읽기

    private bool IsInTitleScene()
    {
        return SceneManager.GetActiveScene().buildIndex == SceneNames.TITLE_INDEX;
    }

    private void StartTitleMicrophone()
    {
        if (Microphone.devices.Length == 0) return;

        titleMicDevice = PlayerPrefs.GetString("Mic_Device", Microphone.devices[0]);

        bool deviceExists = false;
        foreach (var d in Microphone.devices)
        {
            if (d == titleMicDevice)
            {
                deviceExists = true;
                break;
            }
        }
        if (!deviceExists) titleMicDevice = Microphone.devices[0];

        titleMicClip = Microphone.Start(titleMicDevice, true, 1, 44100);
        Debug.Log($"[MicLevelUI] 타이틀 마이크 시작 → {titleMicDevice}");
    }

    // 타이틀 Microphone 종료
    public void StopTitleMicrophone()
    {
        if (titleMicClip == null) return;
        Microphone.End(titleMicDevice);
        titleMicClip = null;
        Debug.Log("[MicLevelUI] 타이틀 마이크 종료");
    }

    // 타이틀에서 마이크 장치 변경 시 재시작
    public void RestartTitleMicrophone(string deviceName)
    {
        if (!IsInTitleScene()) return;
        StopTitleMicrophone();
        titleMicDevice = deviceName;
        titleMicClip = Microphone.Start(titleMicDevice, true, 1, 44100);
        Debug.Log($"[MicLevelUI] 타이틀 마이크 재시작 → {titleMicDevice}");
    }

    private float GetRawdBFSFromMicrophone()
    {
        if (titleMicClip == null) return -96f;

        int micPosition = Microphone.GetPosition(titleMicDevice);
        if (micPosition < sampleWindow) return -96f;

        float[] samples = new float[sampleWindow];
        titleMicClip.GetData(samples, micPosition - sampleWindow);

        float sum = 0f;
        foreach (var s in samples) sum += s * s;
        float rms = Mathf.Sqrt(sum / sampleWindow);

        // 무음 판정
        if (rms < silenceThreshold) return -96f;

        float gain = PlayerPrefs.GetFloat(Constants.KEY_MIC_GAIN, 1f);
        rms = Mathf.Clamp(rms * gain, 0f, 1f);

        return 20f * Mathf.Log10(rms);
    }

    #endregion

    #region 마커 위치 설정
    private void TrySetMarkerPositions()
    {
        if (levelBar == null) return;

        var rt = (RectTransform)levelBar.transform;
        float size = direction == GaugeDirection.Horizontal ? rt.rect.width : rt.rect.height;

        if (size <= 0f) return;

        SetMarkerPosition(markerWhisper, dBFS_Whisper, size);
        SetMarkerPosition(markerNormal, dBFS_Normal, size);
        SetMarkerPosition(markerLoud, dBFS_Loud, size);

        markerPositionSet = true;
    }

    private void SetMarkerPosition(RectTransform marker, float dBFS, float size)
    {
        if (marker == null) return;
        float t = Mathf.InverseLerp(minDBFS, maxDBFS, dBFS);

        if (direction == GaugeDirection.Horizontal)
        {
            // 가로 게이지: 마커는 수직선 -> X 위치 이동, Rotation 0
            marker.anchoredPosition = new Vector2(t * size, marker.anchoredPosition.y);
            marker.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        else
        {
            // 세로 게이지: 마커는 수평선 -> Y 위치 이동, Rotation 90
            marker.anchoredPosition = new Vector2(marker.anchoredPosition.x, t * size);
            marker.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    #endregion

    #region 레벨 게이지

    private void UpdateLevelBar()
    {
        if (levelBar == null) return;

        float rawdBFS = GetCurrentRawdBFS();

        if (rawdBFS <= -96f)
        {
            currentFill = Mathf.Lerp(currentFill, 0f, Time.deltaTime * smoothSpeed);
            levelBar.fillAmount = currentFill;
            levelBar.color = colorSafe;
            return;
        }

        float target = Mathf.InverseLerp(minDBFS, maxDBFS, rawdBFS); // minDBFS~maxDBFS → 0~1

        // 부드럽게 보간
        currentFill = Mathf.Lerp(currentFill, target, Time.deltaTime * smoothSpeed);

        levelBar.fillAmount = currentFill;
        levelBar.color = GetGaugeColor(rawdBFS);
    }

    #endregion

    #region 외부 API
    public float GetCurrentRawdBFS()
    {
        // 인게임
        if (MicrophonedBMeasurer.Instance != null)
            return MicrophonedBMeasurer.Instance.CurrentRawdBFS;

        // 대기실
        if (VoiceManager.Instance?.LocalRecorder?.LevelMeter != null)
        {
            float amp = VoiceManager.Instance?.LocalRecorder?.LevelMeter?.CurrentAvgAmp ?? 0f;
            if (amp <= 0f) return -96f;
            return 20f * Mathf.Log10(amp);
        }

        // 타이틀
        return GetRawdBFSFromMicrophone();
    }

    // 구간 기준 색상 반환
    private Color GetGaugeColor (float dB)
    {
        if (dB > -20f) return colorCritical;  // 고함
        if (dB > -30f) return colorAlert;     // 큰 목소리
        return colorSafe;                        // 일반 이하
    }

    #endregion
}
