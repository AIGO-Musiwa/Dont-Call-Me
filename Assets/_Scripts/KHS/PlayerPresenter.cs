using UnityEngine;
using UnityEngine.InputSystem.XR;

/// <summary>
/// 기존 컨트롤러의 데이터를 불러와 애니메이션과 시각 필터를 구동
/// </summary>
public class PlayerPresenter : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerController controller;

    [Header("애니메이션")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animator shadowAnimator;

    [Header("플레이어 분류에 따른 비쥬얼 세터")]
    [SerializeField] private PlayerVisualSetter visualSetter;

    //파라미터 해시
    private static readonly int HashX = Animator.StringToHash("x");
    private static readonly int HashY = Animator.StringToHash("y");
    private static readonly int HashIsCrouch = Animator.StringToHash("IsCrouch");

    private void Start()
    {
        //컨트롤러의 권한을 확인 1인칭 시각 필터
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

        // 1. 앉기 상태 동기화 (KCCMotor의 _isCrouching 데이터 활용)
        animator.SetBool(HashIsCrouch, motor.IsCrouching);
        shadowAnimator.SetBool(HashIsCrouch, motor.IsCrouching);

        // 2. 이동 애니메이션 동기화
        // SimpleKCC의 RealVelocity를 로컬 좌표계로 변환하여 x, y 값 추출
        Vector3 localVelocity = transform.InverseTransformDirection(motor.KCC.RealVelocity);

        // 애니메이터 파라미터 주입 (블렌드 트리용)
        animator.SetFloat(HashX, localVelocity.x);
        shadowAnimator.SetFloat(HashX, localVelocity.x);
        animator.SetFloat(HashY, localVelocity.z);
        shadowAnimator.SetFloat(HashY, localVelocity.z);
    }
}
