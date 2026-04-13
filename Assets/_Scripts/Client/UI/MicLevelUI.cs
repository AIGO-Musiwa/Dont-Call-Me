using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MicLevelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image micGauge;
    [SerializeField] private TextMeshProUGUI dBLabel;       // db 수치 텍스트

    [Header("게이지 색상")]
    [SerializeField] private Color colorSafe = Color.green;
    [SerializeField] private Color colorAlert = Color.yellow;
    [SerializeField] private Color colorCritical = Color.red;

    //[Header("dB 구간 기준")]
    //[SerializeField] private float dBAlert = 18f;
    //[SerializeField] private float dBCritical = 28f;
    // 최대 dB
    //float maxdB = 43f;

    [Header("게이지 dBFS 범위 (낮출수록 작은 소리에도 반응)")]
    [Tooltip("이 dBFS 이하는 게이지 0 (기본 -60: 주변 소음 무시)")]
    [SerializeField] private float minDBFS = -60f;
    [Tooltip("이 dBFS 이상은 게이지 최대 (기본 -10: 고함 수준)")]
    [SerializeField] private float maxDBFS = -10f;

    [Header("보간 속도")]
    [SerializeField] private float smoothSpeed = 10f;

    private float currentFill = 0f;

    private void Start()
    {
        // 씬 시작 시 Safe 색상으로 초기화 → 빨간색 시작 방지
        if (micGauge != null)
        {
            micGauge.fillAmount = 0f;
            micGauge.color = colorSafe;
        }

        if (dBLabel != null)
            dBLabel.text = "-- dB";
    }

    private void Update()
    {
        if (micGauge == null) return;
        if (SounddBMeasurer.Instance == null)
        {
            currentFill = Mathf.Lerp(currentFill, 0f, Time.deltaTime * smoothSpeed);
            micGauge.fillAmount = currentFill;
            micGauge.color = colorSafe;
            if (dBLabel != null) dBLabel.text = "-- dB";
            return;
        }

        float rawdBFS = SounddBMeasurer.Instance.CurrentRawdBFS;
        float target = Mathf.InverseLerp(minDBFS, maxDBFS, rawdBFS); // minDBFS~maxDBFS → 0~1

        // 부드럽게 보간
        currentFill = Mathf.Lerp(currentFill, target, Time.deltaTime * smoothSpeed);

        micGauge.fillAmount = currentFill;

        micGauge.color = GetGaugeColor(rawdBFS);

        // dB 수치 표시
        if (dBLabel != null)
            dBLabel.text = rawdBFS > -96f ? $"{rawdBFS:F1} dBFS" : "-- dBFS";
    }

    // 구간 기준 색상 반환
    private Color GetGaugeColor (float dB)
    {
        if (dB > -20f) return colorCritical;  // 고함
        if (dB > -30f) return colorAlert;     // 큰 목소리
        return colorSafe;                        // 일반 이하
    }
}
