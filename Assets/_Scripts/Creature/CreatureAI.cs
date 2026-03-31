using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public enum CreatureState
{
    Patrol,         //순찰
    AlerMove,       //경계 이동
    Search,         //수색(탐색)
    Chaser,         //추척
    Capture         //포획
}

public class CreatureAI : MonoBehaviour
{
    [Header("현 상태 및 타겟")]
    public CreatureState currentState = CreatureState.Patrol;
    public Transform player;

    [Header("순찰 지점 (Waypoints)")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;
    public float patrolSpeed = 3.5f;

    [Header("탐색 설정")]
    public float searchDuration = 5f;       //탐색 지속 시간
    private float currentSearchTime = 0f;         //탐색 타이머

    [Header("추적 설정")]
    public float chaseSpeed = 6.5f;         //추적 속도

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //agent 컴포넌트 초기화
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;

        //순찰 지점이 존재하는 경우 첫 번째 지점으로 이동
        if(waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //============[테스트용 임시 코드]============
        //키보드가 연결되어 있는지 확인 후 1번 또는 2번 키 입력 감지
        if(Keyboard.current != null)
        {
            if(Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Debug.Log("테스트: 플레이어가 50dB 작은 소리로 무전");
                OnHearRadioSound(player.position, 50f);
            }

            if(Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                Debug.Log("테스트: 플레이어가 90dB 큰 소리로 무전");
                OnHearRadioSound(player.position, 90f);
            }
        }

        //이미 추적 상태인 경우 or 붙잡은 상태가 아닐 때만 시야 체크
        if(currentState != CreatureState.Chaser && currentState != CreatureState.Capture)
        {
            CheckLineOfSight();
        }

        //현재 상태에 따라 행동을 결정
        switch(currentState)
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
                break;
        }
    }

    #region Creature 무전기 소리 감지 로직
    public void OnHearRadioSound(Vector3 noisePosition, float rawDb)
    {
        //Creature 위치와 소리 위치 사이의 거리 계산
        float distance = Vector3.Distance(transform.position, noisePosition);
        
        //거리에 따라 크리쳐가 인지하는 데시벨 계산
        float perceivedDb = rawDb - (distance * dbDropPerMeter);

        Debug.Log($"원본 소리: {rawDb} | 거리: {distance:F1}m | Creature 체감: {perceivedDb}db");

        //체감 데시벨이 추적 임계값 이상이면 추적 이동 상태로 전환
        if (perceivedDb >= chaseThresholdDb)
        {
            currentState = CreatureState.Chaser;
            agent.speed = chaseSpeed;
            Debug.Log("Creature 상태 변경: 추적 (Chaser)");
        }

        //체감 데시벨이 추적 경계 임계값 이상이면 경계 이동 상태로 전환 
        else if(perceivedDb >= alertThresholdDB)
        {
            //플레이어를 보고 쫒아가는 중이 아닐 때만 소리 난 곳으로 이동
            if(currentState != CreatureState.Chaser)
            {
                currentState = CreatureState.AlerMove;
                soundLocation = noisePosition;
                agent.speed = patrolSpeed * 1.5f;
                agent.SetDestination(soundLocation);
                Debug.Log("Creature 상태 변경: 경계 이동 (AlertMove)");
            }
        }
    }

    #endregion

    #region Creature 시야(LoS) 감지 로직
    void CheckLineOfSight()
    {
        //player null체크
        if(player == null) return;

        //Creature가 플레이어를 향하는 방향 및 사이 거리 계산
        Vector3 directionToPlayer = (player.position - transform.position).normalized;        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        //플레이어가 시야 거리 내에 있는지 확인
        if(distanceToPlayer <= sightDistance)
        {
            //Creature가 플레이어를 향하는 방향과 정면 사이의 각도 계산
            float angle = Vector3.Angle(transform.forward, directionToPlayer);

            //시야각의 절반 이내에 있는지 확인
            if(angle <= fieldOfView / 2f)
            {
                //Creature의 눈 위치 계산 (높이 적용)
                Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;

                Vector3[] targetPoints = {
                    player.position + Vector3.up * 1.6f,    //플레이어 머리
                    player.position + Vector3.up * 1.0f,    //플레이어 몸통
                    player.position + Vector3.up * 0.2f     //플레이어 다리
                };

                bool isVisible = false;

                //Creature의 눈 위치에서 플레이어의 각 지점으로 레이캐스트
                foreach (Vector3 target in targetPoints)
                {
                    //눈에서 플레이어 지점으로 향하는 방향 계산
                    Vector3 dirtoTarget = (target - eyePosition).normalized;

                    //레이캐스트로 장애물 여부 확인
                    if(!Physics.Raycast(eyePosition, dirtoTarget, distanceToPlayer, obstaclMask))
                    {
                        isVisible = true;
                        break;
                    }
                }

                if(isVisible)
                {
                    Debug.Log("Creature: 플레이어가 시야에 감지");
                    currentState = CreatureState.Chaser;
                    agent.speed = chaseSpeed;
                }
            }
        }
    }

    #endregion

    #region Creature 행동 업데이트 로직
    private void UpdatePatrol()
    {
        //순찰 지점이 없는 경우 멈춤
        if (waypoints.Length == 0) return;

        //현재 순찰 지점 0.5m 이내 도착했는지 확인
        if(!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            //다음 순찰 지점 번호 계산
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;

            //다음 순찰 지점으로 이동
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    private void UpdateAlertMove()
    {
        if(!agent.pathPending && agent.remainingDistance < 0.5f)
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
        if(currentSearchTime >= searchDuration)
        {
            Debug.Log("Creature 상태 변경: 순찰 (Patrol)");
            currentState = CreatureState.Patrol;
        }
    }

    private void UpdateChaser()
    {
        if(player != null)
        {
            agent.SetDestination(player.position);
        }
    }

    #endregion
}
