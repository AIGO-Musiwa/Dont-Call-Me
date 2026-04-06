using UnityEngine;

public class PlayerLookView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraLightRoot;

    [Header("View")]
    [SerializeField] private float eyeOffset = 0.1f;
    [SerializeField] private bool lockCursorForLocalPlayer = true;

    private PlayerController _controller;
    private PlayerKCCMotor _motor;

    


    public Camera ViewCamera => playerCamera;
    public Transform ViewOrigin => playerCamera != null ? playerCamera.transform : cameraHolder;

    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        _motor = controller != null ? controller.KCCMotor : null;

        ResolveReferences();
        CacheLightRootOffsetFromHolder();

        ApplyAuthorityOnlyPresentation();
        ValidateSetup();
    }

    private void LateUpdate()
    {
        if (!IsReady())
            return;

        // 1) 공용 표현
        // - 모든 클라이언트에서 cameraHolder pitch / height 갱신
        // - 원격 플레이어의 라이트 방향도 이 pose를 따라가야 함
        ApplyAuthorityOnlyPresentation();
        ValidateSetup();
    }

    public Transform GetCameraLightRoot()
    {
        return cameraLightRoot;
    }
}