using UnityEngine;
using static UnityEditorInternal.VersionControl.ListControl;

/// <summary>
/// 단일 메쉬 구조에 맞게 최적화된 애니메이션 동기화 모터.
/// 구형 그림자 메쉬 부품은 완전히 제거됨.
/// </summary>
public class PlayerPresenter : MonoBehaviour
{
    [Header("참조 부품")]
    [SerializeField] private PlayerController controller;

    [Header("애니메이션")]
    [SerializeField] private Animator animator; // 🛠️ 이제 이 메인 모터 하나만 사용함!

    [Header("비주얼 세터")]
    [SerializeField] private PlayerVisualSetter visualSetter;

    private static readonly int HashX = Animator.StringToHash("x");
    private static readonly int HashY = Animator.StringToHash("y");
    private static readonly int HashIsCrouch = Animator.StringToHash("IsCrouch");

    // 🛠️ [신규 파라미터] 기절과 회복을 담당할 트리거
    private static readonly int HashDown = Animator.StringToHash("down");
    private static readonly int HashRecover = Animator.StringToHash("recover");

    // 🛠️ [상태 감지기] 이전 프레임의 상태를 기억하여 변화가 생겼을 때만 스위치를 작동시킴
    private PlayerState lastState = (PlayerState)(-1);

    private void Start()
    {
        if (visualSetter != null && controller != null)
        {
            visualSetter.SetupVisual(controller.HasInputAuthority);
        }
    }

    private void Update()
    {
        if (controller == null || animator == null) return;

        // ─────────────────────────────────────────────────────────
        // 🛠️ 1. 기절(다운) 및 구출(회복) 상태 동기화 (상태 변화 감지기)
        // ─────────────────────────────────────────────────────────
        PlayerState currentState = controller.NetPlayerState;

        // 상태가 이전과 달라졌을 때만 1회성 트리거 격발
        if (lastState != currentState)
        {
            HandleStateTransition(lastState, currentState);
            lastState = currentState;
        }

        var motor = controller.KCCMotor;
        if (motor == null || motor.KCC == null) return;

        // ─────────────────────────────────────────────────────────
        // 2. 앉기 상태 동기화 (네트워크 변수 직결)
        // ─────────────────────────────────────────────────────────
        bool syncCrouch = controller.NetIsCrouching;
        animator.SetBool(HashIsCrouch, syncCrouch);

        // ─────────────────────────────────────────────────────────
        // 3. 이동 애니메이션 동기화
        // ─────────────────────────────────────────────────────────
        Vector3 localVelocity = transform.InverseTransformDirection(motor.KCC.RealVelocity);

        animator.SetFloat(HashX, localVelocity.x);
        animator.SetFloat(HashY, localVelocity.z);
    }

    /// <summary>
    /// 상태(Enum) 변화에 따른 1회성 애니메이션 트리거 작동 회로
    /// </summary>
    private void HandleStateTransition(PlayerState oldState, PlayerState newState)
    {
        // 1. 포획(Captured) 상태로 진입할 때만 -> down 트리거 발사 (Dead 상태는 완전히 무시)
        if (newState == PlayerState.Captured)
        {
            animator.SetTrigger(HashDown);
        }
        // 2. 포획(Captured) 상태에서 다시 정상(Normal)으로 구출될 때 -> recover 트리거 발사
        else if (newState == PlayerState.Normal && oldState == PlayerState.Captured)
        {
            // 애니메이터에서 Die(또는 Down) -> Move로 돌아가는 'recover' 트리거 작동
            animator.SetTrigger(HashRecover);
        }
    }
}