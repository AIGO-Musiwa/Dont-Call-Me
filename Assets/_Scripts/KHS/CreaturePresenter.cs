using UnityEngine;
using UnityEngine.AI;
using Fusion;

/// <summary>
/// 메인(AI)의 제어권을 침범하지 않고, 순수하게 상태와 속도만을 모니터링하여 시각적으로 출력함.
/// </summary>
public class CreaturePresenter : MonoBehaviour
{
    [Header("핵심 부품 참조")]
    [SerializeField] private CreatureAI ai;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("출력 튜닝 설정")]
    [SerializeField] private float animationBlendSpeed = 8f; // 애니메이션 블렌딩 가속도

    // 애니메이터 해싱 (성능 최적화를 위해 캐싱)
    private static readonly int HashState = Animator.StringToHash("State");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashCaptureTrigger = Animator.StringToHash("OnCapture");

    // 이전 프레임의 데이터 저장소 (상태 변화 및 속도 계산용)
    private CreatureState lastState = (CreatureState)(-1);
    private Vector3 lastPosition;
    private float visualSpeed;
    private bool isFirstFrame = true;

    private void OnEnable()
    {
        // 시작시 초기화
        isFirstFrame = true;
        lastState = (CreatureState)(-1);
        lastPosition = transform.position;
    }

    private void Start()
    {
        if (ai == null) ai = GetComponentInParent<CreatureAI>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (ai == null || animator == null) return;

        // 1. FSM 상태 변화 감지 및 초기화
        if (isFirstFrame || ai.currentState != lastState)
        {
            // 텔레포트(Capture) 이후 복귀 시 찌꺼기 속도 데이터 날리기
            if (isFirstFrame || (lastState == CreatureState.Capture && ai.currentState != CreatureState.Capture))
            {
                ResetMovementSensor();
            }

            OnStateChanged(lastState, ai.currentState);
            lastState = ai.currentState;
            animator.SetInteger(HashState, (int)ai.currentState);
            isFirstFrame = false;
        }

        // 2. 이동 속도 계산 및 애니메이터 반영
        UpdateVisualMovement();

        // 🛠️ [핵심 수리 포인트] 
        // 퓨전의 보간(Interpolation) 이동을 정확히 캐치하기 위해,
        // LateUpdate를 삭제하고 Update의 모든 계산이 끝난 직후 위치를 기록해!
        lastPosition = transform.position;
    }

    // ⛔ LateUpdate는 타이밍 꼬임을 유발하므로 완전히 삭제했습니다.

    /// <summary>
    /// 상태가 변경될 때 발생하는 1회성 이벤트 트리거
    /// </summary>
    private void OnStateChanged(CreatureState oldState, CreatureState newState)
    {
        Debug.Log($"크리처 상태 변속: {oldState} -> {newState}");

        switch (newState)
        {
            case CreatureState.Capture:
                animator.SetTrigger(HashCaptureTrigger);
                break;
        }
    }

    /// <summary>
    /// 실제 이동 데이터를 애니메이션 속도 파라미터로 변환 (프록시 완벽 지원)
    /// </summary>
    private void UpdateVisualMovement()
    {
        float currentFrameSpeed = 0f;

        // 1단계: 현재 상태에 따른 기계적 한계 속도 설정 (애니메이션 튐 방지)
        float maxSpeedLimit = ai.currentState switch
        {
            CreatureState.Patrol => ai.patrolSpeed,
            CreatureState.AlerMove => ai.patrolSpeed * 1.5f,
            CreatureState.Chaser => ai.chaseSpeed,
            CreatureState.Capture => 0f,
            _ => ai.chaseSpeed
        };

        // 2단계: 실제 이동 속도 추출 (권한 유무에 따라 분기)
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // 호스트(Authority): 에이전트의 내부 계산 속도 직접 사용
            currentFrameSpeed = agent.velocity.magnitude;
        }
        else
        {
            // 프록시(Proxy): 위치 변화량을 통한 속도 역산 (추측항법)
            float distance = Vector3.Distance(transform.position, lastPosition);

            if (distance > 5.0f)
            {
                ResetMovementSensor();
                return;
            }
            if (Time.deltaTime > 0f)
            {
                currentFrameSpeed = distance / Time.deltaTime;
            }
        }

        // 3단계: 노이즈 필터링 및 클램핑
        currentFrameSpeed = Mathf.Clamp(currentFrameSpeed, 0, maxSpeedLimit);

        // 4단계: 애니메이터에 부드럽게 값 전달 (Lerp로 기계적 마찰 구현)
        visualSpeed = Mathf.Lerp(visualSpeed, currentFrameSpeed, Time.deltaTime * animationBlendSpeed);
        if (visualSpeed < 0.1f) visualSpeed = 0f;

        animator.SetFloat(HashMoveSpeed, visualSpeed);
    }

    private void ResetMovementSensor()
    {
        lastPosition = transform.position;
        visualSpeed = 0f;
        if (animator != null) animator.SetFloat(HashMoveSpeed, 0f);
        Debug.Log("애니메이터 리셋");
    }
}