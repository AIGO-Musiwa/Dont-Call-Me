using UnityEngine;

/// <summary>
/// 플레이어의 시야 기준 Transform과 카메라 관련 기준점을 관리한다.
/// 실제 화면 출력 Camera는 씬의 LocalMainCamera가 담당하고,
/// 이 스크립트는 플레이어 프리팹 내부의 Normal/Captured 기준 Transform만 제공한다.
/// </summary>
public class PlayerLookView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraHolder;         // 플레이어 pitch 회전을 적용하는 기준 루트
    [SerializeField] private Transform normalCameraTarget;   // Normal 상태에서 시야, 상호작용, 손전등 방향의 기준점
    [SerializeField] private Transform capturedCameraTarget; // Captured 상태에서 포획 카메라가 따라갈 기준점
    [SerializeField] private Transform cameraLightRoot;      // 손전등 SpotLight가 붙어서 따라갈 기준 루트

    [Header("View")]
    [SerializeField] private float eyeOffset = 0.1f;               // KCC 높이에서 눈 위치를 살짝 낮추는 값
    [SerializeField] private bool lockCursorForLocalPlayer = true; // 로컬 플레이어 커서 잠금 여부
    [SerializeField] private float capturedEyeHeight = 0.45f;      // 포획 활성 상태에서 CameraHolder가 내려갈 높이

    [Header("손전등 라이트 위치")]
    [SerializeField] private Vector3 flashlightLocalOffset = new Vector3(0f, -0.35f, 0.2f); // 현재 시야 기준 로컬 위치 보정값

    [Header("카메라 스무딩")]
    [SerializeField] private float heightSmoothTime = 0.2f; // CameraHolder 높이 전환 시간

    private float _heightVelocity;        // SmoothDamp 내부 속도값

    private PlayerController _controller; // 소유 플레이어 컨트롤러
    private PlayerKCCMotor _motor;        // 플레이어 이동/시야 모터

    public Transform NormalCameraTarget => normalCameraTarget != null ? normalCameraTarget : cameraHolder;
    public Transform CapturedCameraTarget => capturedCameraTarget != null ? capturedCameraTarget : NormalCameraTarget;
    public Transform ViewOrigin => GetCurrentViewOrigin();

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

        if (ESCUI.IsOpen && _controller.HasInputAuthority)
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
        if (normalCameraTarget == null)
            normalCameraTarget = cameraHolder;

        if (capturedCameraTarget == null)
            capturedCameraTarget = normalCameraTarget;
    }

    private void ValidateSetup()
    {
        if (cameraHolder == null)
            Debug.LogError("[PlayerLookView] cameraHolder가 비어 있습니다.", this);

        if (normalCameraTarget == null)
            Debug.LogWarning("[PlayerLookView] normalCameraTarget이 비어 있습니다. CameraHolder를 임시 기준으로 사용합니다.", this);

        if (capturedCameraTarget == null)
            Debug.LogWarning("[PlayerLookView] capturedCameraTarget이 비어 있습니다. NormalCameraTarget을 임시 기준으로 사용합니다.", this);

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
    /// 현재 플레이어 상태에 맞는 시야 기준 Transform을 반환한다.
    /// Normal 상태는 NormalCameraTarget, Captured 활성 상태는 CapturedCameraTarget을 사용한다.
    /// </summary>
    private Transform GetCurrentViewOrigin()
    {
        if (_controller != null &&
            _controller.NetPlayerState == PlayerState.Captured &&
            _controller.NetCapturePhase == CapturePhase.Active)
        {
            return CapturedCameraTarget;
        }

        return NormalCameraTarget;
    }

    /// <summary>
    /// 손전등 SpotLight가 따라갈 기준 위치와 회전을 갱신한다.
    /// 위치와 회전은 현재 시야 기준 Transform을 따른다.
    /// flashlightLocalOffset은 시야 기준 로컬 좌표 보정값이며,
    /// y를 음수로 주면 눈보다 아래쪽, z를 양수로 주면 앞쪽에서 빛이 나가는 느낌을 만든다.
    /// </summary>
    private void ApplyCameraLightRootPose()
    {
        if (cameraLightRoot == null)
            return;

        Transform origin = ViewOrigin;
        if (origin == null)
            return;

        cameraLightRoot.position = origin.TransformPoint(flashlightLocalOffset);
        cameraLightRoot.rotation = origin.rotation;
    }

    /// <summary>
    /// 로컬 플레이어 전용 커서 잠금만 처리한다.
    /// 실제 Camera 활성화는 씬의 LocalCameraModeController와 ViewModelCamera 담당 스크립트가 처리한다.
    /// </summary>
    private void ApplyAuthorityOnlyPresentation()
    {
        bool hasInputAuthority = _controller != null && _controller.HasInputAuthority;

        if (!lockCursorForLocalPlayer)
            return;

        if (hasInputAuthority)
        {
            if (ESCUI.IsOpen)
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