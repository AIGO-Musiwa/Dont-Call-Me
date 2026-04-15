using System.Collections.Generic;
using UnityEngine;

// 소리 이벤트 발행 전용 유틸리티
public static class SoundEmitter
{
    private static readonly Dictionary<Zone, CreatureSensor> creatureSensors = new();

    #region 크리처 등록/해제

    public static void RegisterCreature(Zone zone, CreatureSensor sensor)
    {
        creatureSensors[zone] = sensor;
    }

    public static void UnregisterCreature(Zone zone)
    {
        creatureSensors.Remove(zone);
    }

    #endregion

    // 자연음 채널 발행 (플레이어 음성, 발소리 등)
    public static void EmitNatural(float voicedB, Vector3 sourcePosition, Zone sourceZone)
    {
        float penalty = CalculateObstaclePenalty(sourcePosition, sourceZone);

        Debug.Log($"[SoundEmitter] EmitNatural - Zone: {sourceZone}, voicedB: {voicedB}, penalty: {penalty}");

        SoundEventBus.Emit(new SoundEvent
        {
            channel = SoundChannel.Natural,
            voicedB = voicedB,
            sourcePosition = sourcePosition,
            obstaclePenaltydB = penalty
        });
    }

    // 무전음 채널 발행
    public static void EmitWalkie(float voicedB, Vector3 sourcePosition)
    {
        SoundEventBus.Emit(new SoundEvent
        {
            channel = SoundChannel.Walkie,
            voicedB = voicedB,
            sourcePosition = sourcePosition,
            obstaclePenaltydB = 0f
        });
    }

    // ── 편의성을 위한 소리 이벤트 발행 ─────────────────────────

    // 발소리 dB 이벤트 발행
    public static void EmitFootstep(CharacterAudioModule.FootstepType type, Vector3 position, Zone sourceZone)
    {
        float dB = type switch
        {
            CharacterAudioModule.FootstepType.Crouch => 26f,
            CharacterAudioModule.FootstepType.Walk => 31f,
            CharacterAudioModule.FootstepType.Run => 34f,
            _ => 31f
        };

        EmitNatural(dB, position, sourceZone);
    }

    // 라디오 유인음 발행
    public static void EmitRadio(Vector3 radioPosition)
    {
        EmitWalkie(47f, radioPosition);
    }

    // 퍼즐 실패 패널티음 발행
    public static void EmitPuzzleFail(Vector3 puzzlePosition)
    {
        EmitWalkie(49f, puzzlePosition);
    }

    #region 차폐 계산

    private static float CalculateObstaclePenalty(Vector3 sourcePosition, Zone sourceZone)
    {
        if (!creatureSensors.TryGetValue(sourceZone, out CreatureSensor sensor))
        {
            Debug.LogWarning($"[SoundEmitter] Zone {sourceZone}에 등록된 크리처가 없습니다.");
            return 0f;
        }
        if (sensor == null) return 0f;

        Vector3 creaturePos = sensor.transform.position + Vector3.up * sensor.eyeHeight;
        Vector3 direction = creaturePos - sourcePosition;
        float distance = direction.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(sourcePosition, direction.normalized, distance);

        float totalPenalty = 0f;
        foreach (var hit in hits)
        {
            ObstacleData obstacleData = hit.collider.GetComponent<ObstacleData>();
            if (obstacleData != null)
                totalPenalty += obstacleData.GetPenalty();
        }

        return totalPenalty;
    }

    #endregion
}
