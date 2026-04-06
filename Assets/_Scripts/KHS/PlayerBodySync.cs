using UnityEngine;

/// <summary>
/// [기공사 전용] 네트워크 동기화 바디 리깅 모듈.
/// 모든 클라이언트에서 KCC의 회전 데이터를 읽어 상체 뼈대를 제어함.
/// </summary>
public class PlayerBodySync : MonoBehaviour
{
    [Header("참조 장치")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private Transform cameraHolder;

    [Header("관절 뼈대")]
    [SerializeField] private Transform spineBone;
    [SerializeField] private Transform neckBone;
    [SerializeField] private Transform headBone;

    [Header("회전 가중치")]
    [Range(0, 1)][SerializeField] private float spineWeight = 0.3f;
    [Range(0, 1)][SerializeField] private float neckWeight = 0.5f;

    public void Initialize(PlayerController controllerRef)
    {
        controller = controllerRef;
    }

    // 애니메이션이 뼈대를 다 움직인 직후에 실행되어야 하므로 LateUpdate 사용
    private void LateUpdate()
    {
        // 0. 필수 부품 체크
        if (controller == null || controller.KCCMotor == null || headBone == null) return;

        if (cameraHolder != null)
        {
            // [수정] 뼈 위치를 '직대입'하지 않고 부동소수점 오차나 애니메이션 잔떨림을 Lerp로 걸러냄
            // 0.15f 정도의 속도로 따라가게 하면, 숨쉬기 애니메이션의 미세한 떨림은 흡수하고
            // 이동이나 앉기 같은 굵직한 움직임은 부드럽게 따라감 (스태빌라이저 효과)
            cameraHolder.position = Vector3.Lerp(cameraHolder.position, headBone.position, Time.deltaTime * 20f);
        }

        // 2. [핵심] 네트워크로 동기화된 시선 각도(Pitch) 수신
        // SimpleKCC가 자동으로 동기화해주는 값을 사용하므로 모든 유저 화면에서 동일함.
        float pitch = controller.KCCMotor.KCC.GetLookRotation(true, false).x;

        // 유니티 각도 체계(0~360)를 -180~180 체계로 변환
        if (pitch > 180) pitch -= 360;

        // 3. 사망 상태 체크 (사망 시에는 뼈대 제어를 멈춤)
        if (controller.NetPlayerState == PlayerState.Dead)
        {
            if (cameraHolder != null) cameraHolder.rotation = headBone.rotation;
            return;
        }

        // 4. 관절 분할 회전 적용 (상대방 화면에서도 동기화됨)
        if (spineBone != null)
        {
            spineBone.localRotation *= Quaternion.Euler(pitch * spineWeight, 0, 0);
        }

        if (neckBone != null)
        {
            neckBone.localRotation *= Quaternion.Euler(pitch * neckWeight, 0, 0);
        }
    }
}