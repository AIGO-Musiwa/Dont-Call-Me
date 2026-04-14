using Fusion;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CreatureMotor))]
[RequireComponent(typeof(CreatureSensor))]
public class CreatureAI : NetworkBehaviour
{
    [Header("상태 및 구역")]
    [Networked] public CreatureState currentState { get; set; }
    public Zone myZone;
    public Transform playerTarget;

    [Header("이동 속도 설정")]
    public float moveSpeedBase = 4.0f;
    public float alertMoveMultiplier = 1.5f;
    public float searchMultiplier = 1.0f;
    public float chaseMultiplier = 1.75f;

    //프레젠터 스크립트 연동을 위한 실제 계산된 속도 저장 변수
    [HideInInspector] public float patrolSpeed;
    [HideInInspector] public float alertMoveSpeed;
    [HideInInspector] public float searchSpeed;
    [HideInInspector] public float chaseSpeed;

    [Header("상태 전환 타이머 설정")]
    public float searchDuration = 10.0f;
    public float lockOnBreakTime = 3.0f;
    public float chaseMinDuration = 3.0f;

    [Header("수색 상세 설정")]
    public float searchRadius = 5.0f;
    public float searchLookAroundTime = 6.0f;

    [Header("포획 및 리스폰 설정")]
    public Transform playerRespawnPoint;
    public Transform creatureRespawnPoint1F;
    public Transform creatureRespawnPoint3F;

    private CreatureMotor motor;
    private CreatureSensor sensor;

    private Vector3 targetLocation;
    private float stateTimer = 0f;
    private float losLostTimer = 0f;
    private bool isCapturing = false;

    //수색 상태 전용 변수
    private Vector3 searchCenter;
    private int searchPhaseStep = 0;
    private float overallSearchTimer = 0f;

    #region 초기화 및 기본 설정
    public override void Spawned()
    {
        //모터 및 센서 컴포넌트 초기화
        motor = GetComponent<CreatureMotor>();
        sensor = GetComponent<CreatureSensor>();

        //모터 웨이포인트 초기화
        motor.Initialize();

        //실제 이동 속도 사전 계산
        CalculateActualSpeeds();

        //상태 권한이 있는 서버에서 초기 상태 설정
        if (Object.HasStateAuthority)
        {
            //초기 상태 설정
            currentState = CreatureState.Patrol;
            
            //서버(호스트) 측 길찾기 에이전트 켜기
            motor.EnableAgent(true);
            
            //순찰 속도 설정
            motor.SetSpeed(patrolSpeed);
        }

        //클라이언트(프록시) 측 길찾기 에이전트 끄기
        else motor.EnableAgent(false);        
    }

    //기본 속도에 배율을 곱해 실제 속도값 캐싱
    private void CalculateActualSpeeds()
    {        
        patrolSpeed = moveSpeedBase;
        alertMoveSpeed = moveSpeedBase * alertMoveMultiplier;
        searchSpeed = moveSpeedBase * searchMultiplier;
        chaseSpeed = moveSpeedBase * chaseMultiplier;
    }
    #endregion

    #region 메인 AI 루프
    public override void FixedUpdateNetwork()
    {
        //상태 권한 확인
        if (!Object.HasStateAuthority) return;

        //포획 상태일 경우 포획 로직만 실행
        if (currentState == CreatureState.Capture)
        {
            UpdateCaptureState();
            return;
        }

        //주변 감지 및 상태 우선순위 판정
        UpdateSensingAndPriorities();

        //현재 상태에 따라 행동 결정
        switch (currentState)
        {
            case CreatureState.Patrol: UpdatePatrolState(); break;
            case CreatureState.AlerMove: UpdateAlertMoveState(); break;
            case CreatureState.Search: UpdateSearchState(); break;
            case CreatureState.Chaser: UpdateChaseState(); break;
        }
    }
    #endregion

    #region 상황 판단 및 감지
    private void UpdateSensingAndPriorities()
    {
        //총 수색 시간이 끝났다면 순찰 상태로 복귀
        if (searchPhaseStep > 0)
        {
            overallSearchTimer += Runner.DeltaTime;

            if (overallSearchTimer >= searchDuration)
            {
                EndSearchPhase();

                //수색이 끝났으므로 이번 프레임의 다른 판단은 스킵하고 바로 순찰로 넘김
                return;
            }
        }

        //씬에 있는 모든 플레이어를 찾아 가져옴
        PlayerController[] allPlayer = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        //시야 확보 여부 플래그
        bool hasLoS = false;

        //모든 플레이어 중 조건에 맞는 플레이어 탐색
        foreach (PlayerController p in allPlayer)
        {
            //크리처 담당 구역과 플레이어 소속 구역이 다르면 무시
            if (p.NetZone != this.myZone) continue;

            //플레이어가 정상 상태가 아니거나 숨어 있으면 무시
            if (p.NetPlayerState != PlayerState.Normal || p.NetHideState != HideState.None) continue;

            //현재 추적 중인 타겟에 대한 포획 조건 확인
            if (currentState == CreatureState.Chaser && playerTarget == p.transform)
            {
                //포획 조건 만족 시 포획 실행
                if (sensor.CheckCaptureCondition(p.transform))
                {
                    ExecuteCapture(p);
                    return;
                }
            }

            //시야에 보인 플레이어 확인
            if (sensor.CheckLineOfSight(p.transform))
            {
                //시야 확보 플래그 활성화
                hasLoS = true;
                searchPhaseStep = 0;

                //타겟 설정 및 타겟 위치 저장
                playerTarget = p.transform;
                targetLocation = playerTarget.position;

                //추적 상태가 아닐 경우 추적 상태로 전환
                if (currentState != CreatureState.Chaser)
                {
                    currentState = CreatureState.Chaser;

                    //추적 속도로 변경
                    motor.SetSpeed(chaseSpeed);

                    //무전 코스트 초기화
                    sensor.ResetWalkieCost();
                }
                break;
            }
        }

        //추적 상태일 때의 로직
        if (currentState == CreatureState.Chaser)
        {
            //시야에 타겟이 보일 경우
            if (hasLoS)
            {
                //시야 상실 타이머 초기화 및 타겟 위치 갱신
                losLostTimer = 0f;
                targetLocation = playerTarget.position;
            }

            //시야에서 타겟이 사라졌을 경우
            else
            {
                //시야 상실 타이머 증가
                losLostTimer += Runner.DeltaTime;

                //시야 차단 유지 시간 초과 시 수색 상태로 전환
                if (losLostTimer >= lockOnBreakTime)
                {
                    currentState = CreatureState.Search;

                    //수색 상태 초기화
                    stateTimer = 0f;
                    overallSearchTimer = 0f;
                    searchPhaseStep = 1;
                    searchCenter = targetLocation;

                    //타겟 초기화 및 수색 속도로 변경
                    playerTarget = null;
                    motor.StopMoving();
                }
            }
        }

        //순찰 중 무전 코스트 임계치 도달 시 경계 이동 상태로 전환
        if (currentState == CreatureState.Patrol && sensor.IsCostThresholdReached())
        {
            currentState = CreatureState.AlerMove;
            searchPhaseStep = 0;
            stateTimer = 0f;

            //경계 이동 속도로 변경
            motor.SetSpeed(alertMoveSpeed);

            //무전 코스트 초기화
            sensor.ResetWalkieCost();
        }
    }

    public void OnHearRadioSound(Vector3 noisePosition, float rawDb, bool isGlobal)
    {
        //센서를 통해 거리 감쇠가 적용된 소리 크기 계산
        float perceivedDb = sensor.CalculatePerceivedDb(noisePosition, rawDb, isGlobal);

        //기본 소리 임계치 설정
        float alertDbThreshold = sensor.alertThresholdDB;
        float criticalDbThreshold = sensor.criticalThresholdDB;

        //해당 구역 조명 관리자 확인
        ZoneLightingManager manager = ZoneLightingManager.GetManager(myZone);

        //삼막 활성화 시 소리 임계치 절반으로 감소시켜 예민도 증가
        if (manager != null && manager.IsAct3Active)
        {
            alertDbThreshold *= 0.5f;
            criticalDbThreshold *= 0.5f;
        }

        //치명적 소리 임계치 초과 시 즉시 추적 상태로 전환
        if (perceivedDb >= criticalDbThreshold)
        {
            currentState = CreatureState.Chaser;
            searchPhaseStep = 0;
            stateTimer = 0f;

            //추적 속도로 변경 및 타겟 위치 설정
            motor.SetSpeed(chaseSpeed);
            targetLocation = noisePosition;

            //무전 코스트 초기화
            sensor.ResetWalkieCost();
        }

        //경계 소리 임계치 초과 시 경계 이동 상태로 전환
        else if (perceivedDb >= alertDbThreshold && currentState != CreatureState.Chaser)
        {
            currentState = CreatureState.AlerMove;
            searchPhaseStep = 0;
            targetLocation = noisePosition;

            //경계 이동 속도로 변경 및 타겟 위치로 이동
            motor.SetSpeed(alertMoveSpeed);
            motor.MoveToDestination(targetLocation);
        }
    }
    #endregion

    #region 상태별 행동 제어 (FSM)
    private void UpdatePatrolState()
    {
        //순찰 속도 설정
        motor.SetSpeed(patrolSpeed);

        //모터의 순찰 로직 실행
        motor.UpdatePatrolLogic();
    }

    private void UpdateAlertMoveState()
    {
        //NavMesh의 경로 계산 딜레이로 인한 즉시 도착 판정 버그 방지
        stateTimer += Runner.DeltaTime;
        
        //목표 위치 도달 시 수색 상태로 전환
        if (stateTimer > 0.2f && motor.HasReachedDestination())
        {
            if (searchPhaseStep == 0)
            {
                //최초 소리 근원지에 도착 후 제자리 탐색 시작
                currentState = CreatureState.Search;
                stateTimer = 0f;
                motor.StopMoving();

                searchPhaseStep = 1;
                overallSearchTimer = 0f;
                searchCenter = transform.position;
            }
            else if (searchPhaseStep == 2)
            {
                //주변 1회 이동 후 랜덤 지점에 도착하면 해당 지점에서 1회 추가 탐색
                currentState = CreatureState.Search;
                stateTimer = 0f;
                motor.StopMoving();

                searchPhaseStep = 3;
            }
        }
    }

    private void UpdateSearchState()
    {
        //수색 타이머 증가 및 하위 행동 타이머 증가
        stateTimer += Runner.DeltaTime;        
        
        //설정된 시간 동안 탐색 후 주변 수색 이동 시작
        if (searchPhaseStep == 1 && stateTimer >= searchLookAroundTime)
        {
            currentState = CreatureState.AlerMove;
            stateTimer = 0f;
            searchPhaseStep = 2;
            motor.SetSpeed(alertMoveSpeed);

            //벽 너머가 아닌 도달 가능한 무작위 위치 탐색 후 이동
            Vector3 randomDest = GetValidSearchPoint(searchCenter, searchRadius);
            motor.MoveToDestination(randomDest);
        }

        //랜덤 지역 추가 탐색 후 순착 복귀
        else if (searchPhaseStep == 3 && stateTimer >= searchLookAroundTime) EndSearchPhase();        
    }

    //수색 페이즈를 종료하고 순찰로 복귀
    private void EndSearchPhase()
    {
        searchPhaseStep = 0;
        overallSearchTimer = 0f;
        currentState = CreatureState.Patrol;
        stateTimer = 0f;

        motor.StopMoving();
    }

    private Vector3 GetValidSearchPoint(Vector3 center, float radius)
    {
        for (int i = 0; i < 10; i++)
        {
            //반경 내의 무작위 평면 좌표 생성
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomPoint = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            //해당 좌표 근처에 네브메시가 있는지 확인
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 2.0f, NavMesh.AllAreas))
            {
                //벽을 뚫지 않기 위해 실제로 걸어서 갈 수 있는 경로인지 계산하여 검증
                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path))
                {
                    if (path.status == NavMeshPathStatus.PathComplete) return hit.position;
                }
            }
        }

        //유요한 위치를 찾지 못하면 제자리 유지
        return center;
    }

    private void UpdateChaseState()
    {
        //모터를 통해 타겟 위치로 이동
        motor.MoveToDestination(targetLocation);
    }

    private void ExecuteCapture(PlayerController target)
    {
        //플레이어 컨트롤러의 포획 함수 호출
        target.ServerEnterCaptured(playerRespawnPoint.position, playerRespawnPoint.rotation);

        //포획 상태로 전환 및 포획 중 플래그 활성화
        currentState = CreatureState.Capture;
        isCapturing = true;
        stateTimer = 0f;

        //모터 이동 중지
        motor.StopMoving();

        //해당 구역 조명 관리자에게 암전 명령 전달
        ZoneLightingManager myZoneLightManager = ZoneLightingManager.GetManager(myZone);
        if (myZoneLightManager != null) myZoneLightManager.SetCaptureDarkout(true);
    }

    private void UpdateCaptureState()
    {
        //포획 대상 바라보기
        if (playerTarget != null)
        {
            Vector3 direction = (playerTarget.position - transform.position).normalized;
            direction.y = 0f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Runner.DeltaTime * 5f);
        }

        //포획 타이머 증가
        stateTimer += Runner.DeltaTime;

        //포획 연출 시간 종료 시 처리
        if (stateTimer >= 2.0f && isCapturing)
        {
            isCapturing = false;

            //모터를 통해 현재 층수 확인
            int currentFloor = motor.GetCurrentFloor();

            //크리처 리스폰 지점 설정
            Transform targetRespawn = creatureRespawnPoint3F;
            if (currentFloor == 1) targetRespawn = creatureRespawnPoint3F;
            else if (currentFloor == 2) targetRespawn = (Random.value > 0.5f) ? creatureRespawnPoint1F : creatureRespawnPoint3F;
            else if (currentFloor == 3) targetRespawn = creatureRespawnPoint1F;

            //모터를 통해 크리처 워프
            motor.WarpTo(targetRespawn.position, targetRespawn.rotation);

            //상태 복구 및 타겟 초기화
            currentState = CreatureState.Patrol;
            playerTarget = null;

            //조명 관리자에게 암전 해제 명령 전달
            ZoneLightingManager myZoneLightManager = ZoneLightingManager.GetManager(myZone);
            if (myZoneLightManager != null) myZoneLightManager.SetCaptureDarkout(false);
        }
    }

    public void ApplyAct3Multipliers(bool isAct3)
    {
        //삼막 진입 시 배율 적용
        if (isAct3)
        {
            //이동 속도 증가
            patrolSpeed *= 1.25f;
            alertMoveSpeed *= 1.25f;
            searchSpeed *= 1.25f;
            chaseSpeed *= 1.25f;

            //센서 예민도 증가
            sensor.SetActMultiplier(0.5f);
        }
    }
    #endregion
}