using UnityEngine;

/// <summary>
/// [기공사 전용] 순수 시각 전용 상체 동기화 장치
/// 카메라는 PlayerLookView가 독립적으로 제어하므로, 이 스크립트는 
/// 오직 네트워크로 공유된 시선 각도(Pitch)를 기반으로 척추와 목뼈만 부드럽게 꺾어줍니다.
/// </summary>
public class PlayerBodySync : MonoBehaviour
{
    [Header("참조 장치")]
    [SerializeField] private PlayerController controller;

    // [제거됨] 더 이상 카메라 위치를 여기서 건드리지 않으므로 cameraHolder, headBone 삭제

    [Header("관절 뼈대 (Animator 하위 본)")]
    [SerializeField] private Transform spineBone;
    [SerializeField] private Transform neckBone;

    [Header("회전 가중치")]
    [Tooltip("허리가 꺾이는 비율")]
    [Range(0, 1)][SerializeField] private float spineWeight = 0.3f;
    [Tooltip("목이 꺾이는 비율")]
    [Range(0, 1)][SerializeField] private float neckWeight = 0.5f;

    [Header("보간(스무딩) 설정")]
    [Tooltip("뼈대가 각도를 따라가는 속도. 높을수록 즉각 반응, 낮을수록 부드러움.")]
    [SerializeField] private float rotationSmoothSpeed = 15f;

    // 현재 보간 중인 Pitch 각도를 저장할 변수
    private float currentPitch = 0f;

    public void Initialize(PlayerController controllerRef)
    {
        controller = controllerRef;
    }

    // 애니메이터가 기본 포즈를 잡은 직후(LateUpdate)에 관절을 꺾어줌
    private void LateUpdate()
    {
        // 0. 필수 부품 체크
        if (controller == null || controller.KCCMotor == null) return;

        // 1. 사망 상태 체크 (사망 시에는 뼈대를 꺾지 않음)
        if (controller.NetPlayerState == PlayerState.Dead) return;

        // 2. 네트워크로 동기화된 타겟 각도(Pitch) 수신
        float targetPitch = controller.KCCMotor.KCC.GetLookRotation(true, false).x;

        // 유니티 각도 체계(0~360)를 -180~180 체계로 변환
        if (targetPitch > 180) targetPitch -= 360;

        // 3. 부드러운 회전을 위한 Lerp 보간 (기계적인 뚝뚝 끊김 방지)
        // [수리 포인트] 카메라 위치가 아니라 '회전 각도' 자체를 부드럽게 만들어 뼈대에 주입!
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * rotationSmoothSpeed);

        // 4. 상체 관절에 각도 적용
        ApplyBoneRotation();
    }

    private void ApplyBoneRotation()
    {
        // Animator가 이미 적용한 기본 로컬 회전값에, 우리가 계산한 Pitch 각도를 곱해서 추가로 꺾어줌
        // 이렇게 하면 상대방 화면(혹은 내 그림자)에서 고개를 숙이고 드는 모습이 완벽하게 동기화됨!
        if (spineBone != null)
        {
            spineBone.localRotation *= Quaternion.Euler(currentPitch * spineWeight, 0, 0);
        }

        if (neckBone != null)
        {
            neckBone.localRotation *= Quaternion.Euler(currentPitch * neckWeight, 0, 0);
        }
    }
}