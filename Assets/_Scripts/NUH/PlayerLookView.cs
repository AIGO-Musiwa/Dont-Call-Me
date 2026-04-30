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
    [SerializeField] private float lightRootOffset = 0.3f;

    // 🛠️ 추가된 카메라 스무딩 부품
    [Header("카메라 스무딩 (Camera Smoothing)")]
    [SerializeField] private float heightSmoothTime = 0.2f; // 전환에 걸리는 시간 (0.2초 추천)
    private float _heightVelocity; // SmoothDamp가 내부적으로 사용할 현재 속도

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

        if (SettingsManager.IsOpen) return;

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
        float targetHeight = GetCurrentEyeHeight();

        // 🛠️ Mathf.SmoothDamp를 사용해 현재 높이에서 목표 높이로 부드럽게 이동
        localPos.y = Mathf.SmoothDamp(
            localPos.y,           // 현재 위치
            targetHeight,         // 목표 위치
            ref _heightVelocity,  // 현재 속도 (엔진이 알아서 계산함)
            heightSmoothTime      // 도달하는 데 걸리는 시간 (인스펙터에서 조절)
        );

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

    /// <summary>
    /// lightRootOffset 만큼 카메라 transform의 y 위치를 낮춘 위치 반환 (손전등 라이트 루트용)
    /// </summary>
    public Transform GetCameraLightRoot()
    {
        
        if (cameraLightRoot == null || playerCamera == null)
            return null;
        Vector3 offset = new Vector3(0f, -lightRootOffset, 0f);
        cameraLightRoot.position = playerCamera.transform.position + offset;
        cameraLightRoot.rotation = playerCamera.transform.rotation;
        return cameraLightRoot;
    }
}
