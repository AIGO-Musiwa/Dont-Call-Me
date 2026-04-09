using System;
using UnityEngine;

/// <summary>
/// 크리쳐, 플레이어 등 모든 오디오 소스가 이 모듈을 통해 사운드를 재생하도록 하는 범용 오디오 모듈
/// SO 카트리지를 기반으로 작동
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UniversalAudioModule : MonoBehaviour
{
    // 인스펙터에서 사운드 이름과 'SO 카트리지'를 묶어서 관리
    [Serializable]
    public struct SoundEntry
    {
        public string soundID; // 예: "Alert", "Attack", "Jump"
        public AudioEventSO cartridge; // AudioClip 대신 카트리지가 들어감!
    }

    [Header("오디오 소스")]
    public AudioSource audioSource; // 사운드 재생에 사용할 AudioSource 컴포넌트

    [Header("고유 사운드")]
    public SoundEntry[] soundLibrary; // 사운드 이름과 카트리지 매핑 배열

    [Header("물리 기반 사운드 카트리지(발소리 등")]
    public AudioEventSO footstepCartridge; // 발소리 등 물리 기반 사운드용 카트리지

    private void Awake()
    {
        if(audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.spatialBlend = 1f; // 3D 사운드로 설정
            audioSource.playOnAwake = false; // 자동 재생 방지
        }
    }

    /// <summary>
    /// ID(문자열) 기반 사운드 랜더링(코드 호출)
    /// </summary>
    public void PlaySoundByID(string targetID)
    {
        for(int i = 0; i < soundLibrary.Length; i++)
        {
            if(soundLibrary[i].soundID == targetID)
            {
                soundLibrary[i].cartridge.Play(audioSource);
                return;
            }
        }
    }

    /// <summary>
    /// [애니메이션 이벤트용] 인덱스 기반 사운드 랜더링(애니메이션 이벤트는 문자열 전달이 안 되므로 인덱스 사용)
    /// </summary>
    /// <param name="index"></param>
    public void PlaySoundByIndex(int index)
    {
        if (index >= 0 && index < soundLibrary.Length)
        {
            soundLibrary[index].cartridge?.Play(audioSource);
        }
    }

    /// <summary>
    /// 발소리 등 물리 기반 사운드 재생용 메서드 (애니메이션 이벤트에서 호출)
    /// </summary>
    public void PlayFootstep()
    {
        footstepCartridge?.Play(audioSource);
    }
}
