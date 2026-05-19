using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class CreatureMotor : MonoBehaviour
{
    [Header("층별 순찰 지점")]
    public Transform[] waypoints1F;
    public Transform[] waypoints2F;
    public Transform[] waypoints3F;

    private NavMeshAgent agent;
    private List<Transform> allWaypoints = new List<Transform>();
    private int currentWaypointIndex = 0;

    //길이 막혀 영원히 서 있는 것을 방지하기 위한 타이머
    private float patrolStuckTimer = 0f;

    public void Initialize()
    {
        //네브메시 에이전트 초기화
        agent = GetComponent<NavMeshAgent>();

        //퓨전(FixedUpdateNetwork)과 네브메시의 충돌을 막기 위해 자동 이동 및 회전을 강제로 끄고, 수동으로 제어
        agent.updatePosition = false;
        agent.updateRotation = false;

        //모든 층의 웨이포인트를 하나의 리스트로 통합
        allWaypoints.Clear();
        if (waypoints1F != null) allWaypoints.AddRange(waypoints1F);
        if (waypoints2F != null) allWaypoints.AddRange(waypoints2F);
        if (waypoints3F != null) allWaypoints.AddRange(waypoints3F);
    }

    public void EnableAgent(bool isEnabled)
    {
        //네브메시 에이전트 활성화 및 비활성화 상태 제어
        if (agent != null) agent.enabled = isEnabled;
    }

    public void SetSpeed(float speed)
    {
        //에이전트 이동 속도 변경
        if (agent != null) agent.speed = speed;
    }

    public void StopMoving()
    {
        //에이전트가 네브메시 위에 있을 경우 이동 중지 및 경로 초기화
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    public void ResumeMoving()
    {
        //이동 중지 상태 해제
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;        
    }

    public void MoveToDestination(Vector3 destination)
    {
        //지정된 목적지로 이동
        if (agent != null && agent.isOnNavMesh) agent.SetDestination(destination);        
    }

    public void UpdatePatrolLogic(float deltaTime)
    {
        //에이전트가 null이거나, NaveMesh 위에 있지 않거나, 웨이포인트가 부족하면 실행 안 함
        if (agent == null || !agent.isOnNavMesh || allWaypoints.Count <= 1)
        {
            patrolStuckTimer = 0f;
            return;
        }

        //순찰 중 경로가 막혀 2초 이상 제자리 걸음인지 체크
        bool isNotMoving = agent.velocity.sqrMagnitude < 0.01f && !agent.pathPending;

        //의도적으로 멈춘 상태(!agent.isStopped)가 아닐 때만 막힘 타이머 작동
        if (isNotMoving) patrolStuckTimer += deltaTime;
        else patrolStuckTimer = 0f;

        //경로가 없거나 목적지에 거의 도착했을 경우, 또는 막혀서 2.0초가 지났을 때 새로운 목적지 설정
        if (!agent.hasPath || agent.remainingDistance < 0.5f || patrolStuckTimer > 2.0f)
        {
            currentWaypointIndex = (currentWaypointIndex + Random.Range(1, allWaypoints.Count)) % allWaypoints.Count;
            agent.isStopped = false;
            agent.SetDestination(allWaypoints[currentWaypointIndex].position);

            //목적지를 바꿨으니 타이머를 즉시 0으로 초기화해 중복 호출 방지
            patrolStuckTimer = 0f;
        }
    }

    public void TickMovement(float deltaTime)
    {
        if (agent == null || !agent.isOnNavMesh) return;

        //네브메시 에이전트의 내부 가상 위치를 현재 실제 트랜스포 ㅁ위치로 매 프레임 강제 동기화
        agent.nextPosition = transform.position;

        //에이전트가 계산한 목적지 방향 백터(desiredVelocity)가 유효한 경우에만 이동
        if (agent.desiredVelocity.sqrMagnitude > 0.01f)
        {
            //트랜스폼 위치 이동 적용
            transform.position += agent.desiredVelocity * deltaTime;

            //회전 적용 (이동 방향을 자연스럽게 바라보도록 보간 회전)
            Vector3 lookDir = agent.desiredVelocity;
            
            //계단이나 경사로에서 크리처의 몸통이 엉뚱하게 위아래로 기울어지는 것을 방지하기 위해 y축은 0으로 고전
            lookDir.y = 0f;

            if (lookDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, deltaTime * 10);
            }
        }
    }

    public bool HasReachedDestination(float threshold = 0.5f)
    {
        //경로를 계산 중이면 도착하지 않은 것으로 간주
        if (agent.pathPending) return false;

        //남은 거리가 임계치 이하인지 확인
        return agent.remainingDistance <= threshold;
    }

    public void WarpTo(Vector3 position, Quaternion rotation)
    {
        //이동을 멈추고 지정된 위치로 즉시 텔레포트
        StopMoving();
        agent.Warp(position);
        transform.position = position;
        transform.rotation = rotation;

        //내부 가상 에이전트 위치도 원점 동기화
        agent.nextPosition = position;
        ResumeMoving();
    }

    public int GetCurrentFloor()
    {
        //현재 크리처의 높이 캐싱
        float myY = transform.position.y;

        //각 층 웨이포인트와의 높이 차이 계산
        float dist1 = waypoints1F != null && waypoints1F.Length > 0 ? Mathf.Abs(myY - waypoints1F[0].position.y) : float.MaxValue;
        float dist2 = waypoints2F != null && waypoints2F.Length > 0 ? Mathf.Abs(myY - waypoints2F[0].position.y) : float.MaxValue;
        float dist3 = waypoints3F != null && waypoints3F.Length > 0 ? Mathf.Abs(myY - waypoints3F[0].position.y) : float.MaxValue;

        //가장 높이 차이가 적은 층 반환
        if (dist1 <= dist2 && dist1 <= dist3) return 1;
        if (dist2 <= dist1 && dist2 <= dist3) return 2;
        return 3;
    }
}