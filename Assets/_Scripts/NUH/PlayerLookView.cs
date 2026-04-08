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
    [SerializeField] private float capturedEyeHeight = 0.45f;

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

        ApplySharedLookPose();
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
        if (_controller != null)
        {
            if (_controller.NetHideState == HideState.Desk)
                return _motor.CrouchHeight - eyeOffset;

            if (_controller.NetPlayerState == PlayerState.Captured && _controller.NetCapturePhase == CapturePhase.Active)
                return capturedEyeHeight;
        }

        bool isCrouching = _motor.IsCrouching;
        float baseHeight = isCrouching ? _motor.CrouchHeight : _motor.StandHeight;
        return baseHeight - eyeOffset;
    }

    private void ApplyAuthorityOnlyPresentation()
    {
        bool hasInputAuthority = _controller != null && _controller.HasInputAuthority;
        bool shouldEnableFirstPersonCamera = hasInputAuthority && (_controller == null || !_controller.IsSpectatorState());

        if (playerCamera != null)
            playerCamera.enabled = shouldEnableFirstPersonCamera;

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
