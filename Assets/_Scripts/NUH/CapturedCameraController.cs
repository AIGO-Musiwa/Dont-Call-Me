using UnityEngine;

/// <summary>
/// 포획 활성 상태에서 로컬 플레이어의 포획 카메라 pivot만 회전시키는 컨트롤러.
/// Player root, KCC, 모델 방향은 회전시키지 않고 CapturedLookPivot만 제한 각도 안에서 움직인다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CapturedCameraController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform capturedLookPivot; // 포획 상태에서 마우스 입력으로 회전할 기준 pivot
    [SerializeField] private InputHandler inputHandler;   // 로컬 입력을 제공하는 씬 입력 수집기

    [Header("회전 제한")]
    [SerializeField] private float yawMin = -70f;          // 포획 카메라 좌측 최대 각도
    [SerializeField] private float yawMax = 70f;           // 포획 카메라 우측 최대 각도
    [SerializeField] private float pitchMin = -45f;        // 포획 카메라 아래쪽 최대 각도
    [SerializeField] private float pitchMax = 45f;         // 포획 카메라 위쪽 최대 각도
    [SerializeField] private float lookSensitivity = 0.2f; // 포획 카메라 전용 시야 감도 보정값
    [SerializeField] private bool resetWhenEnterCaptured = true; // 포획 활성 진입 시 시야를 정면으로 초기화할지 여부
    [SerializeField] private bool resetWhenExitCaptured = true;  // 포획 종료 시 pivot 회전을 초기화할지 여부

    private PlayerController _controller;  // 이 카메라 컨트롤러를 소유한 플레이어
    private bool _wasCapturedActive;        // 이전 프레임의 포획 활성 상태
    private float _yaw;                     // 포획 pivot의 현재 좌우 회전값
    private float _pitch;                   // 포획 pivot의 현재 상하 회전값

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        ResolveReferences();
        CacheCurrentPivotRotation();
    }

    private void LateUpdate()
    {
        if (!CanRunLocalCapturedCamera())
        {
            HandleCapturedExitIfNeeded();
            return;
        }

        HandleCapturedEnterIfNeeded();
        ApplyCapturedLookInput();
    }

    /// <summary>
    /// 필요한 참조를 자동으로 찾는다.
    /// capturedLookPivot을 비워두면 CapturedCameraTarget 자체를 임시 회전 기준으로 사용한다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_controller != null && capturedLookPivot == null)
        {
            Transform capturedTarget = _controller.CapturedCameraTarget;
            if (capturedTarget != null)
                capturedLookPivot = capturedTarget;
        }

        if (inputHandler == null)
            inputHandler = Object.FindFirstObjectByType<InputHandler>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 현재 클라이언트가 조종하는 플레이어이고 포획 활성 상태일 때만 포획 카메라 입력을 처리한다.
    /// </summary>
    private bool CanRunLocalCapturedCamera()
    {
        if (_controller == null || !_controller.HasInputAuthority)
            return false;

        if (capturedLookPivot == null)
            return false;

        return _controller.NetPlayerState == PlayerState.Captured &&
               _controller.NetCapturePhase == CapturePhase.Active;
    }

    /// <summary>
    /// 포획 활성 상태에 처음 들어온 프레임의 초기화를 처리한다.
    /// </summary>
    private void HandleCapturedEnterIfNeeded()
    {
        if (_wasCapturedActive)
            return;

        _wasCapturedActive = true;

        if (resetWhenEnterCaptured)
            ResetPivotRotation();
        else
            CacheCurrentPivotRotation();
    }

    /// <summary>
    /// 포획 활성 상태에서 벗어난 순간의 초기화를 처리한다.
    /// </summary>
    private void HandleCapturedExitIfNeeded()
    {
        if (!_wasCapturedActive)
            return;

        _wasCapturedActive = false;

        if (resetWhenExitCaptured)
            ResetPivotRotation();
    }

    /// <summary>
    /// 로컬 마우스 입력으로 포획 카메라 pivot만 회전시킨다.
    /// </summary>
    private void ApplyCapturedLookInput()
    {
        if (inputHandler == null)
            ResolveReferences();

        if (inputHandler == null)
            return;

        if (ESCUI.IsOpen)
            return;

        Vector2 lookDelta = inputHandler.FrameLookDelta * lookSensitivity;

        _yaw = Mathf.Clamp(_yaw + lookDelta.x, yawMin, yawMax);
        _pitch = Mathf.Clamp(_pitch - lookDelta.y, pitchMin, pitchMax);

        ApplyPivotRotation();
    }

    /// <summary>
    /// 기본 자세 회전값과 저장된 yaw/pitch 값을 포획 카메라 pivot의 localRotation에 적용한다.
    /// </summary>
    private void ApplyPivotRotation()
    {
        if (capturedLookPivot == null)
            return;

        Quaternion lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);

        capturedLookPivot.localRotation = lookRotation;
    }

    /// <summary>
    /// 현재 pivot 회전값을 yaw/pitch 변수에 반영한다.
    /// 기본 회전값은 별도로 적용되므로 입력 회전값만 초기 기준으로 사용한다.
    /// </summary>
    private void CacheCurrentPivotRotation()
    {
        if (capturedLookPivot == null)
            return;

        _pitch = 0f;
        _yaw = 0f;
    }

    /// <summary>
    /// 포획 카메라 pivot을 기본 자세 기준의 정면으로 초기화한다.
    /// </summary>
    private void ResetPivotRotation()
    {
        _yaw = 0f;
        _pitch = 0f;
        ApplyPivotRotation();
    }

    /// <summary>
    /// 0~360도 각도를 -180~180도 범위로 변환한다.
    /// 현재 구조에서는 기본 회전값을 별도로 사용하므로 필요 시 보조 계산에 사용한다.
    /// </summary>
    private float NormalizeSignedAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }
}