using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단일 스피커로 여러 AudioEventSO 카트리지를 교체하며 재생하는 범용 다중 채널 모터.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MultiAudioTrigger : MonoBehaviour
{
    [Serializable]
    public struct AudioMapping
    {
        public SoundType soundType;         // 신호 타입
        public AudioEventSO audioEvent;     // 꽂아넣을 카트리지
    }

    [Header("사운드 맵 (카트리지 보관함)")]
    [SerializeField] private List<AudioMapping> soundMappings = new();

    [Header("3D 사운드 거리 설정")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;

    private AudioSource _source;

    // 🛠️ 빠른 검색을 위한 해시테이블 캐싱
    private readonly Dictionary<SoundType, AudioEventSO> _soundDict = new();

    private void Awake()
    {
        _source = GetComponent<AudioSource>();

        _source.spatialBlend = 1f;
        _source.playOnAwake = false;
        _source.minDistance = minDistance;
        _source.maxDistance = maxDistance;

        // 인스펙터 리스트를 딕셔너리로 압축 조립
        foreach (var mapping in soundMappings)
        {
            if (mapping.audioEvent != null)
                _soundDict[mapping.soundType] = mapping.audioEvent;
        }
    }

    /// <summary>
    /// 외부에서 신호(Enum)를 주면 해당하는 카트리지를 찾아 스피커로 출력
    /// </summary>
    public void PlaySound(SoundType type)
    {
        if (_soundDict.TryGetValue(type, out AudioEventSO eventSO))
        {
            eventSO.Play(_source);
        }
    }

    public void StopSound()
    {
        if (_source != null && _source.isPlaying)
            _source.Stop();
    }
    /// <summary>
    /// 에디터 씬(Scene) 뷰에서 오디오 도달 거리를 홀로그램으로 출력하는 장치
    /// (오브젝트를 클릭했을 때만 보임)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 1. Min Distance (소리가 100%로 들리는 핵심 구역) - 붉은색
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minDistance);

        // 2. Max Distance (소리가 점점 줄어들다 완전히 사라지는 한계선) - 청록색
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}