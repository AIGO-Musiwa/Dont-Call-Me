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
        ApplySharedLookPose();

        // 2) 로컬 전용 표현
        // - 카메라 enable
        // - 커서 잠금
        ApplyAuthorityOnlyPresentation();
    }

    private bool IsReady()
    {
        return _controller != null && _motor != null && _motor.KCC != null && cameraHolder != null;
    }

    private void ResolveReferences()
    {
        if (playerCamera == null && cameraHolder != null)
            playerCamera = cameraHolder.GetComponentInChildren<Camera>(true);
    }

    private void ValidateSetup()
    {
        if (cameraHolder == null)
            Debug.LogError("[PlayerLookView] cameraHolder가 비어 있습니다.", this);

        if (cameraLightRoot == null)
            Debug.LogWarning("[PlayerLookView] cameraLightRoot가 비어 있습니다. 손전등 라이트 루트를 연결하세요.", this);
    }

    private void ApplySharedLookPose()
    {
        ApplyCameraHolderRotation();
        ApplyCameraHolderHeight();
    }

    private void ApplyCameraHolderRotation()
    {
        Vector2 pitchRotation = _motor.KCC.GetLookRotation(true, false);
        cameraHolder.localRotation = Quaternion.Euler(pitchRotation.x, 0f, 0f);
    }

    private void ApplyCameraHolderHeight()
    {
        Vector3 localPos = cameraHolder.localPosition;
        localPos.y = GetCurrentEyeHeight();
        cameraHolder.localPosition = localPos;
    }

    private float GetCurrentEyeHeight()
    {
        // 현재 구조 기준으로는 motor의 crouch 상태를 사용
        // 나중에 원격 crouch까지 더 정확히 맞추고 싶으면 crouch 상태를 렌더 가능한 값으로 분리하는게 더 안전함
        bool isCrouching = _motor.IsCrouching;
        float baseHeight = isCrouching ? _motor.CrouchHeight : _motor.StandHeight;
        return baseHeight - eyeOffset;
    }

    private void ApplyAuthorityOnlyPresentation()
    {
        bool hasInputAuthority = _controller != null && _controller.HasInputAuthority;

        if (playerCamera != null)
            playerCamera.enabled = hasInputAuthority;

        if (!lockCursorForLocalPlayer)
            return;
        
        if (hasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public Transform GetCameraLightRoot()
    {
        return cameraLightRoot;
    }
}