using Fusion;
using System.Collections;
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
    private Transform[] currentFloorWaypoints;
    private int currentWaypointIndex = 0;
    public float patrolSpeed = 3.5f;

    [Header("탐색 설정")]
    public float searchDuration = 3f;       //탐색 지속 시간
    private float currentSearchTime = 0f;   //탐색 타이머

    [Header("추적 설정")]
    public float chaseSpeed = 6.5f;         //추적 속도

    [Header("포획 설정")]
    public float captureDistance = 1.5f;    //플레이어를 포획할 수 있는 최대 거리
    public Transform playerRespawnPoint;    //플레이어가 포획된 후 이동할 위치
    public Transform creatureRespawnPoint1F;//크리처가 포획된 후 이동할 1층 위치    
    public Transform creatureRespawnPoint3F;//크리처가 포획된 후 이동할 위치    
    private int currentFloor = 1;           //크리처가 현재 위치한 층 번호        


    [Header("Creature 시야(LoS) 설정")]
    public float sightDistance = 7.0f;      //시야 거리
    public float fieldOfView = 120f;        //시야각
    public float eyeHeight = 1.4f;          //눈 높이
    public LayerMask obstaclMask;           //장애물 레이어 마스크

    [Header("무전기 소리 설정")]
    public float alertThresholdDB = 40f;    //경계 이동 상태로 전환되는 데시벨 임계값
    public float chaseThresholdDb = 70f;    //추적 상태로 전환되는 데시벨 임계값
    public float dbDropPerMeter = 2f;       //거리당 데시벨 감소량

    private NavMeshAgent agent;
    private Vector3 soundLocation;          //무전기 소리 위치 저장 변수
    private Vector3 lastKnownPosition;      //플레이어 마지막으로 알려진 위치 저장 변수

    private float originalLightIntensity;   //원래 조명 밝기 저장 변수
    public Light directionalLight;


    #region 로컬용 Start 함수 (테스트용)
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{
    //    //agent 컴포넌트 초기화
    //    agent = GetComponent<NavMeshAgent>();
    //    agent.speed = patrolSpeed;

    //    //시작 시 1층 웨이포인트를 기본 구역으로 설정하고 첫 지점으로 이동
    //    currentFloorWaypoints = waypoints1F;
    //    if (currentFloorWaypoints != null && currentFloorWaypoints.Length > 0)
    //    {
    //        agent.SetDestination(currentFloorWaypoints[0].position);
    //    }

    //    //시작 시 원래 조명 밝기 저장
    //    if (directionalLight != null)
    //    {
    //        originalLightIntensity = directionalLight.intensity;
    //    }
    //}
    #endregion    

    #region 로컬용 Update 함수 (테스트용)
    // Update is called once per frame
    //void Update()
    //{
    //    //============[테스트용 임시 코드]============
    //    //키보드가 연결되어 있는지 확인 후 1번 또는 2번 키 입력 감지
    //    if (Keyboard.current != null)
    //    {
    //        if (Keyboard.current.digit1Key.wasPressedThisFrame)
    //        {
    //            Debug.Log("테스트: 플레이어가 50dB 작은 소리로 무전");
    //            OnHearRadioSound(player.position, 50f, false);
    //        }

    //        if (Keyboard.current.digit2Key.wasPressedThisFrame)
    //        {
    //            Debug.Log("테스트: 플레이어가 90dB 큰 소리로 무전");
    //            OnHearRadioSound(player.position, 90f, true);
    //        }
    //    }

    //    if (currentState == CreatureState.Capture)
    //    {
    //        UpdateCapture();
    //        return;
    //    }

    //    Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);
    //    Vector3 flatPlayerPos = new Vector3(player.position.x, 0, player.position.z);
    //    float currentFlatDistance = Vector3.Distance(flatCreaturePos, flatPlayerPos);

    //    if (currentFlatDistance <= captureDistance)
    //    {
    //        Debug.Log($"크리처: 잡았다. (평면 거리: {currentFlatDistance:F2}m)");
    //        StartCoroutine(CaptureSequenceWithLight());
    //        return;
    //    }

    //    //이미 추적 상태인 경우 or 붙잡은 상태가 아닐 때만 시야 체크
    //    if (currentState != CreatureState.Chaser)
    //    {
    //        //시야 체크에서 플레이어가 보이면 추적 상태로 전환
    //        if (CheckLineOfSight())
    //        {
    //            Debug.Log("크리처: 시야에 플레이어 포착");
    //            currentState = CreatureState.Chaser;
    //            agent.speed = chaseSpeed;
    //            lastKnownPosition = player.position;
    //        }
    //    }

    //    //현재 상태에 따라 행동을 결정
    //    switch (currentState)
    //    {
    //        case CreatureState.Patrol:
    //            UpdatePatrol();
    //            break;
    //        case CreatureState.AlerMove:
    //            UpdateAlertMove();
    //            break;
    //        case CreatureState.Search:
    //            UpdateSearch();
    //            break;
    //        case CreatureState.Chaser:
    //            UpdateChaser();
    //            break;
    //        case CreatureState.Capture:
    //            UpdateCapture();
    //            break;
    //    }
    //}
    #endregion

    #region Fusion용 Spawned 함수
    public override void Spawned()
    {
        //초기 상태 설정
        currentState = CreatureState.Patrol;

        //agent 컴포넌트 초기화
        agent = GetComponent<NavMeshAgent>();

        if (Object.HasStateAuthority)
        {
            agent.enabled = true;
            agent.speed = patrolSpeed;
        }

        else agent.enabled = false;

        //시작 시 원래 조명 밝기 저장
        if (directionalLight != null)
        {
            originalLightIntensity = directionalLight.intensity;
        }

        //PlayerController를 찾아 타겟 할당
        if (player == null)
        {
            PlayerController foundPlayerScript = FindAnyObjectByType<PlayerController>();
            if (foundPlayerScript != null) player = foundPlayerScript.transform;
            else Debug.LogError("CreatureAI: 플레이어 Transform이 할당되지 않았고 씬에서 PlayerController도 찾을 수 없습니다!");
        }

        //Scene에서 각 층의 웨이포인트 부모 오브젝트를 찾아서 자식으로 있는 웨이포인트들을 배열에 저장
        GameObject wayPointParent1F = GameObject.Find("Waypoints1F");
        if (wayPointParent1F != null)
        {
            waypoints1F = new Transform[wayPointParent1F.transform.childCount];
            for (int i = 0; i < wayPointParent1F.transform.childCount; i++)
            {
                waypoints1F[i] = wayPointParent1F.transform.GetChild(i);
            }
        }

        GameObject wayPointParent2F = GameObject.Find("Waypoints2F");
        if (wayPointParent2F != null)
        {
            waypoints2F = new Transform[wayPointParent2F.transform.childCount];
            for (int i = 0; i < wayPointParent2F.transform.childCount; i++)
            {
                waypoints2F[i] = wayPointParent2F.transform.GetChild(i);
            }
        }

        GameObject wayPointParent3F = GameObject.Find("Waypoints3F");
        if (wayPointParent3F != null)
        {
            waypoints3F = new Transform[wayPointParent3F.transform.childCount];
            for (int i = 0; i < wayPointParent3F.transform.childCount; i++)
            {
                waypoints3F[i] = wayPointParent3F.transform.GetChild(i);
            }
        }

        //시작 시 1층 웨이포인트를 기본값으로 설정
        SetCurrentFloor(1);
    }
    #endregion

    #region Fusion용 FixedUpdateNetwork 함수
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (currentFloorWaypoints == null || currentFloorWaypoints.Length == 0 || currentFloorWaypoints[0] == null) return;

        //============[테스트용 임시 코드]============
        //키보드가 연결되어 있는지 확인 후 1번 또는 2번 키 입력 감지
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Debug.Log("테스트: 플레이어가 50dB 작은 소리로 무전");
                OnHearRadioSound(player.position, 50f, false);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                Debug.Log("테스트: 플레이어가 90dB 큰 소리로 무전");
                OnHearRadioSound(player.position, 90f, true);
            }
        }

        if (currentState == CreatureState.Capture)
        {
            UpdateCapture();
            return;
        }

        Vector3 flatCreaturePos = new Vector3(transform.position.x, 0, transform.position.z);

        //플레이어를 찾지 못한 상태면 추적/거리 계산 생략
        if (player != null)
        {
            Vector3 flatPlayerPos = new Vector3(player.position.x, 0, player.position.z);
            float currentFlatDistance = Vector3.Distance(flatCreaturePos, flatPlayerPos);

            if (currentFlatDistance <= captureDistance)
            {
                Debug.Log($"크리처: 잡았다. (평면 거리: {currentFlatDistance:F2}m)");
                StartCoroutine(CaptureSequenceWithLight());
                return;
            }

            //이미 추적 상태인 경우 or 붙잡은 상태가 아닐 때만 시야 체크
            if (currentState != CreatureState.Chaser)
            {
                //시야 체크에서 플레이어가 보이면 추적 상태로 전환
                if (CheckLineOfSight())
                {
                    Debug.Log("크리처: 시야에 플레이어 포착");
                    currentState = CreatureState.Chaser;
                    agent.speed = chaseSpeed;
                    lastKnownPosition = player.position;
                }
            }
        }

        //현재 상태에 따라 행동을 결정
        switch (currentState)
        {
            case CreatureState.Patrol:
                UpdatePatrol();
                break;
            case CreatureState.AlerMove:
                UpdateAlertMove();
                break;
            case CreatureState.Search:
                UpdateSearch();
                break;
            case CreatureState.Chaser:
                UpdateChaser();
                break;
            case CreatureState.Capture:
                UpdateCapture();
                break;
        }
    }
    #endregion

    #region Creature 무전기 소리 감지 로직
    public void OnHearRadioSound(Vector3 noisePosition, float rawDb, bool isGlobal)
    {
        float perceivedDb = rawDb;

        if (!isGlobal)
        {
            //Creature 위치와 소리 위치 사이의 거리 계산
            float distance = Vector3.Distance(transform.position, noisePosition);

            //거리에 따라 크리쳐가 인지하는 데시벨 계산
            perceivedDb -= (distance * dbDropPerMeter);

            Debug.Log($"원본 소리: {rawDb} | 거리: {distance:F1}m | Creature 체감: {perceivedDb}db");
        }

        else Debug.Log($"원본 소리: {rawDb} | 글로벌 소리로 거리 무시 | Creature 체감: {perceivedDb}db");

        //체감 데시벨이 추적 임계값 이상이면 추적 이동 상태로 전환
        if (perceivedDb >= chaseThresholdDb)
        {
            Debug.Log("Creature 상태 변경: 추적 (Chaser)");
            currentState = CreatureState.Chaser;
            agent.speed = chaseSpeed;
            lastKnownPosition = noisePosition;
        }

        //체감 데시벨이 추적 경계 임계값 이상이면 경계 이동 상태로 전환 
        else if (perceivedDb >= alertThresholdDB)
        {
            //플레이어를 보고 쫒아가는 중이 아닐 때만 소리 난 곳으로 이동
            if (currentState != CreatureState.Chaser)
            {
                Debug.Log("Creature 상태 변경: 경계 이동 (AlertMove)");
                currentState = CreatureState.AlerMove;
                soundLocation = noisePosition;
                agent.speed = patrolSpeed * 1.5f;
                agent.SetDestination(soundLocation);
            }
        }
    }

    #endregion

    #region Creature 시야(LoS) 감지 로직
    bool CheckLineOfSight()
    {
        //player null체크
        if (player == null) return false;

        //Creature가 플레이어를 향하는 방향 및 사이 거리 계산
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        //플레이어가 시야 거리 내에 있는지 확인
        if (distanceToPlayer <= sightDistance)
        {
            //Creature가 플레이어를 향하는 방향과 정면 사이의 각도 계산
            float angle = Vector3.Angle(transform.forward, directionToPlayer);

            //시야각의 절반 이내에 있는지 확인
            if (angle <= fieldOfView / 2f)
            {
                //Creature의 눈 위치 계산 (높이 적용)
                Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;

                Vector3[] targetPoints = {
                    player.position + Vector3.up * 1.6f,    //플레이어 머리
                    player.position + Vector3.up * 1.0f,    //플레이어 몸통
                    player.position + Vector3.up * 0.2f     //플레이어 다리
                };

                //Creature의 눈 위치에서 플레이어의 각 지점으로 레이캐스트
                foreach (Vector3 target in targetPoints)
                {
                    //눈에서 플레이어 지점으로 향하는 방향 계산
                    Vector3 dirtoTarget = (target - eyePosition).normalized;

                    //레이캐스트로 장애물 여부 확인
                    if (!Physics.Raycast(eyePosition, dirtoTarget, distanceToPlayer, obstaclMask))
                    {
                        //하나라도 시야에 보이면 true 반환
                        return true;
                    }
                }
            }
        }

        return false;
    }

    #endregion

    #region Creature 행동 업데이트 로직

    //텔레포터에서 호출해주어 크리처의 현재 순찰 구역(층)을 갱신하는 함수
    public void SetCurrentFloor(int floorNumber)
    {
        //현재 층 번호 업데이트
        currentFloor = floorNumber;

        if (floorNumber == 1) currentFloorWaypoints = waypoints1F;
        else if (floorNumber == 2) currentFloorWaypoints = waypoints2F;
        else if (floorNumber == 3) currentFloorWaypoints = waypoints3F;

        Debug.Log($"크리처: {floorNumber}층으로 구역 갱신 완료! 이제 이 층을 순찰합니다.");
    }

    private void UpdatePatrol()
    {
        agent.speed = patrolSpeed;

        //현재 설정된 층의 순찰 지점이 없는 경우 멈춤
        if (currentFloorWaypoints == null || currentFloorWaypoints.Length <= 1) return;

        if (agent.pathPending) return;

        //현재 순찰 지점 0.5m 이내 도착했는지 확인
        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            //현재 순찰 지점에서 다음 순찰 지점으로 이동하기 전에 랜덤하게 다른 지점 선택
            int nextIndex = currentWaypointIndex;

            while (nextIndex == currentWaypointIndex)
            {
                //현재 층의 웨이포인트 배열에서 랜덤 추출
                nextIndex = Random.Range(0, currentFloorWaypoints.Length);
            }

            //다음 순찰 지점 번호 계산
            currentWaypointIndex = nextIndex;

            //다음 순찰 지점으로 이동
            agent.SetDestination(currentFloorWaypoints[currentWaypointIndex].position);
        }
    }

    private void UpdateAlertMove()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentState = CreatureState.Search;
            Debug.Log("Creature 상태 변경: 수색 (Search)");
        }
    }

    private void UpdateSearch()
    {
        //탐색 타임 증가
        currentSearchTime += Time.deltaTime;

        //제자리에서 천천히 회전
        transform.Rotate(Vector3.up * 60f * Time.deltaTime);

        //탐색 시간이 지속 시간 이상이면 순찰 상태로 복귀
        if (currentSearchTime >= searchDuration)
        {
            Debug.Log("Creature 상태 변경: 순찰 (Patrol)");
            currentState = CreatureState.Patrol;
        }
    }

    private void UpdateChaser()
    {
        float currentDistance = Vector3.Distance(transform.position, player.position);

        //플레이어와의 거리가 포획 거리 이내이면 포획 상태로 전환
        if (currentDistance <= captureDistance)
        {
            StartCoroutine(CaptureSequenceWithLight());
            return;
        }

        //플레이어가 시야에 보이는지 확인
        if (CheckLineOfSight())
        {
            lastKnownPosition = player.position;
            agent.SetDestination(lastKnownPosition);
        }
        //플레이어가 시야에서 사라졌지만 마지막으로 알려진 위치로 이동
        else
        {
            agent.SetDestination(lastKnownPosition);

            //길을 찾고 있는 중(Pending)이거나 경로가 없으면 대기
            if (agent.pathPending || !agent.hasPath) return;

            //마지막으로 알려진 위치에 도착했는지 확인
            if (agent.remainingDistance < 0.5f)
            {
                //탐색 상태로 전환
                Debug.Log("Creature 상태 변경: 수색 (Search)");
                currentState = CreatureState.Search;
                currentSearchTime = 0f;
                agent.speed = patrolSpeed;
            }
        }
    }

    private void UpdateCapture()
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
        }
    }

    IEnumerator CaptureSequenceWithLight()
    {
        currentState = CreatureState.Capture;
        
        //크리처 이동 멈춤
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        //암전
        if (directionalLight != null) directionalLight.intensity = 0f;

        //잠시 대기
        yield return new WaitForSeconds(2.0f);

        //플레이어와 크리처를 리스폰 지점으로 이동
        CharacterController playerCC = player.GetComponent<CharacterController>();
        if (playerCC != null) playerCC.enabled = false;
        player.position = playerRespawnPoint.position;
        //플레이어 리스폰 지점 방향 동기화
        player.rotation = playerRespawnPoint.rotation;
        if (playerCC != null) playerCC.enabled = true;

        //3층 리스폰 지점으로 기본값 설정
        Transform targetRespawnPoint = creatureRespawnPoint3F;
        int nextFloor = 3;

        if (currentFloor == 1)
        {
            targetRespawnPoint = creatureRespawnPoint3F;
            nextFloor = 3;

        }
        else if (currentFloor == 2)
        {
            if (Random.value > 0.5f)
            {
                targetRespawnPoint = creatureRespawnPoint1F;
                nextFloor = 1;
            }
            else
            {
                targetRespawnPoint = creatureRespawnPoint3F;
                nextFloor = 3;
            }
        }

        else if (currentFloor == 3)
        {
            targetRespawnPoint = creatureRespawnPoint3F;
                nextFloor = 1;
        }

        //결정된 위치로 크리처 순간이동 및 구역 갱신
        agent.Warp(targetRespawnPoint.position);        
        agent.transform.rotation = targetRespawnPoint.rotation;        
        SetCurrentFloor(nextFloor);

        //크리처 상태 초기화
        agent.isStopped = false;
        currentState = CreatureState.Patrol;

        //조명 원래 밝기로 복구
        if (directionalLight != null)
        {
            float currentIntensity = 0f;
            while (currentIntensity < originalLightIntensity)
            {
                currentIntensity += Time.deltaTime * (originalLightIntensity / 2f); //2초 동안 밝기 복구
                directionalLight.intensity = Mathf.Min(currentIntensity, originalLightIntensity);
                yield return null;
            }

            directionalLight.intensity = originalLightIntensity;
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        //기즈모 색상
        Gizmos.color = Color.red;

        //크리처 실제 눈 위치
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;

        //시야 거리 원 그리기
        Gizmos.DrawWireSphere(eyePosition, sightDistance);

        //시야각의 왼쪽, 오른쪽 경계선 각도 계산
        Vector3 leftBoundary = Quaternion.Euler(0, -fieldOfView / 2f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, fieldOfView / 2f, 0) * transform.forward;

        //눈 위치에서 경계선 방향으로 시야각 선 그리기
        Gizmos.DrawLine(eyePosition, eyePosition + leftBoundary * sightDistance);
        Gizmos.DrawLine(eyePosition, eyePosition + rightBoundary * sightDistance);
    }
}