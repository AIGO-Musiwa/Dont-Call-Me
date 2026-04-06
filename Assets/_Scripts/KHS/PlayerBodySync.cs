using UnityEngine;

/// <summary>
/// PlayerLookView를 관축하여 상체각도 조절 및 사망등의 겨우 카메라 제어
/// </summary>
public class PlayerBodySync : MonoBehaviour
{
    [Header("연동 대상")]
    [SerializeField] private PlayerLookView lookView; // 연동할 PlayerLookView 참조
    [SerializeField] private Transform cameraHolder; // 카메라 홀더 참조 (PlayerLookView의 cameraHolder와 동일해야 함)

    [Header("뼈대")]
    [SerializeField] private Transform spineBone; // 상체 회전에 사용할 뼈대 (예: Spine1)
    [SerializeField] private Transform neckBone; // 목 회전에 사용할 뼈대 (예: Neck)
    [SerializeField] private Transform headBone; // 머리 회전에 사용할 뼈대 (예: Head)

    [Header("보정 수치")]
    [Range(0,1)][SerializeField] private float spineWeight = 0.3f; // 상체 회전에 적용할 가중치
    [Range(0,1)][SerializeField] private float neckWeight = 0.5f; // 목 회전에 적용할 가중치

    private bool isDead = false; // 플레이어 사망 여부

    public void SetDeathState(bool iDead)
    {
        isDead = iDead;

        //사망시 PlaterLookView의 카메라 제어 비활성화
        if(lookView != null) lookView.enabled = !isDead;
    }

    //유니티 실행 순서(Execution Order)에서 해당 스크립트가 PlayerLookView보다 늦게 실행되도록 설정
    private void LateUpdate()
    {
        // 0. 필수 부품 체크
        if (cameraHolder == null || headBone == null) return;

        // 1. [공통 공정] 위치 동기화: 어떤 상태든 카메라는 머리 뼈 위치를 물리적으로 추적함
        // 애니메이션으로 머리가 들썩이거나 앉아서 내려가면 카메라도 즉시 따라가게 돼.
        cameraHolder.position = headBone.position;

        // 2. 사망 시 특수 공정
        if (isDead)
        {
            // 사망 시에는 회전(Rotation)까지 뼈를 따라가서 시야가 바닥으로 구르게 만듦
            cameraHolder.rotation = headBone.rotation;
            return;
        }

        // 3. 일반 모드: 회전 제어 및 상체 동기화
        // 위치는 이미 위에서 뼈를 따르고 있으니, 여기서는 '회전'만 독립적으로 유지함 (스테디캠)

        // PlayerLookView가 이미 결정한 cameraHolder의 Pitch 값을 읽어옴
        float pitch = cameraHolder.localEulerAngles.x;
        if (pitch > 180) pitch -= 360;

        // 4. 상체 관절 분할 회전 (기존 로직 유지)
        if (spineBone != null)
        {
            // 척추 뼈에 카메라 각도의 일부를 배분
            spineBone.localRotation *= Quaternion.Euler(pitch * spineWeight, 0, 0);
        }

        if (neckBone != null)
        {
            // 목 뼈에 카메라 각도의 일부를 배분
            neckBone.localRotation *= Quaternion.Euler(pitch * neckWeight, 0, 0);
        }
    }

}
