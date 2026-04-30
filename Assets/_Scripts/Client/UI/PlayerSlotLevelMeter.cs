using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotLevelMeter : MonoBehaviour
{
    [Header("게이지")]
    [SerializeField] private Image levelBar;

    [Header("게이지 설정")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private int sampleSize = 256;

    private AudioSource targetAudioSource;
    private float currentFill = 0f;
    private float[] samples;

    private bool isLocalPlayer = false;
    
    private void Awake()
    {
        samples = new float[sampleSize];

        if (levelBar != null)
        {
            levelBar.type = Image.Type.Filled;
            levelBar.fillMethod = Image.FillMethod.Horizontal;
            levelBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            levelBar.fillAmount = 0f;
        }
    }

    private void Update()
    {
        if (levelBar == null) return;

        float target = 0f;

        if (isLocalPlayer)
        {
            // 본인은 Recorder.LevelMeter 사용
            float amp = VoiceManager.Instance?.LocalRecorder?.LevelMeter?.CurrentAvgAmp ?? 0f;
            target = Mathf.Clamp01(amp * 10f);
        }
        else if ( targetAudioSource != null && targetAudioSource.isPlaying)
        {
            targetAudioSource.GetOutputData(samples, 0);

            float sum = 0f;
            foreach (var s in samples) sum += s * s;
            float rms = Mathf.Sqrt(sum / sampleSize);

            // rms -> 0 ~ 1 범위로 정규화
            target = Mathf.Clamp01(rms * 10f);
        }

        currentFill = Mathf.Lerp(currentFill, target, Time.deltaTime * smoothSpeed);
        levelBar.fillAmount = currentFill;
    }

    #region 외부 API

    // 레벨 미터 대상 설정
    public void setTarget(PlayerData playerData)
    {
        if (playerData == null)
        {
            targetAudioSource = null;
            currentFill = 0f;
            if (levelBar != null) levelBar.fillAmount = 0f;
            return;
        }
        isLocalPlayer = playerData.HasInputAuthority;
        targetAudioSource = playerData.GetComponent<AudioSource>();
    }

    // 대상 지우기
    public void Clear()
    {
        targetAudioSource = null;
        currentFill = 0f;
        if (levelBar != null) levelBar.fillAmount = 0f;
    }

    #endregion
}
