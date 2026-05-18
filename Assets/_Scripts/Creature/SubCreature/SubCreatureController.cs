using Fusion;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SubCreatureSensor))]
public class SubCreatureController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public SubCreatureState NetState { get; set; } = SubCreatureState.Inactive;

    [Header("구역")]
    public Zone myZone;

    [Header("타이머")]
    [Tooltip("활성화 후 플레이어 미감지 시 이 시간이 지나면 비활성화.")]
    public float activeDuration = 10f;

    [Tooltip("플레이어 감지 후 기믹을 유지하는 시간.")]
    public float triggerDuration = 5f;

    [Header("호출음 설정")]
    [Tooltip("메인 크리처를 자극하는 호출음 dB")]
    public float callSoundDb = 45f;

    [Tooltip("호출음 발생 주기 (초)")]
    public float callSoundInterval = 2f;

    [Header("메인 크리처 감지 설정")]
    [Tooltip("이 범위 안에 메인 크리처가 들어오면 호출음을 멈춘다.")]
    public float creatureDetectRange = 10f;

    [Tooltip("메인 크리처 감지용 레이어 마스크")]
    public LayerMask creatureLayerMask;

    [Tooltip("층간 차단 레이어 마스크")]
    public LayerMask wallLayerMask;

    // ── 컴포넌트 참조 ─────────────────────────────────────

    private SubCreatureSensor sensor;

    // ── 내부 상태 (호스트 전용) ───────────────────────────

    private float stateTimer = 0f;
    private float callTimer = 0f;
    private bool callStopped = false;

    private readonly List<PlayerDebuffHandler> slowedPlayers = new();
    private NetworkId sourceId;

    #region 초기화

    public override void Spawned()
    {
        sensor = GetComponent<SubCreatureSensor>();
        sourceId = Object.Id;

        if (HasStateAuthority)
        {
            NetState = SubCreatureState.Inactive;
            stateTimer = 0f;
        }
    }

    #endregion

    #region 외부 API (Spawner 전용)

    // Spawner가 호출. Inactive → Active 전환
    public void Activate()
    {
        if (!HasStateAuthority) return;
        if (NetState != SubCreatureState.Inactive) return;

        NetState = SubCreatureState.Active;
        stateTimer = 0f;
        callTimer = 0f;
        callStopped = false;

        Debug.Log($"[SubCreatureController] {myZone} → Active");
    }

    // Spawner가 강제 비활성화 시 호출
    public void Deactivate()
    {
        if (!HasStateAuthority) return;

        ClearGimmick();
        NetState = SubCreatureState.Inactive;
        stateTimer = 0f;
    }

    #endregion

    #region 메인 루프

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        stateTimer += Runner.DeltaTime;

        switch (NetState)
        {
            case SubCreatureState.Active: UpdateActive(); break;
            case SubCreatureState.Triggered: UpdateTriggered(); break;
        }
    }

    #endregion

    #region FSM 상태별 처리

    private void UpdateActive()
    {
        // 플레이어 감지 시 Triggered 전환
        if (sensor.HasPlayerInRange())
        {
            EnterTriggered();
            return;
        }

        // activeDuration 초과 → 비활성화
        if (stateTimer >= activeDuration)
        {
            NetState = SubCreatureState.Inactive;
            stateTimer = 0f;
            SecurityCameraCreatureManager.Instance?.OnCameraDeactivated(this);
            Debug.Log($"[SubCreatureController] {myZone} → Inactive (시간 초과)");
        }
    }

    private void UpdateTriggered()
    {
        UpdateCreatureDetect();
        UpdateSlowRange();
        UpdateCallSound(Runner.DeltaTime);

        // triggerDuration 초과 → 비활성화
        if (stateTimer >= triggerDuration)
        {
            ClearGimmick();
            NetState = SubCreatureState.Inactive;
            stateTimer = 0f;
            SecurityCameraCreatureManager.Instance?.OnCameraDeactivated(this);
            Debug.Log($"[SubCreatureController] {myZone} → Inactive (트리거 종료)");
        }
    }

    private void EnterTriggered()
    {
        NetState = SubCreatureState.Triggered;
        stateTimer = 0f;
        callTimer = 0f;
        callStopped = false;

        Debug.Log($"[SubCreatureController] {myZone} → Triggered");
    }

    #endregion

    #region 기믹 처리

    private void UpdateSlowRange()
    {
        List<PlayerController> inRange = sensor.GetPlayersInRange();

        foreach (PlayerController pc in inRange)
        {
            PlayerDebuffHandler debuff = pc.GetComponent<PlayerDebuffHandler>();
            if (debuff == null) continue;

            debuff.ApplySlowDebuff(sourceId);

            if (!slowedPlayers.Contains(debuff))
                slowedPlayers.Add(debuff);
        }

        for (int i = slowedPlayers.Count - 1; i >= 0; i--)
        {
            if (slowedPlayers[i] == null) { slowedPlayers.RemoveAt(i); continue; }

            PlayerController pc = slowedPlayers[i].GetComponent<PlayerController>();
            if (!sensor.IsPlayerInRange(pc))
            {
                slowedPlayers[i].RemoveSlowDebuffBySource(sourceId);
                slowedPlayers.RemoveAt(i);
            }
        }
    }

    private void UpdateCreatureDetect()
    {
        if (callStopped) return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            creatureDetectRange,
            creatureLayerMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider col in hits)
        {
            CreatureAI ai = col.GetComponent<CreatureAI>();
            if (ai == null) continue;
            if (ai.myZone != myZone) continue;

            Vector3 dir = ai.transform.position - transform.position;
            float dist = dir.magnitude;
            if (wallLayerMask.value != 0 &&
                Physics.Raycast(transform.position, dir.normalized, dist, wallLayerMask, QueryTriggerInteraction.Ignore))
                continue;

            callStopped = true;
            Debug.Log($"[SubCreatureController] 메인 크리처 감지 → 호출음 중단 ({myZone})");
            break;
        }
    }

    private void UpdateCallSound(float deltaTime)
    {
        if (callStopped) return;

        callTimer += deltaTime;
        if (callTimer < callSoundInterval) return;

        callTimer = 0f;

        SoundEmitter.EmitToEventBus(
            SoundChannel.Natural,
            callSoundDb,
            transform.position,
            0f,
            myZone);
    }

    private void ClearGimmick()
    {
        foreach (PlayerDebuffHandler debuff in slowedPlayers)
        {
            if (debuff != null)
                debuff.RemoveSlowDebuffBySource(sourceId);
        }
        slowedPlayers.Clear();
    }

    #endregion

    #region OnChangedRender

    private void OnStateChanged()
    {
        // TODO: 카메라 활성/비활성 시각 피드백 (표시등 색상 등) 연결 가능
        Debug.Log($"[SubCreatureController] 상태 → {NetState}");
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new UnityEngine.Color(1f, 0.5f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, creatureDetectRange);
        UnityEditor.Handles.color = new UnityEngine.Color(1f, 0.5f, 0f, 1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, creatureDetectRange);
    }
#endif
}
