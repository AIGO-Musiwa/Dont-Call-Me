using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카트리지를 넘겨받아 특정 좌표에서 랜더링후 스피커를 자동 회수
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("최대 풀 수")]
    public int poolSize = 20; // 풀에서 관리할 최대 사운드 수
    [Tooltip("빈 오브젝트에 AudioSurce만 붙은 프리팹")]
    public GameObject audioSourcePrefab; // 사운드 재생에 사용할 AudioSource 프리팹

    private Queue<AudioSource> soundPool = new();

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(audioSourcePrefab, transform);
            obj.SetActive(false);

            // 미리 3D 사운드 세팅을 해둠
            AudioSource source = obj.GetComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.playOnAwake = false;

            soundPool.Enqueue(source);
        }
    }

    /// <summary>
    /// 카트리지(SO)와 3D 좌표를 넘겨받아 스피커를 할당하고 소리를 렌더링함
    /// </summary>
    public void PlaySoundAtPosition(AudioEventSO cartridge, Vector3 position)
    {
        if (cartridge == null || soundPool.Count == 0) return;

        // 1. 대기 중인 스피커 부품 인출
        AudioSource source = soundPool.Dequeue();

        // 2. 3D 좌표 동기화 및 활성화
        source.transform.position = position;
        source.gameObject.SetActive(true);

        // 3. 카트리지 재생 명령 하달 (클립 길이 반환받음)
        float clipLength = cartridge.Play(source);

        // 4. 클립의 재생이 끝난 후 스피커 반납 회로 작동
        StartCoroutine(ReturnToPoolAfterPlay(source, clipLength));
    }
    private IEnumerator ReturnToPoolAfterPlay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.gameObject.SetActive(false);
        soundPool.Enqueue(source);
    }
}
