using Fusion;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SubCreatureSensor))]
public class SubCreatureController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public SubCreatureState NetState { get; set; } = SubCreatureState.Idle;

    [Header("구역")]
    public Zone myZone;

    [Header("서브 크리처 배치 위치 풀")]
    [Tooltip("Reloacting 시 이 중 하나로 랜덤 텔레포트. 현재 인덱스 제외")]
    public Transform[] relocatePoints;

    [Header("타이머")]
    [Tooltip("플레이어 미감지 상태에서 이 시간이 지나면 Relocating으로 전환")]
    public float maxIdleTime = 30f;

    [Tooltip("기믹 발동 후 이 시간이 지나면 Relocating으로 전환")]
    public float activeDuration = 8f;

    // ── 컴포넌트 참조 ─────────────────────────────────────

    private SubCreatureSensor sensor;
    private ISubCreatureGimmick gimmick;

    // ── 내부 상태 (호스트 전용) ───────────────────────────

    private float stateTimer = 0f;
    public int currentRelocateIndex = -1;

    // 기믹 활성 상태 추적 (Active 중 플레이어 감지 여부에 따라 변경)
    private bool isGimmickActive = false;

    #region 초기화

    public override void Spawned()
    {
        sensor = GetComponent<SubCreatureSensor>();
        gimmick = GetComponent<ISubCreatureGimmick>();

        // 기믹 컴포넌트에 공통 의존성 주입
        InjectGimmickDependencies();

        if (HasStateAuthority)
        {
            NetState = SubCreatureState.Idle;
            stateTimer = 0f;
        }
    }

    // 기믹 종류에 따라 필요한 참조 주입
    // 새 기믹 추가 시 case 추가
    private void InjectGimmickDependencies()
    {
        switch (gimmick)
        {
            case SlowAndCallGimmick slow:
                slow.Setup(sensor, this);
                break;

            case NoiseEnhancerGimmick noise:
                noise.Setup(sensor, this);
                break;
        }
    }

    #endregion

    #region 메인 루프

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        stateTimer += Runner.DeltaTime;

        switch (NetState)
        {
            case SubCreatureState.Idle: UpdateIdle(); break;
            case SubCreatureState.Active: UpdateActive(); break;
            case SubCreatureState.Relocating: UpdateRelocating(); break;
        }
    }

    #endregion

    #region FSM 상태별 처리

    private void UpdateIdle()
    {
        // 머물러 있는 시간 초과 -> 이동
        if (stateTimer >= maxIdleTime)
        {
            EnterRelocating();
            return;
        }

        // 감지 범위 안에 플레이어 진입 -> Active 전환
        if (sensor.HasPlayerInRange())
            EnterActive();
    }

    private void UpdateActive()
    {
        bool hasPlayer = sensor.HasPlayerInRange();

        // 플레이어 감지 상태 변화 시에만 기믹 활성/비활성 전환
        if (hasPlayer && !isGimmickActive)
        {
            gimmick?.OnActivate();
            isGimmickActive = true;
        }
        else if (!hasPlayer && isGimmickActive)
        {
            gimmick?.OnDeactivate();
            isGimmickActive = false;
        }

        // 기믹 활성 중일 때만 Tick
        if (isGimmickActive)
            gimmick?.OnTick(Runner.DeltaTime);

        // 지속 시간 초과 -> Relocating (타이머는 항상 흐름)
        if (stateTimer >= activeDuration)
        {
            if (isGimmickActive)
            {
                gimmick?.OnDeactivate();
                isGimmickActive = false;
            }
            EnterRelocating();
        }
    }

    private void UpdateRelocating()
    {
        // 텔레포트는 EnterRelocating에서, 다음 프레임에서 Idle 복귀
        NetState = SubCreatureState.Idle;
        stateTimer = 0f;
    }

    #endregion

    #region 상태 전환

    private void EnterActive()
    {
        NetState = SubCreatureState.Active;
        stateTimer = 0f;
        isGimmickActive = true;

        gimmick?.OnActivate();

        Debug.Log($"[SubCreatureController] {myZone} → Active");
    }

    private void EnterRelocating()
    {
        NetState = SubCreatureState.Relocating;
        stateTimer = 0f;

        TeleportToNextPoint();

        Debug.Log($"[SubCreatureController] {myZone} → Relocating");
    }

    #endregion

    #region 내부 유틸

    private void TeleportToNextPoint()
    {
        if (relocatePoints == null || relocatePoints.Length == 0) return;

        SubCreatureSpawner spawner = SubCreatureSpawner.Instance;

        int chosen;
        if (spawner != null)
        {
            // 현재 점유 해제
            if (currentRelocateIndex >= 0)
                spawner.ReleasePoint(myZone, currentRelocateIndex);

            // 다른 크리처가 점유하지 않은 포인트 요청
            chosen = spawner.GetAvailablePointIndex(myZone, currentRelocateIndex, this);

            // 빈 포인트가 없으면 이동 포기 (드문 케이스)
            if (chosen < 0)
            {
                Debug.LogWarning($"[SubCreatureController] {myZone} 사용 가능한 포인트 없음. 이동 취소.");
                if (currentRelocateIndex >= 0)
                    spawner.OccupyPoint(myZone, currentRelocateIndex, this);
                return;
            }

            // 새 포인트 점유 등록
            spawner.OccupyPoint(myZone, chosen, this);
        }
        else
        {
            // Spawner 없을 때 폴백: 기존 랜덤 방식
            List<int> candidates = new();
            for (int i = 0; i < relocatePoints.Length; i++)
            {
                if (i != currentRelocateIndex) candidates.Add(i);
            }
            if (candidates.Count == 0) return;
            chosen = candidates[Random.Range(0, candidates.Count)];
        }

        currentRelocateIndex = chosen;

        Transform dest = relocatePoints[chosen];
        transform.SetPositionAndRotation(dest.position, dest.rotation);

        // 물리 엔진에 콜라이더 위치 즉시 반영
        Physics.SyncTransforms();

        sensor.ClearPlayers();

        // 클라이언트 위치 동기화
        RPC_SyncTeleport(dest.position, dest.rotation);
    }

    #endregion

    #region RPC

    // 텔레포트 위치 동기화
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncTeleport(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    #endregion

    private void OnStateChanged()
    {
        // 향후 애니메이션 트리거 연결 가능
        Debug.Log($"[SubCreatureController] 상태 → {NetState}");
    }
}
