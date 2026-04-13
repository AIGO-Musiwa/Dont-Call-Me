using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 클립 데이터와 재생 튜닝 값을 하나로 묶어 관리
/// </summary>
[CreateAssetMenu(fileName = "NewAudioEvent", menuName = "AudioSO/Audio Cartridge")]
public class AudioEventSO : ScriptableObject
{
    [Header("사운드 에셋 (배열 셔플 지원)")]
    public AudioClip[] clips;

    [Header("믹서")]
    [Tooltip("AudioSettingsController의 제어를 받기 위해 SFX 믹서 그룹을 연결")]
    public AudioMixerGroup mixerGroup;

    [Header("출력 튜닝")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 2f)] public float pitchMin = 0.9f;
    [Range(0.1f, 2f)] public float pitchMax = 1.1f;

    /// <summary>
    /// AudioSource를 넘겨받아 소리를 출력하고 클립의 길이를 반환함
    /// </summary>
    public float Play(AudioSource source)
    {
        if (clips == null || clips.Length == 0 || source == null) return 0f;

        AudioClip selectedClip = clips[Random.Range(0, clips.Length)];

        source.outputAudioMixerGroup = mixerGroup; // 믹서 채널 연결
        source.clip = selectedClip;
        source.volume = volume;
        source.pitch = Random.Range(pitchMin, pitchMax);

        source.Play();

        // 타이머를 맞추기 위해 길이를 반환
        return selectedClip.length;
    }
}
