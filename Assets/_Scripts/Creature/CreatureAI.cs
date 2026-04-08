using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class CreatureAI : NetworkBehaviour
{
    [Header("현 상태 및 타겟")]
    [Networked] public CreatureState currentState { get; set; }
    public Transform player;

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
    //public float searchRotationSpeed = 360f;
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

    //현재 적용 중인 시야 스펙
    private float currentSightDistance;
    private float currentFieldOfView;
    public float eyeHeight = 1.4f;
    public LayerMask obstaclMask;

    [Header("무전기 소리 설정")]
    public float alertThresholdDB = 40f;
    public float chaseThresholdDb = 70f;
    public float dbDropPerMeter = 2f;

    private NavMeshAgent agent;
    private Vector3 soundLocation;
    private Vector3 lastKnownPosition;

    [Header("조명 관리")]
    public Light[] managedLights;
    private float[] originalLightIntensities;

    #region Fusion용 Spawned 및 Render 함수
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

        //할당된 조명들의 원래 밝기 저장
        if (managedLights != null && managedLights.Length > 0)
        {
            originalLightIntensities = new float[managedLights.Length];
            for (int i = 0; i < managedLights.Length; i++)
            {
                if (managedLights[i] != null) originalLightIntensities[i] = managedLights[i].intensity;
            }
        }

        //PlayerController를 찾아 타겟 할당
        if (player == null)
        {
            PlayerController foundPlayerScript = FindAnyObjectByType<PlayerController>();
            if (foundPlayerScript != null) player = foundPlayerScript.transform;
        }

        //기본 시야로 초기화
        SetNormalSight();
    }

    public override void Render()
    {
        if (managedLights == null || managedLights.Length == 0) return;

        //상태에 따라 조명 밝기 동기화 처리
        for (int i = 0; i < managedLights.Length; i++)
        {
            if (managedLights[i] == null) continue;

            if (currentState == CreatureState.Capture)
            {
                managedLights[i].intensity = Mathf.Lerp(managedLights[i].intensity, 0f, Time.deltaTime * 5f);
            }
            else
            {
                managedLights[i].intensity = Mathf.Lerp(managedLights[i].intensity, originalLightIntensities[i], Time.deltaTime * 2f);
            }
        }
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

        //플레이어가 없으면 씬에서 다시 찾아 할당
        if (player == null)
        {
            PlayerController foundPlayerScript = FindAnyObjectByType<PlayerController>();
            if (foundPlayerScript != null && foundPlayerScript.gameObject.scene.IsValid()) player = foundPlayerScript.transform;
        }

        //플레이어를 찾은 상태인 경우 거리 계산 및 시야 체크
        if (player != null)
        {
            //평면 거리 계산
            Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatPlayerPos = new Vector3(player.position.x, 0, player.position.z);
            float currentFlatDistance = Vector3.Distance(flatCreaturePos, flatPlayerPos);

            //높이 차이 계산
            float yDiff = Mathf.Abs(transform.position.y - player.position.y);

            //거리가 가깝고 같은 층일 때 포획 발동
            if (currentFlatDistance <= captureDistance && yDiff < 2.0f && currentState != CreatureState.Capture)
            {
                Debug.Log($"크리처: 잡았다. (평면 거리: {currentFlatDistance:F2}m, 높이 차이: {yDiff:F2}m)");
                currentState = CreatureState.Capture;
                captureTimer = 0f;
                isTeleportDone = false;

                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                return;
            }

            //이미 추적 상태인 경우를 제외하고 시야 체크
            if (currentState != CreatureState.Chaser)
            {
                if (CheckLineOfSight())
                {
                    currentState = CreatureState.Chaser;
                    agent.speed = chaseSpeed;
                    lastKnownPosition = player.position;
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

    bool CheckLineOfSight()
    {
        //player null 체크
        if (player == null) return false;
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        //플레이어가 시야 거리 내에 있는지 확인
        if (distanceToPlayer <= currentSightDistance)
        {
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle <= currentFieldOfView / 2f)
            {
                Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
                Vector3[] targetPoints = { player.position + Vector3.up * 1.6f, player.position + Vector3.up * 1.0f, player.position + Vector3.up * 0.2f };

                foreach (Vector3 target in targetPoints)
                {
                    Vector3 dirtoTarget = (target - eyePosition).normalized;
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

        //한 바퀴 돌면서 두리번거리는 스캔 움직임
        //transform.Rotate(Vector3.up * searchRotationSpeed * Runner.DeltaTime);
        
        if (currentSearchTime >= searchDuration)
        {
            SetNormalSight();
            currentState = CreatureState.Patrol;
            currentSearchTime = 0f;
        }
    }

    private void UpdateChaser()
    {
        if (CheckLineOfSight())
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

            //플레이어 텔레포트 적용
            PlayerKCCMotor playerMotor = player.GetComponentInParent<PlayerKCCMotor>();
            if (playerMotor == null) playerMotor = player.GetComponentInChildren<PlayerKCCMotor>();

            if (playerMotor != null && playerMotor.KCC != null)
            {
                playerMotor.KCC.SetPosition(playerRespawnPoint.position);
                playerMotor.KCC.SetLookRotation(playerRespawnPoint.rotation.eulerAngles.x, playerRespawnPoint.rotation.eulerAngles.y);
                playerMotor.transform.position = playerRespawnPoint.position;
                playerMotor.transform.rotation = playerRespawnPoint.rotation;
            }

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