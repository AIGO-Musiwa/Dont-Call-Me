using System;
using UnityEngine;
using UnityEngine.AI;

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
    [Header("현재 상태")]
    public CreatureState currentState = CreatureState.Patrol;

    [Header("순찰 지점 (Waypoints)")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    private NavMeshAgent agent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //agent 컴포넌트 초기화
        agent = GetComponent<NavMeshAgent>();

        //순찰 지점이 존재하는 경우 첫 번째 지점으로 이동
        if(waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //현재 상태에 따라 행동을 결정
        switch(currentState)
        {
            case CreatureState.Patrol:
                UpdatePatrol();
                break;
            case CreatureState.AlerMove:
                break;
            case CreatureState.Search:
                break;
            case CreatureState.Chaser:
                break;
            case CreatureState.Capture:
                break;
        }
    }

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
}
