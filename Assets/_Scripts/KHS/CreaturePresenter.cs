using UnityEngine;
using UnityEngine.AI;


/// <summary>
/// CreatureAI의 상태와 물리 속도를 애니메이터 파라미터로 변환
/// </summary>
public class CreaturePresenter : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CreatureAI ai; //상태를 가져올 AI
    [SerializeField] private NavMeshAgent agent; //속도를 가져올 NavMeshAgent
    [SerializeField] private Animator animator; //애니메이터

    [Header("애니메이터 해시")]
    private static readonly int HashState = Animator.StringToHash("State");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    // 네트워크 프록시(다른 클라이언트)에서도 부드러운 속도 계산을 위한 변수
    private Vector3 lastPosition;
    private float visualSpeed;

    private void Start()
    {
        lastPosition = transform.position;

        // 만약 수동 연결을 안 했다면 자동 감지 시도
        if (ai == null) ai = GetComponentInParent<CreatureAI>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if(ai == null || agent == null || animator == null) return;

        // Enum => int 변환하여 애니메이터에 전달
        // Patrol=0, AlertMove=1, Search=2, Chase=3, Capture =4
        animator.SetInteger(HashState, (int)ai.currentState);

        //이동속도 동기화
        if (agent != null && agent.enabled)
        {
            visualSpeed = agent.velocity.magnitude;
        }
        else
        {
                        // NavMeshAgent가 없거나 비활성화된 경우, 위치 변화량으로 속도 계산
            float distance = Vector3.Distance(transform.position, lastPosition);
            visualSpeed = distance / Time.deltaTime;
            lastPosition = transform.position;
        }

        // 애니메이터에 이동 속도 전달
        animator.SetFloat(HashMoveSpeed, visualSpeed);
    }
}
