using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;

[RequireComponent(typeof(SimpleKCC))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerKCCMotor : MonoBehaviour
{
    [Header("Move Speed")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 2f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.2f;
    [SerializeField] private float pitchMin = -80f;
    [SerializeField] private float pitchMax = 80f;

    [Header("Gravity")]
    [SerializeField] private float gravityMultiplier = 2f;

    [Header("Height")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;

    private PlayerController _controller;
    private SimpleKCC _simpleKCC;
    private Rigidbody _rigidbody;

    private PlayerDebuffHandler _debuffHandler;

    private bool _initialized;

    public SimpleKCC KCC => _simpleKCC;

    /// <summary>
    /// 현재 실제 crouch 상태.
    /// NetIsCrouching은 입력 crouch뿐 아니라 Desk 은신 강제 crouch까지 포함한다.
    /// </summary>
    public bool IsCrouching => _controller != null && _controller.NetIsCrouching;

    public float StandHeight => standHeight;
    public float CrouchHeight => crouchHeight;

    /// <summary>
    /// 플레이어 컨트롤러와 KCC를 연결하고 초기 중력/높이를 세팅한다.
    /// </summary>
    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        _simpleKCC = GetComponent<SimpleKCC>();
        _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody != null)
            _rigidbody.isKinematic = true;

        if (_simpleKCC != null)
        {
            _simpleKCC.SetGravity(Physics.gravity.y * gravityMultiplier);
            ApplyKCCHeight(false); // 시작은 서 있는 높이
        }

        _initialized = true;
    }

    /// <summary>
    /// 입력을 받아 시야 / 높이 / 이동을 적용한다.
    /// 메인 상태와 은신 상태에 따라 이동 가능 여부를 제한한다.
    /// </summary>
    public void Simulate(PlayerNetworkInput input, bool movementLocked, bool lookLocked)
    {
        if (!_initialized || _simpleKCC == null)
            return;

        bool canUseNormalLook = CanUseNormalLook(lookLocked);

        if (canUseNormalLook)
            ApplyLook(input);

        bool canMove = CanMove(movementLocked);

        UpdateCrouchState(input, canMove);
        ApplyMove(input, canMove);
    }

    /// <summary>
    /// 지정한 월드 위치/회전으로 KCC를 즉시 워프한다.
    /// 위치는 SimpleKCC 기준으로 동기화하고, 회전은 현재 pitch를 유지한 채 yaw만 맞춘다.
    /// </summary>
    public void WarpToPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (!_initialized || _simpleKCC == null)
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (_rigidbody != null)
            {
                _rigidbody.position = worldPosition;
                _rigidbody.rotation = worldRotation;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            return;
        }

        Vector2 currentLook = _simpleKCC.GetLookRotation(true, true);
        float preservedPitch = currentLook.x;
        float targetYaw = NormalizeSignedAngle(worldRotation.eulerAngles.y);

        _simpleKCC.SetPosition(worldPosition);
        _simpleKCC.SetLookRotation(preservedPitch, targetYaw);

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 은신 상태 변경 직후 KCC 높이를 즉시 다시 맞추고 싶을 때 사용한다.
    /// 현재는 Desk 은신이면 crouch height, 그 외에는 stand height로 맞춘다.
    /// 필요하면 PlayerController.ServerEnterHide / ServerExitHide에서 호출할 수 있다.
    /// </summary>
    public void RefreshCrouchHeightFromHideState()
    {
        if (!_initialized || _simpleKCC == null || _controller == null)
            return;

        bool forcedCrouch = IsForcedCrouchByState();

        _controller.NetIsCrouching = forcedCrouch;
        ApplyKCCHeight(forcedCrouch);
    }

    /// <summary>
    /// 0~360도 각도를 -180~180 범위의 signed 각도로 변환한다.
    /// </summary>
    private float NormalizeSignedAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }


    /// <summary>
    /// Normal 상태에서만 KCC LookRotation을 갱신할 수 있는지 판단한다.
    /// Captured 상태의 시야 회전은 CapturedCameraController가 별도 pivot으로 처리한다.
    /// </summary>
    private bool CanUseNormalLook(bool lookLocked)
    {
        if (lookLocked)
            return false;

        if (_controller == null)
            return true;

        return _controller.NetPlayerState == PlayerState.Normal;
    }

    /// <summary>
    /// 마우스 입력을 사용해 KCC LookRotation을 갱신한다.
    /// </summary>
    private void ApplyLook(PlayerNetworkInput input)
    {
        Vector2 lookDelta = new Vector2(
            -input.LookInput.y * lookSensitivity,
             input.LookInput.x * lookSensitivity
        );

        _simpleKCC.AddLookRotation(lookDelta, pitchMin, pitchMax);
    }

    /// <summary>
    /// 상태와 입력을 기준으로 crouch 여부와 KCC 높이를 갱신한다.
    /// 
    /// 입력 crouch:
    /// - 이동 가능한 Normal 상태에서 Ctrl을 누를 때
    /// 
    /// 강제 crouch:
    /// - 책상 은신 상태일 때
    /// - 이동은 잠겨 있어도 collider는 낮아져야 한다.
    /// </summary>
    private void UpdateCrouchState(PlayerNetworkInput input, bool canMove)
    {
        bool inputCrouch = canMove && input.Buttons.IsSet(InputButtons.Crouch);
        bool forcedCrouch = IsForcedCrouchByState();

        bool shouldCrouch = inputCrouch || forcedCrouch;

        if (_controller != null)
            _controller.NetIsCrouching = shouldCrouch;

        ApplyKCCHeight(shouldCrouch);
    }

    /// <summary>
    /// 현재 플레이어 상태상 강제로 crouch height를 써야 하는지 반환한다.
    /// </summary>
    private bool IsForcedCrouchByState()
    {
        if (_controller == null)
            return false;

        return _controller.NetHideState == HideState.Desk;
    }

    /// <summary>
    /// 실제 SimpleKCC 높이를 적용한다.
    /// </summary>
    private void ApplyKCCHeight(bool isCrouching)
    {
        if (_simpleKCC == null)
            return;

        _simpleKCC.SetHeight(isCrouching ? crouchHeight : standHeight);
    }

    /// <summary>
    /// 현재 입력과 상태를 기준으로 이동 속도를 계산해 KCC에 적용한다.
    /// </summary>
    private void ApplyMove(PlayerNetworkInput input, bool canMove)
    {
        Vector3 moveVelocity = Vector3.zero;

        if (canMove)
        {
            float speed = GetCurrentSpeed(input);

            Vector3 moveDirection =
                _simpleKCC.TransformRotation * new Vector3(input.MoveInput.x, 0f, input.MoveInput.y);

            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();

            moveVelocity = moveDirection * speed;
        }

        _simpleKCC.Move(moveVelocity, 0f);
    }

    /// <summary>
    /// 현재 상태에서 이동이 가능한지 판단한다.
    /// </summary>
    private bool CanMove(bool movementLocked)
    {
        if (_controller == null)
            return !movementLocked;

        if (movementLocked)
            return false;

        if (_controller.NetPlayerState != PlayerState.Normal)
            return false;

        if (_controller.NetHideState != HideState.None)
            return false;

        return true;
    }

    /// <summary>
    /// 현재 입력 상태에 맞는 이동 속도를 반환한다.
    /// </summary>
    private float GetCurrentSpeed(PlayerNetworkInput input)
    {
        float baseSpeed;

        if (IsCrouching)
            baseSpeed = crouchSpeed;
        else if (input.Buttons.IsSet(InputButtons.Sprint))
            baseSpeed = runSpeed;
        else
            baseSpeed = walkSpeed;

        // 서브 크리처 이속 감소 디버프 배율 적용
        float multiplier = _debuffHandler != null ? _debuffHandler.NetSpeedMultiplier : 1f;
        return baseSpeed * multiplier;
    }
}