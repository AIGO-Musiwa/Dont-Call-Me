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

    [Header("손전등 라이트 위치")]
    [SerializeField] private float flashlightHeightOffset = 2f; // 플레이어 발 위치 기준 라이트 높이
    [SerializeField] private float flashlightForwardOffset = 0.5f; // 플레이어 몸 기준 앞쪽 거리

    [Header("카메라 스무딩")]
    [SerializeField] private float heightSmoothTime = 0.2f; // 카메라 높이 전환 시간

    private float _heightVelocity; // SmoothDamp 내부 속도값

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

        if (SettingsManager.IsOpen && _controller.HasInputAuthority)
            return;

        ApplySharedLookPose();
        ApplyCameraLightRootPose();
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
        float targetHeight = GetCurrentEyeHeight();

        localPos.y = Mathf.SmoothDamp(
            localPos.y,
            targetHeight,
            ref _heightVelocity,
            heightSmoothTime
        );

        cameraHolder.localPosition = localPos;
    }

    private float GetCurrentEyeHeight()
    {
        if (_controller != null)
        {
            if (_controller.NetHideState == HideState.Desk)
                return _motor.CrouchHeight - eyeOffset;

            if (_controller.NetPlayerState == PlayerState.Captured &&
                _controller.NetCapturePhase == CapturePhase.Active)
                return capturedEyeHeight;
        }

        bool isCrouching = _motor.IsCrouching;
        float baseHeight = isCrouching ? _motor.CrouchHeight : _motor.StandHeight;

        return baseHeight - eyeOffset;
    }

    /// <summary>
    /// 손전등 SpotLight가 따라갈 기준 위치와 회전을 갱신한다.
    /// 위치는 플레이어 몸 기준 높이/앞뒤 offset을 사용하고,
    /// 회전은 실제 카메라가 보는 방향을 사용한다.
    /// </summary>
    private void ApplyCameraLightRootPose()
    {
        if (cameraLightRoot == null || playerCamera == null)
            return;

        Vector3 targetPosition =
            transform.position +
            Vector3.up * flashlightHeightOffset +
            transform.forward * flashlightForwardOffset;

        cameraLightRoot.position = targetPosition;
        cameraLightRoot.rotation = playerCamera.transform.rotation;
    }

    private void ApplyAuthorityOnlyPresentation()
    {
        bool hasInputAuthority = _controller != null && _controller.HasInputAuthority;
        bool shouldEnableFirstPersonCamera =
            hasInputAuthority &&
            (_controller == null || !_controller.IsSpectatorState());

        if (playerCamera != null)
            playerCamera.enabled = shouldEnableFirstPersonCamera;

        if (!lockCursorForLocalPlayer)
            return;

        if (hasInputAuthority)
        {
            if (SettingsManager.IsOpen)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// 손전등 라이트가 붙을 기준 Transform을 반환한다.
    /// 실제 위치/회전 갱신은 LateUpdate의 ApplyCameraLightRootPose에서 처리한다.
    /// </summary>
    public Transform GetCameraLightRoot()
    {
        return cameraLightRoot;
    }
}