using UnityEngine;

/// <summary>
/// 독립형 사운드 출력 유닛
/// 어떤 오브젝트든 이 컴포넌트만 붙이면 사운드 출력 기능을 갖게 됨.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SimpleAudioTrigger : MonoBehaviour
{
    [Header("사운드 카트리지")]
    [SerializeField] private AudioEventSO soundCartridge;

    [Header("3D 사운드 거리 설정")]
    [SerializeField] private float minDistance = 1f;    // 이 거리 내에서는 최대 음량
    [SerializeField] private float maxDistance = 20f;   // 이 거리 밖에서는 소리가 들리지 않음

    private AudioSource _source;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();

        // 3D 물리 사운드 기본 세팅
        _source.spatialBlend = 1f;
        _source.playOnAwake = false;

        _source.minDistance = minDistance;
        _source.maxDistance = maxDistance;

        // 믹서 배선이 안 되어 있을 때를 대비한 안전 장치
        if (_source.outputAudioMixerGroup == null)
        {
            // 필요시 기본 SFX 믹서를 찾아 연결하는 로직을 넣을 수도 있음.
        }
    }

    /// <summary>
    /// 외부에서 이 포트를 찌르면 소리가 납니다.
    /// </summary>
    public void Play()
    {
        if (soundCartridge != null && _source != null)
        {
            soundCartridge.Play(_source);
        }
    }
    // 🛠️ [추가된 부품] 외부에서 소리를 강제로 차단합니다.
    public void Stop()
    {
        if (_source != null && _source.isPlaying)
        {
            _source.Stop();
        }
    }
}