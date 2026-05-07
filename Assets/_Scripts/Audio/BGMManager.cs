using UnityEngine;

/// <summary>
/// 전역 BGM(배경음악)을 관리하는 중앙 방송국.
/// (효과음은 각 퍼즐 오브젝트의 자체 AudioSource가 담당함)
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("BGM 제어 유닛")]
    [SerializeField] private AudioSource bgmSource;

    private AudioEventSO _currentBgmCartridge; // 현재 장착된 BGM 카트리지 기억

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // BGM 소스 자동 세팅 (2D 사운드)
            if (bgmSource == null)
                bgmSource = gameObject.AddComponent<AudioSource>();

            bgmSource.spatialBlend = 0f; // BGM은 공간감이 없는 2D
            bgmSource.loop = true;       // 무한 반복 설정
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //// <summary>
    /// 전역 BGM 재생 (AudioEventSO 카트리지 사용)
    /// </summary>
    public void PlayBGM(AudioEventSO bgmCartridge, bool isLoop = true)
    {
        // 🛠️ [핵심 개조 포인트] 빈 카트리지가 들어오면 스피커 전원을 아예 꺼버림!
        if (bgmCartridge == null)
        {
            StopBGM();
            _currentBgmCartridge = null; // 메모리 초기화
            return;
        }

        // 중복 재생 방지
        if (bgmSource.isPlaying && _currentBgmCartridge == bgmCartridge) return;

        bgmSource.Stop();
        _currentBgmCartridge = bgmCartridge;

        // 카트리지 성격에 맞춰 스피커의 루프 모드를 껐다 켰다 함!
        bgmSource.loop = isLoop;

        bgmCartridge.Play(bgmSource);
    }

    /// <summary>
    /// 전역 BGM 정지
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }
}