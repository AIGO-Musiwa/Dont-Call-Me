using System;
using UnityEngine;

// 크리처 AI로 전달되는 소리 이벤트 데이터
public struct SoundEvent
{
    public SoundChannel channel;
    public float voicedB;           // 발생원 dB (감쇠 전)
    public Vector3 sourcePosition;  // 소리 발생 위치
    public float obstaclePenaltydB; // 자연음 채널만 사용
    public Zone sourceZone;         // 소리 발생 구역
}

// 소리 이벤트 버스
public static class SoundEventBus
{
    // 소리 이벤트 발행 시 크리처 AI가 구독하여 감지 판정 수행
    public static event Action<SoundEvent> OnSoundEmitted;

    public static void Emit(SoundEvent soundEvent)
    {
        OnSoundEmitted?.Invoke(soundEvent);
    }
}