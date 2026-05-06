using UnityEngine;

/// <summary>
/// UI 효과음을 전담하는 2D 스피커 배전반.
/// </summary>
public class UIAudioManager : MonoBehaviour
{
    public static UIAudioManager Instance { get; private set; }

    [Header("UI 전용 스피커")]
    [SerializeField] private AudioSource uiSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 넘어가도 UI 소리 담당 유지

            if (uiSource == null)
                uiSource = gameObject.AddComponent<AudioSource>();

            uiSource.spatialBlend = 0f; // UI 사운드이므로 완벽한 2D
            uiSource.playOnAwake = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// UI 효과음 카트리지를 받아 재생
    /// </summary>
    public void PlayUISound(AudioEventSO soundCartridge)
    {
        if (soundCartridge != null && uiSource != null)
        {
            soundCartridge.Play(uiSource);
        }
    }
}