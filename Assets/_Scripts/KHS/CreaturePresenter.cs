using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [기공사 전용] 크리처 시각 출력 장치.
/// 네트워크로 동기화된 상태와 좌표 변화를 감지하여 애니메이션을 출력함.
/// </summary>
public class CreaturePresenter : MonoBehaviour
{
    [Header("참조 장치")]
    [SerializeField] private CreatureAI ai;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("애니메이터 파라미터")]
    private static readonly int HashState = Animator.StringToHash("State");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    private Vector3 lastPosition;
    private float visualSpeed;

    private void Start()
    {
        lastPosition = transform.position;

        if (ai == null) ai = GetComponentInParent<CreatureAI>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (ai == null || animator == null) return;

        // 1. [상태 동기화] 
        // ai.currentState는 [Networked]이므로 모든 유저의 화면에서 동일한 값이 읽힘.
        animator.SetInteger(HashState, (int)ai.currentState);

        // 2. [속도 계산 공정]
        // 서버(권한자)는 Agent의 실제 속도를 쓰고, 클라이언트는 좌표 변화량으로 속도를 역산함.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            visualSpeed = agent.velocity.magnitude;
        }
        else
        {
            // 클라이언트 화면에서 위치 변화를 감지해 이동 속도 계산 (미끄러짐 방지)
            float distance = Vector3.Distance(transform.position, lastPosition);
            // 델타 타임으로 나누어 초당 속도로 변환 (보간을 위해 부드럽게 처리)
            float currentSpeed = distance / Time.deltaTime;
            visualSpeed = Mathf.Lerp(visualSpeed, currentSpeed, Time.deltaTime * 10f);

            lastPosition = transform.position;
        }

        // 3. 애니메이터에 최종 출력값 전송
        animator.SetFloat(HashMoveSpeed, visualSpeed);
    }
}