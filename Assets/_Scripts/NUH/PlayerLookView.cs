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

    /// <summary>
    /// 플레이어 컨트롤러와 KCC 참조를 연결하고 카메라 설정을 초기화한다.
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        _motor = controller != null ? controller.KCCMotor : null;

        ResolveReferences();
        ApplyAuthorityOnlyPresentation();
        ValidateSetup();
    }

    /// <summary>
    /// 매 프레임 공용 시선 표현과 로컬 카메라 표현을 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!IsReady())
            return;

        ApplySharedLookPose();
        ApplyAuthorityOnlyPresentation();
    }

    /// <summary>
    /// 카메라 홀더와 KCC 참조가 유효한지 검사한다.
    /// </summary>
    private bool IsReady()
    {
        return _controller != null && _motor != null && _motor.KCC != null && cameraHolder != null;
    }

    /// <summary>
    /// Inspector에서 비어 있을 수 있는 카메라 참조를 자동으로 찾는다.
    /// </summary>
    private void ResolveReferences()
    {
        if (playerCamera == null && cameraHolder != null)
            playerCamera = cameraHolder.GetComponentInChildren<Camera>(true);
    }

    /// <summary>
    /// 필수 참조가 비어 있을 때 디버그 로그를 출력한다.
    /// </summary>
    private void ValidateSetup()
    {
        if (cameraHolder == null)
            Debug.LogError("[PlayerLookView] cameraHolder가 비어 있습니다.", this);

        if (cameraLightRoot == null)
            Debug.LogWarning("[PlayerLookView] cameraLightRoot가 비어 있습니다. 손전등 라이트 루트를 연결하세요.", this);
    }

    /// <summary>
    /// 모든 클라이언트에서 동일하게 보여야 하는 시선 방향 / 높이 표현을 적용한다.
    /// </summary>
    private void ApplySharedLookPose()
    {
        ApplyCameraHolderRotation();
        ApplyCameraHolderHeight();
    }

    /// <summary>
    /// KCC의 pitch 값을 읽어 cameraHolder 로컬 회전을 맞춘다.
    /// </summary>
    private void ApplyCameraHolderRotation()
    {
        Vector2 pitchRotation = _motor.KCC.GetLookRotation(true, false);
        cameraHolder.localRotation = Quaternion.Euler(pitchRotation.x, 0f, 0f);
    }

    /// <summary>
    /// 현재 상태에 맞는 눈높이를 계산해 cameraHolder 높이를 갱신한다.
    /// </summary>
    private void ApplyCameraHolderHeight()
    {
        Vector3 localPos = cameraHolder.localPosition;
        localPos.y = GetCurrentEyeHeight();
        cameraHolder.localPosition = localPos;
    }

    /// <summary>
    /// 현재 상태에 맞는 눈높이를 반환한다.
    /// 책상 은신은 crouch 높이, 포획 Active는 누운 시점용 높이를 사용한다.
    /// </summary>
    private float GetCurrentEyeHeight()
    {
        if (_controller != null)
        {
            if (_controller.NetHideState == HideState.Desk)
                return _motor.CrouchHeight - eyeOffset;

            if (_controller.NetPlayerState == PlayerState.Captured && _controller.NetCapturePhase == CapturePhase.Active)
            {
                // [Capture Presentation] 실제 누운 카메라 회전 / 흔들림 / 포즈 보정은 나중에 여기서 확장.
                return capturedEyeHeight;
            }
        }

        bool isCrouching = _motor.IsCrouching;
        float baseHeight = isCrouching ? _motor.CrouchHeight : _motor.StandHeight;
        return baseHeight - eyeOffset;
    }

    /// <summary>
    /// 로컬 플레이어에게만 필요한 카메라 enable / 커서 잠금 표현을 적용한다.
    /// </summary>
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
    /// 손전등 뷰가 사용할 카메라 라이트 루트를 반환한다.
    /// </summary>
    public Transform GetCameraLightRoot()
    {
        return cameraLightRoot;
    }
}
