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

    public void UpdatePatrolLogic()
    {
        //네브메시 위에 없거나 웨이포인트가 부족하면 실행 안 함
        if (!agent.isOnNavMesh || allWaypoints.Count <= 1) return;

        //순찰 중 경로가 막혀 2초 이상 제자리 걸음인지 체크
        bool isNotMoving = agent.velocity.sqrMagnitude < 0.1f && !agent.pathPending;
        if (isNotMoving) patrolStuckTimer += Time.deltaTime;
        else patrolStuckTimer = 0f;

        //경로가 없거나 목적지에 거의 도착했을 경우, 또는 막혀서 2.0초가 지났을 때 새로운 목적지 설정
        if (!agent.hasPath || agent.remainingDistance < 0.5f || patrolStuckTimer > 2.0f)
        {
            int nextIndex = currentWaypointIndex;
            //현재 위치와 다른 새로운 목적지를 랜덤으로 설정
            while (nextIndex == currentWaypointIndex)
            {
                nextIndex = Random.Range(0, allWaypoints.Count);
            }
            currentWaypointIndex = nextIndex;
            agent.SetDestination(allWaypoints[currentWaypointIndex].position);
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
        transform.rotation = rotation;
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