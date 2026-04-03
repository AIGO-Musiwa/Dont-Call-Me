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

    private bool _initialized;
    private bool _isCrouching;

    public SimpleKCC KCC => _simpleKCC;
    public bool IsCrouching => _isCrouching;
    public float StandHeight => standHeight;
    public float CrouchHeight => crouchHeight;

    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        _simpleKCC = GetComponent<SimpleKCC>();
        _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody != null)
            _rigidbody.isKinematic = true;

        if (_simpleKCC != null)
        {
            // SetGravity는 float 하나를 받음
            // 내부에서 Vector3.up * gravity로 처리하므로
            // 아래 방향 중력을 원하면 음수값을 넘겨야 함
            _simpleKCC.SetGravity(Physics.gravity.y * gravityMultiplier);

            // 시작 높이 설정
            _simpleKCC.SetHeight(standHeight);
        }

        _isCrouching = false;
        _initialized = true;
    }

    public void Simulate(PlayerNetworkInput input, bool movementLocked, bool lookLocked)
    {
        if (!_initialized || _simpleKCC == null)
            return;

        // 1) Look 처리
        if (!lookLocked)
        {
            Vector2 lookDelta = new Vector2(
                -input.LookInput.y * lookSensitivity,
                 input.LookInput.x * lookSensitivity
            );

            _simpleKCC.AddLookRotation(lookDelta, pitchMin, pitchMax);
        }

        // 2) Crouch 처리
        bool wantsCrouch = !movementLocked && input.Buttons.IsSet(InputButtons.Crouch);
        if (wantsCrouch != _isCrouching)
        {
            _isCrouching = wantsCrouch;
            _simpleKCC.SetHeight(_isCrouching ? crouchHeight : standHeight);
        }

        // 3) Move 처리
        Vector3 moveVelocity = Vector3.zero;

        if (!movementLocked)
        {
            float speed = GetCurrentSpeed(input);

            Vector3 moveDirection =
                _simpleKCC.TransformRotation * new Vector3(input.MoveInput.x, 0f, input.MoveInput.y);

            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();

            moveVelocity = moveDirection * speed;
        }

        // Move(Vector3, float)
        _simpleKCC.Move(moveVelocity, 0f);
    }

    private float GetCurrentSpeed(PlayerNetworkInput input)
    {
        if (_isCrouching)
            return crouchSpeed;

        if (input.Buttons.IsSet(InputButtons.Sprint))
            return runSpeed;

        return walkSpeed;
    }
}