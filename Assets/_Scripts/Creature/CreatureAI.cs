using Fusion;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CreatureMotor))]
[RequireComponent(typeof(CreatureSensor))]
[RequireComponent(typeof(CreatureWalkieTracker))]
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

    [Header("구출 보호 설정")]
    public float rescueProtectTime = 10.0f;
    public float rescueProtectTimer = 0f;

    private CreatureMotor motor;
    private CreatureSensor sensor;
    private CreatureWalkieTracker walkieTracker;
    private NavMeshAgent agent;

    private Vector3 targetLocation;
    private float stateTimer = 0f;
    private float losLostTimer = 0f;
    private bool isCapturing = false;

    //수색 상태 전용 변수
    private SearchPhase currentSearchPhase = SearchPhase.None;
    private Vector3 searchCenter;    
    private float overallSearchTimer = 0f;

    //현재 추적 중인 소리의 정보 기억
    private float currentTrackedDb = 0f;
    private SoundChannel currentTrackedChannel = SoundChannel.Natural;

    #region 초기화 및 기본 설정
    public override void Spawned()
    {
        //컴포넌트 초기화
        motor = GetComponent<CreatureMotor>();
        sensor = GetComponent<CreatureSensor>();
        walkieTracker = GetComponent<CreatureWalkieTracker>();
        agent = GetComponent<NavMeshAgent>();

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

            //호스트(서버) 권한일 때 글로벌 소리 이벤트 구독
            SoundEventBus.OnSoundEmitted += OnSoundEventReceived;
            SoundEmitter.RegisterCreature(myZone, sensor);
        }

        //클라이언트(프록시) 측 길찾기 에이전트 끄기
        else motor.EnableAgent(false);        
    }

    //오브젝트 소멸 시 이벤트 구독 해제
    public override void Despawned(NetworkRunner runner, bool hasState)
    {        
        if (hasState)
        {
            SoundEventBus.OnSoundEmitted -= OnSoundEventReceived;
            SoundEmitter.UnregisterCreature(myZone);
        }
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

        //구출 보호 타이머 감소
        if (rescueProtectTimer > 0f) rescueProtectTimer -= Runner.DeltaTime;

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

    #region 상황 판단 및 감지 (역할별 분리)
    private void UpdateSensingAndPriorities()
    {
        //10초 보호 기간 중에는 시야 및 주변 감지를 모두 무시
        if (rescueProtectTimer > 0f) return;

        //수색 전체 제한 시간이 지났으면 강제 종료 후 순찰로 복귀
        if (CheckAndHanledSearchTimeout()) return;

        //플레이어를 발견했거나 포획 조건을 만족했다면 리턴
        if (DetectAndHandlePlayer()) return;

        //추격 중 시야 상실 여부 관리
        ManageChaseLosLost();

        //순찰 중 무전 코스트 초과 여부 관리
        ProcessWalkieCostAndTrigger();
    }

    //글로벌 소리 이벤트 수신 핸들러
    private void OnSoundEventReceived(SoundEvent soundEvent)
    {
        if (!Object.HasStateAuthority) return;
        if (currentState == CreatureState.Capture) return;

        //10초 보호 기간 중에는 모든 소리 자극을 무시
        if (rescueProtectTimer > 0f) return;

        // 소리 발생 구역이 다르면 무시
        if (soundEvent.sourceZone != myZone) return;

        //크리처와 소리 발생원 간의 거리 계산
        float distance = Vector3.Distance(transform.position, soundEvent.sourcePosition);

        //실제 체감 dB 연산
        float perceivedDb = sensor.CalculatePerceivedDb(soundEvent.voicedB, distance, soundEvent.obstaclePenaltydB);

        float currentAlertThresh = sensor.alertThresholdDB;
        float currentCriticalThresh = sensor.criticalThresholdDB;

        //3막 이벤트 발동에 따른 예민도 처리
        ZoneLightingManager manager = ZoneLightingManager.GetManager(myZone);

        if (manager != null && manager.IsAct3Active)
        {
            currentAlertThresh *= 0.5f;
            currentCriticalThresh *= 0.5f;
        }

        //Critical dB 이상 자극 감지 시 즉지 Chase로 상태 변경
        if (perceivedDb >= currentCriticalThresh)
        {
            //현재 상태가 이미 추적이나 경계라면 타겟 갱신
            if (currentState == CreatureState.Chaser || currentState == CreatureState.AlerMove)
            {
                if (ShouldUpdateTarget(perceivedDb, soundEvent.channel, distance))
                {
                    //같은 자리에서 들리는 마이크/발소리 스팸으로 인한 타이머 초기화 방지
                    if (Vector3.Distance(targetLocation, soundEvent.sourcePosition) > 2.0f) stateTimer = 0f;

                    targetLocation = soundEvent.sourcePosition;
                    currentTrackedDb = perceivedDb;
                    currentTrackedChannel = soundEvent.channel;
                    motor.MoveToDestination(targetLocation);
                    stateTimer = 0f;
                }
            }

            //순찰이나 수색 중이면 바로 상태 변경
            else
            {
                currentState = CreatureState.Chaser;
                currentSearchPhase = SearchPhase.None;
                stateTimer = 0f;

                motor.SetSpeed(chaseSpeed);
                targetLocation = soundEvent.sourcePosition;
                currentTrackedDb = perceivedDb;
                currentTrackedChannel = soundEvent.channel;
                motor.MoveToDestination(targetLocation);
                walkieTracker.ResetCost();
                Debug.Log("소리로 즉시 반응");
            }
        }

        //Alert dB 이상 감지
        else if (perceivedDb >= currentAlertThresh && currentState != CreatureState.Chaser)
        {
            //Search 중 Alert dB를 들으면 즉기 Chase로 상태 변경
            if (currentState == CreatureState.Search)
            {
                currentState = CreatureState.Chaser;
                currentSearchPhase = SearchPhase.None;
                stateTimer = 0f;

                motor.SetSpeed(chaseSpeed);
                targetLocation = soundEvent.sourcePosition;
                currentTrackedDb = perceivedDb;
                currentTrackedChannel = soundEvent.channel;
                motor.MoveToDestination(targetLocation);
                walkieTracker.ResetCost();
                stateTimer = 0f;
                Debug.Log("소리로 chaser로 변경");
            }

            //AlertMove 중 타깃 갱신
            else if (currentState == CreatureState.AlerMove)
            {                
                if (ShouldUpdateTarget(perceivedDb, soundEvent.channel, distance))
                {
                    //같은 자리에서 들리는 마이크/발소리 스팸으로 인한 타이머 초기화 방지
                    if (Vector3.Distance(targetLocation, soundEvent.sourcePosition) > 2.0f) stateTimer = 0f;

                    targetLocation = soundEvent.sourcePosition;
                    currentTrackedDb = perceivedDb;
                    currentTrackedChannel = soundEvent.channel;
                    motor.MoveToDestination(targetLocation);
                    Debug.Log("소리로 AlertMove 유지");
                }
            }

            //Patrol 중 Alert dB를 들으면 AlertMove로 상태 변경
            else if (currentState == CreatureState.Patrol)
            {
                currentState = CreatureState.AlerMove;
                currentSearchPhase = SearchPhase.None;
                stateTimer = 0f;

                motor.SetSpeed(alertMoveSpeed);
                targetLocation = soundEvent.sourcePosition;
                currentTrackedDb = perceivedDb;
                currentTrackedChannel = soundEvent.channel;
                motor.MoveToDestination(targetLocation);
                walkieTracker.ResetCost();
                Debug.Log("소리로 AlertMove로 변경");
            }
        }
    }

    private bool ShouldUpdateTarget(float newDb, SoundChannel newChannel, float newDistance)
    {
        //새로운 소리가 현재 타겟의 dB보다 크면 즉시 갱신
        if (newDb > currentTrackedDb + 0.1f) return true;

        //새로운 소리가 작으면 무시
        if (newDb < currentTrackedDb - 0.1f) return false;

        //소리 크기가 거의 같을 때
        if (Mathf.Abs(newDb - currentTrackedDb) <= 0.1f)
        {
            //자연음 dB >= 무전음 dB이면 자연음 우선
            if (newChannel == SoundChannel.Natural && currentTrackedChannel == SoundChannel.Walkie) return true;
            if (newChannel == SoundChannel.Walkie && currentTrackedChannel == SoundChannel.Natural) return false;

            //채널마저 동일하다면, 거리가 더 가까운 곳으로 타겟 갱신
            float currentDist = Vector3.Distance(transform.position, targetLocation);
            if (newDistance < currentDist) return true;
        }

        return false;
    }

    private void ProcessWalkieCostAndTrigger()
    {
        //Patrol 상태가 아닐 때는 무전 소리를 들어도 코스트가 쌓이지 않도록 초기화 후 반환
        if (currentState != CreatureState.Patrol)
        {
            walkieTracker.ResetCost();
            return;
        }

        //트래커에게 현 시간, 구역, 위치 값을 넘겨주고 코스트 누적 계산
        if (walkieTracker.ProcessWalkieCost(Runner.DeltaTime, myZone, transform.position, out Vector3 walkieLocation))
        {
            //경계 이동 상태로 전환
            currentState = CreatureState.AlerMove;
            currentSearchPhase = SearchPhase.None;
            stateTimer = 0f;

            //이동 속도를 경계 속도로 올리고, 타겟 위치를 무전기 위치로 설정하여 출발
            motor.SetSpeed(alertMoveSpeed);
            targetLocation = walkieLocation;
            motor.MoveToDestination(targetLocation);

            //무전 코스트 누적으로 인한 이동이므로, 이후 소리 비교를 위해 최소 Alert 수준 dB 세팅
            currentTrackedDb = sensor.alertThresholdDB;
            currentTrackedChannel = SoundChannel.Walkie;

            //트리거가 발동하여 이동을 시작했으므로 누적된 코스트는 0으로 싹 비워줌
            walkieTracker.ResetCost();
        }
    }

    private bool CheckAndHanledSearchTimeout()
    {
        //수색 중이 아니면 무시
        if (currentSearchPhase == SearchPhase.None) return false;

        //전체 수색 타이머 증가
        overallSearchTimer += Runner.DeltaTime;

        //수색 애니메이션 재생 중일 때는 제한 시간이 지나도 끝까지 실행
        if (overallSearchTimer >= searchDuration && currentState != CreatureState.Search)
        {
            //이동 중 너무 오래 걸린 경우에만 안전장치로 순찰 상태 강제 복귀
            EndSearchPhase();
            return true;
        }
        
        return false;
    }

    private bool DetectAndHandlePlayer()
    {
        //씬에 있는 모든 플레이어를 찾아 가져옴
        PlayerController[] allPlayer = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (PlayerController p in allPlayer)
        {
            //크리처 담당 구역과 플레이어 소속 구역이 다르면 무시
            if (p.NetZone != this.myZone) continue;

            //플레이어가 정상 상태(생존)가 아니면 무시
            if (p.NetPlayerState != PlayerState.Normal) continue;

            //플레이어가 어딘가에 숨어있는 상태인지 확인
            if (p.NetHideState != HideState.None)
            {                                
                //몰래 숨었지만 크리처가 수색 중 너무 가까이 와서 은신 발각 범위에 들어온 경우 포획
                if (sensor.CheckHiddenPlayerDetect(p.transform, p.NetHideState))
                {
                    ExecuteCapture(p);
                    return true;
                }

                //내가 지금 촞고 있는 타겟이라면, 강제 포획
                if (currentState == CreatureState.Chaser && playerTarget == p.transform)
                {
                    Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);
                    Vector3 flatTargetPos = new Vector3(p.transform.position.x, 0, p.transform.position.z);
                    float dist = Vector3.Distance(flatCreaturePos, flatTargetPos);
                    float yDiff = Mathf.Abs(transform.position.y - p.transform.position.y);

                    //포획 가능 거리를 늘려 캐비닛 앞에서 비비는 즉시 포획 모션 발동
                    if (dist <= 2.5f && yDiff <= 2.0f)
                    {
                        ExecuteCapture(p);
                        return true;
                    }
                }

                //시야 밖에서 안전하게 숨은 경우 시야 검사 무시
                continue;
            }

            //추적 중 포획 거리 내에 들어왔는지 확인 (안 숨은 상태)
            if (currentState == CreatureState.Chaser && playerTarget == p.transform)
            {
                //포획 조건 만족 시 포획 실행
                if (sensor.CheckCaptureCondition(p.transform))
                {
                    ExecuteCapture(p);
                    return true;
                }
            }

            //플레이어가 시야에 들어왔는지 확인
            if (sensor.CheckLineOfSight(p.transform))
            {
                //새로운 위협 발견 시 기존 수색 즉시 강제 종료
                currentSearchPhase = SearchPhase.None;

                //타겟 설정 및 타겟 위치 저장
                playerTarget = p.transform;
                targetLocation = playerTarget.position;

                //추적 상태가 아닐 경우 추적 상태로 전환
                if (currentState != CreatureState.Chaser)
                {
                    currentState = CreatureState.Chaser;

                    //추적 속도 변경
                    motor.SetSpeed(chaseSpeed);

                    //무전 코스트 초기화
                    walkieTracker.ResetCost();                    
                }

                //플레이어를 발견했으므로 탐색 중단하고 트루 반환
                return true;
            }
        }

        return false;
    }

    private void ManageChaseLosLost()
    {
        //추적 상태가 아닐 경우 시야 상실 로직 무시
        if (currentState != CreatureState.Chaser) return;

        //타겟을 완전히 잃어버린 상태면 무시
        if (playerTarget == null) return;

        //타겟을 시야에 담고 있다면 타이머 초기화
        if (sensor.CheckLineOfSight(playerTarget))
        {
            losLostTimer = 0f;
            targetLocation = playerTarget.position;
            return;
        }

        //시야에서 놓쳤을 경우 타이머 증가
        losLostTimer += Runner.DeltaTime;

        //시야에서 사라져도 lockOnBreakTime 동안에는 타겟의 실제 위치를 정확히 추적
        if (losLostTimer < lockOnBreakTime) targetLocation = playerTarget.position;

        //시야 상실 후 lockOnBreakTime 초과 시, 타겟의 마지막 위치를 중심으로 수색 시작
        else if (losLostTimer >= lockOnBreakTime)
        {
            currentState = CreatureState.Search;

            //최초 주변 수색 페이즈 설정
            currentSearchPhase = SearchPhase.InitialLookAround;
            overallSearchTimer = 0f;
            searchCenter = targetLocation;
            stateTimer = 0f;
            currentTrackedDb = 0f;

            //타겟 초기화 및 제자리 대기
            playerTarget = null;
            motor.StopMoving();
        }
        
    }  

    //수색 페이즈를 종료하고 순찰로 복귀
    private void EndSearchPhase()
    {
        currentSearchPhase = SearchPhase.None;
        overallSearchTimer = 0f;
        currentState = CreatureState.Patrol;
        stateTimer = 0f;

        //순찰 로직이 제대로 작동하도록 이동 중지
        motor.StopMoving();
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

        //눈에 보이지 않는 소리를 쫓아가다가 막혔을 때나 NavMesh가 끊겨 있을 때
        bool reachedNormally = motor.HasReachedDestination(1.0f);
        bool isPathBroken = (agent.pathStatus == NavMeshPathStatus.PathPartial || agent.pathStatus == NavMeshPathStatus.PathInvalid);
        bool isStuck = agent.velocity.sqrMagnitude < 0.1f;

        //0.2초 이상 지났을 때: 정상 도착했거나 길이 끊긴 곳에서 멈춰 섰거나 1초 이상 지났을 때
        if (stateTimer > 0.2f && (reachedNormally || (isStuck && (isPathBroken || stateTimer > 1.0f))))
        {
            //최초 소리 근원지에 도착했다면 제자리에서 주변 수색 시작
            if (currentSearchPhase == SearchPhase.None)
            {                
                currentState = CreatureState.Search;
                currentTrackedDb = 0f;
                stateTimer = 0f;                
                motor.StopMoving();

                //수색 시작 지점 설정
                currentSearchPhase = SearchPhase.InitialLookAround;
                overallSearchTimer = 0f;
                searchCenter = transform.position;
            }

            //주변 랜덤 지점까지의 이동을 마쳤다면 해당 지점에서 다시 수색 시작
            else if (currentSearchPhase == SearchPhase.MovingToRandomPoint)
            {                
                currentState = CreatureState.Search;
                stateTimer = 0f;
                motor.StopMoving();

                //추가 주변 수색 상태로 전환
                currentSearchPhase = SearchPhase.SecondaryLookAround;
            }
        }
    }

    private void UpdateSearchState()
    {
        //수색 타이머 증가 및 하위 행동 타이머 증가
        stateTimer += Runner.DeltaTime;        
        
        //설정된 시간 동안 탐색 후 주변 수색 이동 시작
        if (currentSearchPhase == SearchPhase.InitialLookAround && stateTimer >= searchLookAroundTime)
        {
            currentState = CreatureState.AlerMove;
            stateTimer = 0f;
            currentSearchPhase = SearchPhase.MovingToRandomPoint;
            motor.SetSpeed(alertMoveSpeed);

            //수색 반경 NavMesh 내 유효한 무작위 위치 탐색 후 이동
            Vector3 randomDest = GetValidSearchPoint(searchCenter, searchRadius);
            motor.MoveToDestination(randomDest);
        }

        //추가 수색까지 끝났다면 수색을 종료하고 순착 복귀
        else if (currentSearchPhase == SearchPhase.SecondaryLookAround && stateTimer >= searchLookAroundTime) EndSearchPhase();        
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

        //유효한 위치를 찾지 못하면 제자리 유지
        return center;
    }

    private void UpdateChaseState()
    {
        //모터를 통해 타겟 위치로 이동
        motor.MoveToDestination(targetLocation);

        //소리를 쫓아온 경우 목적지에 도착하면 수색 상태로 전환
        if (playerTarget == null)
        {
            stateTimer += Runner.DeltaTime;

            //눈에 보이지 않는 소리를 쫓아가다가 막혔을 때나 NavMesh가 끊겨 있을 때
            bool reachedNormally = motor.HasReachedDestination(1.0f);
            bool isPathBroken = (agent.pathStatus == NavMeshPathStatus.PathPartial || agent.pathStatus == NavMeshPathStatus.PathInvalid);
            bool isStuck = agent.velocity.sqrMagnitude < 0.1f;

            //0.2초 이상 지났을 때: 정상 도착했거나 길이 끊긴 곳에서 멈춰 섰거나 1초 이상 지났을 때
            if (stateTimer > 0.2f && (reachedNormally || (isStuck && (isPathBroken || stateTimer > 1.0f))))
            {
                currentState = CreatureState.Search;

                //주변 수색 상태로 변경
                currentSearchPhase = SearchPhase.InitialLookAround;
                overallSearchTimer = 0f;
                searchCenter = transform.position;
                stateTimer = 0f;
                currentTrackedDb = 0f;

                motor.StopMoving();
                Debug.Log("[CreatureAI] 문(NavMesh Obstacle)에 막혀 더 이상 접근 불가 -> 즉시 수색(Search)으로 전환");
            }
        }
    }

    private void ExecuteCapture(PlayerController target)
    {       
        if (target.NetHideState != HideState.None)
        {
            //플레이어가 숨어있는 곳의 네트워크ID를 이용해 Photon 내부 딕셔너리에서 찾음
            if (Runner.TryFindObject(target.NetCurrentHideSpotId, out NetworkObject hideSpotObj))
            {
                //네트워크 오브젝트로 되어 있는 HideSpot 상호작용 스크립트를 찾음
                HideSpotInteractable spot = hideSpotObj.GetComponentInChildren<HideSpotInteractable>();

                if (spot != null)
                {
                    spot.ServerTryExit(target);
                }
            }
        }

        //플레이어 컨트롤러의 포획 함수 호출
        target.ServerEnterCaptured(playerRespawnPoint.position, playerRespawnPoint.rotation);

        //포획 상태로 전환 및 포획 중 플래그 활성화
        currentState = CreatureState.Capture;
        isCapturing = true;
        stateTimer = 0f;
        currentSearchPhase = SearchPhase.None;
        currentTrackedDb = 0f;

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

    //구출 구역 성공 시 호출
    public void ActivateRescueProtection()
    {
        if (Object.HasStateAuthority)
        {
            rescueProtectTimer = rescueProtectTime;

            //이미 포획 중인 상태가 아니라면 안전 확보를 위해 모든 어그로 초기화 후 순찰로 복귀
            if (currentState != CreatureState.Capture)
            {
                currentState = CreatureState.Patrol;
                currentSearchPhase = SearchPhase.None;
                playerTarget = null;
                currentTrackedDb = 0f;
                motor.SetSpeed(patrolSpeed);
                walkieTracker.ResetCost();
            }

            Debug.Log("[CreatureAI] 구출 구역 개방 성공! 10초간 크리처 상태 전이 보호가 활성화됩니다.");
        }
    }

    //구출 구역 이탈 시 보호 즉시 종료
    public void CancelRescueProtection()
    {
        if (Object.HasStateAuthority && rescueProtectTimer > 0f)
        {
            rescueProtectTimer = 0f;
            Debug.Log("[CreatureAI] 플레이어가 구출 구역을 이탈하여 10초 보호가 즉시 해제됩니다!");
        }
    }

    public void ApplyAct3Multipliers(bool isAct3)
    {
        //3막 진입 시 배율 적용
        if (isAct3)
        {
            //이동 속도 증가
            patrolSpeed *= 1.25f;
            alertMoveSpeed *= 1.25f;
            searchSpeed *= 1.25f;
            chaseSpeed *= 1.25f;

            //센서 예민도 증가
            walkieTracker.SetAct3(true);
        }
    }
    #endregion
}