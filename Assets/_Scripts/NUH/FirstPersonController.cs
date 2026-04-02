using Fusion;
using UnityEngine;

/// <summary>
/// 1인칭 이동 / 시야 / 앉기를 담당하는 플레이어 컨트롤러.
///
/// 이 버전의 핵심 구조:
/// 1. 로컬 시야는 LateUpdate()에서 프레임 단위로 즉시 반응
/// 2. 네트워크 시뮬레이션용 yaw / pitch는 FixedUpdateNetwork()에서 GetInput()로 처리
/// 3. 원격 플레이어는 Render()에서 [Networked] 값 보간
///
/// 왜 이렇게 나누는가?
/// - 로컬 화면은 마우스 움직임에 즉시 반응해야 덜 끊겨 보임
/// - 하지만 네트워크 상태는 틱 단위로 확정되어야 예측/동기화가 안정적임
/// - 원격 플레이어는 Render 보간으로 충분히 자연스럽게 보일 수 있음
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    // ------------------------------------------------------------
    // Inspector 설정값
    // ------------------------------------------------------------
    [Header("이동 속도")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("시야")]
    [SerializeField] private float mouseSensitivity = 0.2f;
    [SerializeField] private float pitchMin = -80f;
    [SerializeField] private float pitchMax = 80f;

    [Header("물리")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("플레이어 높이")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float standCenterY = 1f;
    [SerializeField] private float crouchCenterY = 0.5f;

    [Header("레퍼런스")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraLightRoot;
    [SerializeField] private float eyeOffset = 0.1f;

    // ------------------------------------------------------------
    // [Networked] 동기화 값
    // ------------------------------------------------------------
    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkYaw { get; set; }
    [Networked] private float NetworkPitch { get; set; }

    [Networked] public bool IsCrouching { get; set; }
    [Networked] public bool IsMovementLocked { get; set; }
    [Networked] public bool IsLookLocked { get; set; }

    // ------------------------------------------------------------
    // 런타임 캐시
    // ------------------------------------------------------------
    private CharacterController _cc;
    private InputHandler _inputHandler;

    /// <summary>
    /// 중력 누적 속도.
    /// CharacterController는 Rigidbody가 아니므로 직접 누적해야 한다.
    /// </summary>
    private float _verticalVelocity;

    /// <summary>
    /// 틱 시뮬레이션용 yaw / pitch.
    /// 이동 방향, 네트워크 복제, 원격 표시 기준으로 사용된다.
    /// </summary>
    private float _simYaw;
    private float _simPitch;

    /// <summary>
    /// 로컬 렌더용 yaw / pitch.
    /// LateUpdate()에서 프레임 단위로 즉시 반응하는 값이다.
    /// </summary>
    private float _renderYaw;
    private float _renderPitch;

    // ------------------------------------------------------------
    // 외부 접근용 프로퍼티
    // ------------------------------------------------------------
    public Transform CameraLightRoot => cameraLightRoot;
    public bool CCEnabled => _cc != null && _cc.enabled;

    // ------------------------------------------------------------
    // Fusion 생명주기
    // ------------------------------------------------------------
    public override void Spawned()
    {
        _cc = GetComponent<CharacterController>();

        if (playerCamera == null && cameraHolder != null)
            playerCamera = cameraHolder.GetComponentInChildren<Camera>(true);

        if (HasInputAuthority && Runner != null)
            _inputHandler = Runner.GetComponent<InputHandler>();

        if (cameraHolder == null)
            Debug.LogError("[FirstPersonController] cameraHolder가 비어 있습니다.", this);

        // 초기 시뮬레이션 회전값 설정
        _simYaw = NormalizeAngle(transform.eulerAngles.y);
        _simPitch = 0f;

        // 로컬 렌더값도 동일하게 시작
        _renderYaw = _simYaw;
        _renderPitch = _simPitch;

        ApplyCrouchState(false);

        if (playerCamera != null)
            playerCamera.enabled = HasInputAuthority;

        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        NetworkPosition = transform.position;
        NetworkYaw = Quaternion.Euler(0f, _simYaw, 0f);
        NetworkPitch = _simPitch;
    }

    /// <summary>
    /// 네트워크 틱마다 호출.
    /// 이동 / 중력 / 네트워크용 회전은 여기서 처리한다.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerNetworkInput input))
            return;

        // 네트워크용 회전은 반드시 틱에서 처리
        if (!IsLookLocked)
            ApplySimulationLook(input.LookInput);

        // 이동은 네트워크용 yaw 기준
        if (!IsMovementLocked)
        {
            HandleCrouch(input.Buttons.IsSet(InputButtons.Crouch));
            HandleMovement(input);
        }

        ApplyGravity();

        // 시뮬레이션 결과를 transform과 [Networked] 값에 반영
        transform.rotation = Quaternion.Euler(0f, _simYaw, 0f);

        NetworkPosition = transform.position;
        NetworkYaw = transform.rotation;
        NetworkPitch = _simPitch;

        // 로컬 렌더값과 시뮬레이션값이 너무 벌어졌으면 보정
        // (일시적인 드리프트 누적 방지)
        if (HasInputAuthority)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(_renderYaw, _simYaw)) > 20f)
                _renderYaw = _simYaw;

            if (Mathf.Abs(_renderPitch - _simPitch) > 20f)
                _renderPitch = _simPitch;
        }
    }

    /// <summary>
    /// 로컬 플레이어는 LateUpdate에서 즉시 시야 반응을 준다.
    /// 
    /// 이유:
    /// - InputHandler.Update()가 이번 프레임의 FrameLookDelta를 갱신함
    /// - LateUpdate는 모든 Update 이후에 호출되므로,
    ///   가장 최신 프레임 마우스 델타를 안정적으로 읽을 수 있음
    /// </summary>
    private void LateUpdate()
    {
        if (!HasInputAuthority)
            return;

        if (IsLookLocked)
            return;

        if (_inputHandler == null)
            return;

        Vector2 frameLook = _inputHandler.FrameLookDelta;

        // 로컬 렌더용 회전은 프레임마다 즉시 반응
        _renderYaw += frameLook.x * mouseSensitivity;
        _renderPitch -= frameLook.y * mouseSensitivity;
        _renderPitch = Mathf.Clamp(_renderPitch, pitchMin, pitchMax);

        ApplyRenderLook(_renderYaw, _renderPitch);
    }

    /// <summary>
    /// 원격 플레이어는 [Networked] 값을 프레임 보간으로 보여준다.
    /// </summary>
    public override void Render()
    {
        if (HasInputAuthority)
            return;

        transform.position = Vector3.Lerp(transform.position, NetworkPosition, Runner.DeltaTime * 15f);
        transform.rotation = Quaternion.Slerp(transform.rotation, NetworkYaw, Runner.DeltaTime * 15f);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(NetworkPitch, 0f, 0f);
    }

    // ------------------------------------------------------------
    // 이동 처리
    // ------------------------------------------------------------
    private void HandleMovement(PlayerNetworkInput input)
    {
        if (_cc == null || !_cc.enabled)
            return;

        float currentSpeed = GetCurrentSpeed(input);

        // 이동 방향은 "현재 transform.forward"가 아니라
        // 시뮬레이션 yaw 기준으로 계산해야 local render yaw와 분리할 수 있다.
        Quaternion simRotation = Quaternion.Euler(0f, _simYaw, 0f);

        Vector3 move = (simRotation * Vector3.right) * input.MoveInput.x +
                       (simRotation * Vector3.forward) * input.MoveInput.y;

        move = move.normalized * currentSpeed;
        move.y = _verticalVelocity;

        _cc.Move(move * Runner.DeltaTime);
    }

    // ------------------------------------------------------------
    // 틱 시뮬레이션용 시야 처리
    // ------------------------------------------------------------
    private void ApplySimulationLook(Vector2 lookDelta)
    {
        _simYaw += lookDelta.x * mouseSensitivity;
        _simPitch -= lookDelta.y * mouseSensitivity;
        _simPitch = Mathf.Clamp(_simPitch, pitchMin, pitchMax);
    }

    // ------------------------------------------------------------
    // 로컬 렌더용 시야 처리
    // ------------------------------------------------------------
    private void ApplyRenderLook(float yaw, float pitch)
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ------------------------------------------------------------
    // 앉기 처리
    // ------------------------------------------------------------
    private void HandleCrouch(bool crouchInput)
    {
        if (IsCrouching == crouchInput)
            return;

        ApplyCrouchState(crouchInput);
    }

    private void ApplyCrouchState(bool crouching)
    {
        IsCrouching = crouching;

        if (_cc != null)
        {
            _cc.height = crouching ? crouchHeight : standHeight;
            _cc.center = new Vector3(0f, crouching ? crouchCenterY : standCenterY, 0f);
        }

        if (cameraHolder != null)
        {
            Vector3 localPos = cameraHolder.localPosition;
            localPos.y = crouching ? crouchHeight - eyeOffset : standHeight - eyeOffset;
            cameraHolder.localPosition = localPos;
        }
    }

    // ------------------------------------------------------------
    // 중력 처리
    // ------------------------------------------------------------
    private void ApplyGravity()
    {
        if (_cc == null || !_cc.enabled)
            return;

        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = groundedStickForce;

        _verticalVelocity += gravity * Runner.DeltaTime;
    }

    // ------------------------------------------------------------
    // 속도 계산
    // ------------------------------------------------------------
    private float GetCurrentSpeed(PlayerNetworkInput input)
    {
        if (IsCrouching)
            return crouchSpeed;

        if (input.Buttons.IsSet(InputButtons.Sprint))
            return runSpeed;

        return walkSpeed;
    }

    // ------------------------------------------------------------
    // 외부 제어
    // ------------------------------------------------------------
    public void LockInput(bool lockMovement, bool lockLook)
    {
        IsMovementLocked = lockMovement;
        IsLookLocked = lockLook;

        if (_cc != null)
            _cc.enabled = !lockMovement;
    }

    public void SetCCEnabled(bool enabled)
    {
        if (_cc != null)
            _cc.enabled = enabled;
    }

    public Transform GetCameraLightRoot()
    {
        return cameraLightRoot;
    }

    // ------------------------------------------------------------
    // 유틸리티
    // ------------------------------------------------------------
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}