using UnityEngine;

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
        bool syncCrouch = controller.NetIsCrouching;
        animator.SetBool(HashIsCrouch, syncCrouch);

        // ─────────────────────────────────────────────────────────
        // 2. 이동 애니메이션 동기화
        // ─────────────────────────────────────────────────────────
        Vector3 localVelocity = transform.InverseTransformDirection(motor.KCC.RealVelocity);

        animator.SetFloat(HashX, localVelocity.x);
        animator.SetFloat(HashY, localVelocity.z);
    }
}