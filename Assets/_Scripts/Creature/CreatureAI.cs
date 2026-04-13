using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class CreatureAI : NetworkBehaviour
{
    [Header("현 상태 및 타겟")]
    [Networked] public CreatureState currentState { get; set; }
    public Transform player;

    [Header("소속 구역 설정")]
    public Zone myZone;

    [Header("층별 순찰 지점 (Waypoints)")]
    public Transform[] waypoints1F;
    public Transform[] waypoints2F;
    public Transform[] waypoints3F;

    //크리처가 현재 순찰 중인 층의 웨이포인트 목록을 저장하는 변수
    private List<Transform> allWaypoints = new List<Transform>();
    private int currentWaypointIndex = 0;
    public float patrolSpeed = 3.5f;

    [Header("탐색 설정")]
    public float searchDuration = 3f;
    public float searchSweepAngle = 120f;
    public float searchSweepSpeed = 4f;
    private float currentSearchTime = 0f;

    [Header("추적 설정")]
    public float chaseSpeed = 6.5f;

    [Header("포획 설정")]
    public float captureDistance = 1.5f;
    public Transform playerRespawnPoint;
    public Transform creatureRespawnPoint1F;
    public Transform creatureRespawnPoint3F;
    private float captureTimer = 0f;
    private bool isTeleportDone = false;

    [Header("Creature 시야(LoS) 설정")]
    public float normalSightDistance = 7.0f;
    public float chaseSightDistance = 12.0f;
    public float normalFieldOfView = 120f;
    public float chaseFieldOfView = 160f;

    private float currentSightDistance;
    private float currentFieldOfView;
    public float eyeHeight = 1.4f;
    public LayerMask obstaclMask;

    [Header("소리 설정")]
    public float alertThresholdDB = 40f;
    public float chaseThresholdDb = 70f;
    public float dbDropPerMeter = 2f;

    private NavMeshAgent agent;
    private Vector3 soundLocation;
    private Vector3 lastKnownPosition;

    #region Fusion용 Spawned함수
    public override void Spawned()
    {
        //agent 컴포넌트 초기화
        agent = GetComponent<NavMeshAgent>();
        player = null;

        if (Object.HasStateAuthority)
        {
            //초기 상태 설정
            currentState = CreatureState.Patrol;
            agent.enabled = true;
            agent.speed = patrolSpeed;
        }
        else agent.enabled = false;

        //기본 시야로 초기화
        SetNormalSight();
    }
    #endregion

    #region Fusion용 FixedUpdateNetwork 함수
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (allWaypoints.Count == 0) return;

        if (currentState == CreatureState.Capture)
        {
            UpdateCapture();
            return;
        }

        //씬에 있는 모든 플레이어를 찾아 가져옴
        PlayerController[] allPlayer = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        //모든 플레이어 중 가장 가깝고 조건에 맞는 플레이어를 찾음
        foreach (PlayerController p in allPlayer)
        {
            //크리쳐 담당 구역과 플레이어 소속 구역이 다르면 무시
            if (p.NetZone != this.myZone) continue;

            //플레이어가 죽었거나 탈출했거나 포획 중이거나 캐비닛에 숨어 있으면 무시
            if (p.NetPlayerState != PlayerState.Normal || p.NetHideState != HideState.None) continue;

            //평면 거리 계산
            Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatPlayerPos = new Vector3(p.transform.position.x, 0, p.transform.position.z);
            float currentFlatDistance = Vector3.Distance(flatCreaturePos, flatPlayerPos);

            //높이 차이 계산
            float yDiff = Mathf.Abs(transform.position.y - p.transform.position.y);

            //거리가 가깝고 같은 층일 때 포획 발동
            if (currentFlatDistance <= captureDistance && yDiff < 2.0f && currentState != CreatureState.Capture)
            {
                player = p.transform;

                //PlayerController 포획 함수 호출
                p.ServerEnterCaptured(playerRespawnPoint.position, playerRespawnPoint.rotation);

                currentState = CreatureState.Capture;
                captureTimer = 0f;
                isTeleportDone = false;

                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;

                //포획 발생 시 해당 구역 조명 관리자에게 암전 명령 전달
                ZoneLightingManager myZoneLightManager = ZoneLightingManager.GetManager(myZone);
                if (myZoneLightManager != null) myZoneLightManager.SetCaptureDarkout(true);

                return;
            }
        }

        //이미 추적 상태인 경우를 제외하고 시야 체크
        if (currentState != CreatureState.Chaser)
        {
            foreach (PlayerController p in allPlayer)
            {
                if (p.NetZone != this.myZone) continue;
                if (p.NetPlayerState != PlayerState.Normal || p.NetHideState != HideState.None) continue;

                //시야에 보인 플레이어 체크
                if (CheckLineOfSight(p.transform))
                {
                    player = p.transform;
                    currentState = CreatureState.Chaser;

                    //추격 시 시야 거리 및 각도 증가
                    SetChaseSight();
                    agent.speed = chaseSpeed;
                    lastKnownPosition = player.position;
                    break;
                }
            }
        }

        //현재 상태에 따라 행동을 결정
        switch (currentState)
        {
            case CreatureState.Patrol: UpdatePatrol(); break;
            case CreatureState.AlerMove: UpdateAlertMove(); break;
            case CreatureState.Search: UpdateSearch(); break;
            case CreatureState.Chaser: UpdateChaser(); break;
        }
    }
    #endregion

    #region Creature 무전기 및 시야 감지 로직
    public void OnHearRadioSound(Vector3 noisePosition, float rawDb, bool isGlobal)
    {
        float perceivedDb = rawDb;
        if (!isGlobal) perceivedDb -= (Vector3.Distance(transform.position, noisePosition) * dbDropPerMeter);

        if (perceivedDb >= chaseThresholdDb)
        {
            currentState = CreatureState.Chaser;

            //큰 소리를 듣고 추격할 때도 시야 증가
            SetChaseSight();

            agent.speed = chaseSpeed;
            lastKnownPosition = noisePosition;
        }
        else if (perceivedDb >= alertThresholdDB && currentState != CreatureState.Chaser)
        {
            currentState = CreatureState.AlerMove;
            soundLocation = noisePosition;
            agent.speed = patrolSpeed * 1.5f;
            agent.SetDestination(soundLocation);
        }
    }

    private void SetNormalSight()
    {
        currentSightDistance = normalSightDistance;
        currentFieldOfView = normalFieldOfView;
    }

    private void SetChaseSight()
    {
        currentSightDistance = chaseSightDistance;
        currentFieldOfView = chaseFieldOfView;
    }

    bool CheckLineOfSight(Transform target)
    {
        //target null 체크
        if (target == null) return false;

        //층간 시야 차단 로직
        //Y축 높이 차이가 4.5m 이상 나면 다른 층으로 간주하고 시야 검사를 생략
        float yDiff = Mathf.Abs(transform.position.y - target.position.y);
        if (yDiff > 4.5f) return false;

        Vector3 directionToPlayer = (target.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        //플레이어가 시야 거리 내에 있는지 확인
        if (distanceToPlayer <= currentSightDistance)
        {
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle <= currentFieldOfView / 2f)
            {
                Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
                Vector3[] targetPoints = {
                    target.position + Vector3.up * 1.6f,
                    target.position + Vector3.up * 1.0f,
                    target.position + Vector3.up * 0.2f
                };

                foreach (Vector3 targetPlayer in targetPoints)
                {
                    Vector3 dirtoTarget = (targetPlayer - eyePosition).normalized;

                    //레이캐스트가 장애물에 부딪히지 않으면 시야에 보인다고 판정
                    if (!Physics.Raycast(eyePosition, dirtoTarget, distanceToPlayer, obstaclMask)) return true;
                }
            }
        }
        return false;
    }
    #endregion

    #region Creature 행동 업데이트 로직
    public void InitializeAllWaypoints()
    {
        allWaypoints.Clear();
        if (waypoints1F != null) allWaypoints.AddRange(waypoints1F);
        if (waypoints2F != null) allWaypoints.AddRange(waypoints2F);
        if (waypoints3F != null) allWaypoints.AddRange(waypoints3F);
    }

    private void UpdatePatrol()
    {
        if (!agent.isOnNavMesh) return;
        agent.speed = patrolSpeed;
        if (allWaypoints.Count <= 1 || agent.pathPending) return;

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            int nextIndex = currentWaypointIndex;
            while (nextIndex == currentWaypointIndex) nextIndex = Random.Range(0, allWaypoints.Count);
            currentWaypointIndex = nextIndex;
            agent.SetDestination(allWaypoints[currentWaypointIndex].position);
        }
    }

    private void UpdateAlertMove()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentState = CreatureState.Search;
            currentSearchTime = 0f;
        }
    }

    private void UpdateSearch()
    {
        currentSearchTime += Runner.DeltaTime;

        //좌우로 두리번거리는 스캔 움직임
        float turnAmount = Mathf.Cos(currentSearchTime * searchSweepSpeed) * searchSweepAngle * Runner.DeltaTime;
        transform.Rotate(Vector3.up * turnAmount);

        if (currentSearchTime >= searchDuration)
        {
            SetNormalSight();
            currentState = CreatureState.Patrol;
            currentSearchTime = 0f;
        }
    }

    private void UpdateChaser()
    {
        if (player != null && CheckLineOfSight(player))
        {
            lastKnownPosition = player.position;
            agent.SetDestination(lastKnownPosition);
        }
        else
        {
            agent.SetDestination(lastKnownPosition);
            if (agent.pathPending || !agent.hasPath) return;
            if (agent.remainingDistance < 0.5f)
            {
                currentState = CreatureState.Search;
                currentSearchTime = 0f;
                agent.speed = patrolSpeed;

                //놓쳤으므로 null로 Player 초기화
                player = null;
            }
        }
    }

    private void UpdateCapture()
    {
        //포획 대상 바라보기
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Runner.DeltaTime * 5f);
        }

        //포획 타이머 계산
        captureTimer += Runner.DeltaTime;
        if (captureTimer >= 2.0f && !isTeleportDone)
        {
            isTeleportDone = true;

            //크리처 리스폰 지점 설정 및 텔레포트 적용
            int currentFloor = GetCurrentFloor();
            Transform targetRespawnPoint = creatureRespawnPoint3F;
            if (currentFloor == 1) targetRespawnPoint = creatureRespawnPoint3F;
            else if (currentFloor == 2) targetRespawnPoint = (Random.value > 0.5f) ? creatureRespawnPoint1F : creatureRespawnPoint3F;
            else if (currentFloor == 3) targetRespawnPoint = creatureRespawnPoint1F;

            //크리처 경로 초기화 및 워프
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;

            agent.Warp(targetRespawnPoint.position);
            agent.nextPosition = targetRespawnPoint.position;
            agent.transform.rotation = targetRespawnPoint.rotation;

            //상태 복구
            agent.isStopped = false;
            currentState = CreatureState.Patrol;
            SetNormalSight();

            //포획 처리가 끝나고 순찰로 복귀할 때 조명 다시 켜기 명령 전달
            ZoneLightingManager myZoneLightManager = ZoneLightingManager.GetManager(myZone);
            if (myZoneLightManager != null) myZoneLightManager.SetCaptureDarkout(false);
        }
    }

    private int GetCurrentFloor()
    {
        //내 높이 계산
        float myY = transform.position.y;
        float dist1 = waypoints1F != null && waypoints1F.Length > 0 ? Mathf.Abs(myY - waypoints1F[0].position.y) : float.MaxValue;
        float dist2 = waypoints2F != null && waypoints2F.Length > 0 ? Mathf.Abs(myY - waypoints2F[0].position.y) : float.MaxValue;
        float dist3 = waypoints3F != null && waypoints3F.Length > 0 ? Mathf.Abs(myY - waypoints3F[0].position.y) : float.MaxValue;

        //각 층 웨이포인트의 평균 높이와 비교하여 현재 층 반환
        if (dist1 <= dist2 && dist1 <= dist3) return 1;
        if (dist2 <= dist1 && dist2 <= dist3) return 2;
        return 3;
    }
    #endregion
}