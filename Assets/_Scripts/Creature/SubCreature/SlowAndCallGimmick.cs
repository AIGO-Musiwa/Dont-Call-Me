using Fusion;
using JetBrains.Annotations;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class SlowAndCallGimmick : MonoBehaviour, ISubCreatureGimmick
{
    [Header("이속 감소 설정")]
    [Tooltip("호출음 dB")]
    public float callSounddB = 45f;

    [Tooltip("호출음 발생 주기(초)")]
    public float callSoundInterval = 2f;

    [Header("메인 크리처 감지 설정")]
    [Tooltip("이 범위 안에 메인 크리처가 들어오면 호출음을 멈춘다.")]
    public float creatureDetectRange = 10f;

    [Tooltip("메인 크리처 감지용 레이어 마스크")]
    public LayerMask creatureLayerMask;

    private SubCreatureSensor sensor;
    private SubCreatureController controller;
    private NetworkId sourceId;

    private float callSoundTimer = 0f;

    // 메인 크리처가 감지 범위 안에 있는지 여부
    private bool callStopped = false;

    // Active 중 이속 감소가 적용된 플레이어 추적
    private readonly List<PlayerDebuffHandler> slowedPlayers = new();

    #region 초기화

    public void Setup(SubCreatureSensor sensor, SubCreatureController controller)
    {
        this.sensor = sensor;
        this.controller = controller;
        this.sourceId = controller.Object.Id;
    }

    #endregion

    #region ISubCreatureGimmick 구현

    public void OnActivate()
    {
        callSoundTimer = 0f;
        callStopped = false;
        slowedPlayers.Clear();
        Debug.Log($"[SlowAndCallGimmick] 활성화 ({controller.myZone})");
    }

    public void OnTick(float deltaTime)
    {
        UpdateCreatureDetect();
        UpdateSlowRange();
        UpdateCallSound(deltaTime);
    }

    public void OnDeactivate()
    {
        foreach (PlayerDebuffHandler debuff in slowedPlayers)
        {
            if (debuff != null)
                debuff.RemoveSlowDebuffBySource(sourceId);
        }
        slowedPlayers.Clear();
        Debug.Log($"[SlowAndCallGimmick] 비활성화 ({controller.myZone})");
    }

    #endregion

    #region 내부 유틸

    private void UpdateCreatureDetect()
    {
        if (callStopped) return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            creatureDetectRange,
            creatureLayerMask,
            QueryTriggerInteraction.Ignore
            );

        bool creatureFound = false;
        foreach (Collider col in hits)
        {
            CreatureAI ai = col.GetComponent<CreatureAI>();
            if (ai == null) continue;

            // 같은 구역 메인 크리처만 감지
            if (ai.myZone != controller.myZone) continue;

            creatureFound = true;
            break;
        }

        if (creatureFound != callStopped)
        {
            callStopped = creatureFound;
            Debug.Log($"[SlowAndCallGimmick] 메인 크리처 감지 → 호출음 영구 중단 ({controller.myZone})");
        }
    }

    private void UpdateSlowRange()
    {
        List<PlayerController> inRange = sensor.GetPlayersInRange();

        // 범위 안 플레이어에게 이속 감소 갱신
        foreach (PlayerController pc in inRange)
        {
            PlayerDebuffHandler debuff = pc.GetComponent<PlayerDebuffHandler>();
            if (debuff == null) continue;

            debuff.ApplySlowDebuff(sourceId);

            if (!slowedPlayers.Contains(debuff))
                slowedPlayers.Add(debuff);
        }

        // 범위를 이탈한 플레이어 이속 감소 해제
        for (int i = slowedPlayers.Count - 1; i >= 0; i--)
        {
            if (slowedPlayers[i] == null)
            {
                slowedPlayers.RemoveAt(i);
                continue;
            }

            PlayerController pc = slowedPlayers[i].GetComponent<PlayerController>();
            if (!sensor.IsPlayerInRange(pc))
            {
                slowedPlayers[i].RemoveSlowDebuffBySource(sourceId);
                slowedPlayers.RemoveAt(i);
            }
        }
    }

    // 메인 크리처가 근처에 없을 때만 호출음 발생
    private void UpdateCallSound(float deltaTime)
    {
        // 메인 크리처가 도착했으면 호출음 중단
        if (callStopped) return;

        callSoundTimer += deltaTime;
        if (callSoundTimer < callSoundInterval) return;

        callSoundTimer = 0f;

        SoundEmitter.EmitToEventBus(
            SoundChannel.Natural,
            callSounddB,
            transform.position,
            0f,
            controller.myZone);

        Debug.Log($"[SlowAndCallGimmick] 호출음 발생 ({controller.myZone}) dB={callSounddB}");
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 메인 크리처 감지 범위 (주황)
        UnityEditor.Handles.color = new UnityEngine.Color(1f, 0.5f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, creatureDetectRange);

        UnityEditor.Handles.color = new UnityEngine.Color(1f, 0.5f, 0f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, creatureDetectRange);
    }
#endif
}
