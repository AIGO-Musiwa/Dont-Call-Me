using System;
using UnityEngine;

/// <summary>
/// [기공사 튜닝 버전] 
/// 다중 채널 지원 및 네트워크 프록시 환경에 최적화된 오디오 모듈
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UniversalAudioModule : MonoBehaviour
{
    [Serializable]
    public struct SoundEntry
    {
        public string soundID;
        public AudioEventSO cartridge;
    }

    [Header("채널 설정")]
    [Tooltip("기본 효과음/음성용 (중요한 소리)")]
    public AudioSource mainSource;
    [Tooltip("발소리/단발성 루프용 (자주 끊기는 소리)")]
    public AudioSource subSource;

    [Header("3D 거리 감쇄 설정")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Header("라이브러리")]
    public SoundEntry[] soundLibrary;
    public AudioEventSO footstepCartridge;

    private void Awake()
    {
        // 소스 자동 할당 및 세팅
        if (mainSource == null) mainSource = GetComponent<AudioSource>();

        // 서브 소스가 없다면 자동으로 하나 더 생성해서 부착 (발소리용)
        if (subSource == null) subSource = gameObject.AddComponent<AudioSource>();

        SetupSource(mainSource);
        SetupSource(subSource);
    }

    private void SetupSource(AudioSource source)
    {
        if (source == null) return;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.playOnAwake = false;
    }

    /// <summary>
    /// 일반 효과음 재생 (메인 채널 사용)
    /// </summary>
    public void PlaySoundByID(string targetID)
    {
        var entry = Array.Find(soundLibrary, s => s.soundID == targetID);
        if (entry.cartridge != null)
        {
            entry.cartridge.Play(mainSource);
        }
    }

    /// <summary>
    /// 발소리 재생 (서브 채널 사용 - 메인 소리를 끊지 않음)
    /// </summary>
    public void PlayFootstep()
    {
        footstepCartridge?.Play(subSource);
    }

    /// <summary>
    /// 애니메이션 이벤트 등에서 인덱스로 직접 재생
    /// </summary>
    /// <param name="index"></param>
    public void PlaySoundByIndex(int index)
    {
        if (index >= 0 && index < soundLibrary.Length)
        {
            soundLibrary[index].cartridge?.Play(mainSource);
        }
    }
}