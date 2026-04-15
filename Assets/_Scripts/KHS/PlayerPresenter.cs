using UnityEngine;

/// <summary>
/// 내 화면뿐만 아니라, 다른 플레이어의 화면에 떠 있는 '나의 분신' 애니메이션도 
/// 서버의 NetIsCrouching 값을 보고 완벽하게 동기화합니다.
/// </summary>
public class PlayerPresenter : MonoBehaviour
{
    [Header("참조 부품")]
    [SerializeField] private PlayerController controller;

    [Header("애니메이션")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animator shadowAnimator;

    [Header("비주얼 세터")]
    [SerializeField] private PlayerVisualSetter visualSetter;

    private static readonly int HashX = Animator.StringToHash("x");
    private static readonly int HashY = Animator.StringToHash("y");
    private static readonly int HashIsCrouch = Animator.StringToHash("IsCrouch");

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

        var motor = controller.KCCMotor;
        if (motor == null || motor.KCC == null) return;

        // ─────────────────────────────────────────────────────────
        // 1. 앉기 상태 동기화 (네트워크 변수 직결)
        // ─────────────────────────────────────────────────────────
        // motor.IsCrouching을 써도 되지만, 
        // 명확하게 네트워크 변수인 controller.NetIsCrouching을 직접 읽어옵니다.
        // 이제 다른 플레이어(Proxy) 화면에서도 이 값이 실시간으로 동기화됩니다.
        bool syncCrouch = controller.NetIsCrouching;

        animator.SetBool(HashIsCrouch, syncCrouch);

        if (shadowAnimator != null)
        {
            shadowAnimator.SetBool(HashIsCrouch, syncCrouch);
        }

        // ─────────────────────────────────────────────────────────
        // 2. 이동 애니메이션 동기화
        // ─────────────────────────────────────────────────────────
        // KCC의 Velocity는 SimpleKCC가 내부적으로 이미 네트워크 동기화를 해줍니다.
        Vector3 localVelocity = transform.InverseTransformDirection(motor.KCC.RealVelocity);

        animator.SetFloat(HashX, localVelocity.x);
        animator.SetFloat(HashY, localVelocity.z);

        if (shadowAnimator != null)
        {
            shadowAnimator.SetFloat(HashX, localVelocity.x);
            shadowAnimator.SetFloat(HashY, localVelocity.z);
        }
    }
}