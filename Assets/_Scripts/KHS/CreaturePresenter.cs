using UnityEngine;
using UnityEngine.AI;
using Fusion;

/// <summary>
/// [기공사 현수 전용] 크리처 시각 출력 장치 - 최종 공정 완료본
/// </summary>
public class CreaturePresenter : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CreatureAI ai;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("설정")]
    [SerializeField] private float rotationSpeed = 10f;

    private static readonly int HashState = Animator.StringToHash("State");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashCaptureTrigger = Animator.StringToHash("OnCapture");

    private CreatureState lastState = (CreatureState)(-1);
    private Vector3 lastPosition;
    private float visualSpeed;
    private bool isFirstFrame = true;

    private void OnEnable()
    {
        lastPosition = transform.position;
        isFirstFrame = true;
        lastState = (CreatureState)(-1);
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

        // 1. [상태 변화 감지 및 초기화]
        if (isFirstFrame || ai.currentState != lastState)
        {
            if (isFirstFrame || (lastState == CreatureState.Capture && ai.currentState != CreatureState.Capture))
            {
                ResetMovementSensor();
            }

            OnStateChanged(lastState, ai.currentState);
            lastState = ai.currentState;
            animator.SetInteger(HashState, (int)ai.currentState);
            isFirstFrame = false;
        }

        // 2. [이동 속도 계산 및 애니메이터 반영]
        UpdateVisualMovement();

        // 3. [시선 처리 - 스마트 룩킹]
        UpdateRotation();
    }

    private void OnStateChanged(CreatureState oldState, CreatureState newState)
    {
        Debug.Log($"[Presenter] 상태 전환: {oldState} -> {newState}");

        switch (newState)
        {
            case CreatureState.Capture:
                animator.SetTrigger(HashCaptureTrigger);
                break;
        }
    }

    private void UpdateVisualMovement()
    {
        float currentFrameSpeed = 0f;

        // 상태별 최대 속도 리미터 (애니메이션 섞임 방지 가이드)
        float maxSpeedLimit = 0f;
        switch (ai.currentState)
        {
            case CreatureState.Patrol: maxSpeedLimit = ai.patrolSpeed; break;
            case CreatureState.AlerMove: maxSpeedLimit = 5.0f; break; // 걷기와 뛰기 사이의 긴장감 있는 섞임
            case CreatureState.Chaser: maxSpeedLimit = ai.chaseSpeed; break;
            default: maxSpeedLimit = ai.chaseSpeed; break;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            currentFrameSpeed = agent.velocity.magnitude;
        }
        else
        {
            float distance = Vector3.Distance(transform.position, lastPosition);
            if (distance > 5.0f)
            {
                ResetMovementSensor();
                return;
            }
            currentFrameSpeed = distance / Time.deltaTime;
            lastPosition = transform.position;
        }

        // [핵심] 클램핑을 통해 데이터 전송 시 생기는 노이즈를 차단
        currentFrameSpeed = Mathf.Clamp(currentFrameSpeed, 0, maxSpeedLimit);

        visualSpeed = Mathf.Lerp(visualSpeed, currentFrameSpeed, Time.deltaTime * 8f);
        if (visualSpeed < 0.1f) visualSpeed = 0f;

        animator.SetFloat(HashMoveSpeed, visualSpeed);
    }

    private void UpdateRotation()
    {
        // 추격/포획 시에는 플레이어를, 경계 이동 시에는 이동 방향(steeringTarget)을 바라봄
        if (ai.currentState == CreatureState.Chaser || ai.currentState == CreatureState.Capture)
        {
            if (ai.player != null) RotateTowards(ai.player.position);
        }
        else if (ai.currentState == CreatureState.AlerMove && agent != null && agent.enabled)
        {
            // 소리 난 곳(목적지)을 향해 몸을 틀며 이동
            RotateTowards(agent.steeringTarget);
        }
    }

    private void RotateTowards(Vector3 targetPos)
    {
        Vector3 targetDir = targetPos - transform.position;
        targetDir.y = 0;

        if (targetDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    private void ResetMovementSensor()
    {
        lastPosition = transform.position;
        visualSpeed = 0f;
        if (animator != null) animator.SetFloat(HashMoveSpeed, 0f);
        Debug.Log("[Presenter] 센서 리셋 완료 (위치 보정)");
    }
}