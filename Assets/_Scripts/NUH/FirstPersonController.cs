using Fusion;
using UnityEngine;
/// <summary>
/// 1인칭 이동/시야/앉기를 담당하는 핵심 컨트롤러입니다.
///
/// 중요한 설계 포인트:
/// - 실제 이동 계산은 FixedUpdateNetwork에서 수행
/// - 로컬 플레이어만 입력을 사용
/// - 원격 플레이어는 [Networked] 값으로 부드럽게 보간해서 보임
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    [Header("이동 속도")]
    [SerializeField] private float walkSpeed = 4f; // 기본 걷기 속도
    [SerializeField] private float runSpeed = 7f; // 달리기 속도
    [SerializeField] private float crouchSpeed = 2f; // 앉아서 이동하는 속도
    [Header("시야")]
    [SerializeField] private float mouseSensitivity = 2f; // 마우스 민감도
    [SerializeField] private float pitchMin = -85f; // 위아래 회전 최소 각도
    [SerializeField] private float pitchMax = 85f; // 위아래 회전 최대 각도
    [Header("물리")]
    [SerializeField] private float gravity = -20f; // 중력 가속도

    [Header("앉기")]
    [SerializeField] private float standHeight = 1.8f; // 서 있을 때 캡슐 높이
    [SerializeField] private float crouchHeight = 0.9f; // 앉았을 때 캡슐 높이
    [SerializeField] private float crouchCenterY_Stand = 0.9f; // 서 있을 때 center.y
    [SerializeField] private float crouchCenterY_Crouch = 0.45f; // 앉았을 때 center.y
    [Header("레퍼런스")]
    [SerializeField] private Transform cameraHolder; // 카메라 상하 회전 피벗
    [SerializeField] private Transform cameraLightRoot; // 손전등 라이트가 붙을 자리


    // 원격 플레이어를 보여주기 위한 네트워크 동기화 값
    [Networked] private Vector3 NetworkPosition { get; set; }
    [Networked] private Quaternion NetworkYaw { get; set; }
    [Networked] private float NetworkPitch { get; set; }


    // 게임 규칙 상태
    [Networked] public bool IsCrouching { get; private set; }
    [Networked] public bool IsMovementLocked { get; set; } // 포획/숨기 등으로 이동 잠금
    [Networked] public bool IsLookLocked { get; set; } // 시야 잠금 여부


    private CharacterController _cc; // Unity CharacterController 캐시
    private float _verticalVelocity; // 중력 누적 속도
    private float _pitch; // 로컬 상하 회전 누적값

    public Transform CameraLightRoot => cameraLightRoot;

    public bool CCEnabled => _cc.enabled;


    public override void Spawned()
    {
        _cc = GetComponent<CharacterController>();
        if (HasInputAuthority)
        {
            // 로컬 플레이어만 자신의 카메라를 켭니다.
            // 다른 사람 캐릭터 카메라까지 켜지면 화면이 꼬입니다.
            Camera cam = cameraHolder.GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        ApplyCrouchState(false);
    }
    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerNetworkInput input)) return;

        // 이동이 허용될 때만 이동/앉기 처리
        if (!IsMovementLocked)
        {
            HandleMovement(input);
            HandleCrouch(input.Buttons.IsSet(InputButtons.Crouch));
        }

        // 시야가 허용될 때만 마우스 회전 처리
        if (!IsLookLocked)
        {
            HandleLook(input);
        }

        ApplyGravity();

        // 현재 결과를 [Networked] 값에 기록해 원격 플레이어가 볼 수 있게 함
        NetworkPosition = transform.position;
        NetworkYaw = transform.rotation;
        NetworkPitch = _pitch;
    }
    public override void Render()
    {
        // 내 로컬 플레이어는 이미 직접 입력으로 움직이고 있으니
        // 여기서 다시 보간하면 오히려 이중 적용이 됩니다.
        if (HasInputAuthority) return;

        // 원격 플레이어만 부드럽게 보간해서 표시
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, Runner.DeltaTime * 15f);
        transform.rotation = Quaternion.Slerp(transform.rotation, NetworkYaw, Runner.DeltaTime * 15f);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(NetworkPitch, 0f, 0f);
    }

    private void HandleMovement(PlayerNetworkInput input)
    {
        if (!_cc.enabled) return;

        float speed = GetCurrentSpeed(input);

        // 로컬 입력을 월드 이동 방향으로 변환
        Vector3 moveDir = transform.right * input.MoveInput.x
        + transform.forward * input.MoveInput.y;

        moveDir = moveDir.normalized * speed;
        moveDir.y = _verticalVelocity;

        _cc.Move(moveDir * Runner.DeltaTime);
    }

    private void HandleLook(PlayerNetworkInput input)
    {
        if (!HasInputAuthority) return;

        float yaw = input.LookInput.x * mouseSensitivity;
        _pitch -= input.LookInput.y * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // 몸체는 좌우(yaw) 회전
        transform.Rotate(Vector3.up * yaw);

        // 카메라는 상하(pitch) 회전
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void HandleCrouch(bool crouchInput)
    {
        if (IsCrouching == crouchInput) return;
        ApplyCrouchState(crouchInput);
    }

    private void ApplyCrouchState(bool crouching)
    {
        IsCrouching = crouching;

        // CharacterController 캡슐 높이와 중심 변경
        _cc.height = crouching ? crouchHeight : standHeight;
        _cc.center = new Vector3(0f, crouching ? crouchCenterY_Crouch : crouchCenterY_Stand, 0f);

        // 카메라 높이도 같이 조정해서 앉은 느낌을 만듦
        if (cameraHolder != null)
        {
            Vector3 pos = cameraHolder.localPosition;
            pos.y = crouching ? crouchHeight - 0.1f : standHeight - 0.1f;
            cameraHolder.localPosition = pos;
        }
    }

    private void ApplyGravity()
    {
        if (!_cc.enabled) return;

        // 바닥에 붙어 있을 때 너무 큰 음수 속도가 누적되지 않게 보정
        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Runner.DeltaTime;
    }

    private float GetCurrentSpeed(PlayerNetworkInput input)
    {
        if (IsCrouching) return crouchSpeed;
        if (input.Buttons.IsSet(InputButtons.Sprint)) return runSpeed;
        return walkSpeed;
    }
    /// <summary>
    /// 외부 시스템(포획, 연출 등)이 이동/시야를 잠글 때 사용.
    /// </summary>
    public void LockInput(bool lockMovement, bool lockLook)
    {
        IsMovementLocked = lockMovement;
        IsLookLocked = lockLook;
        if (_cc != null)
            _cc.enabled = !lockMovement;
    }
    /// <summary>
    /// 캐비닛 안처럼 CharacterController 자체를 끄고 싶을 때 사용.
    /// </summary>
    public void SetCCEnabled(bool enabled)
    {
        if (_cc != null)
            _cc.enabled = enabled;
    }
    /// <summary>
    /// 손전등이 카메라 자식 위치를 찾아 붙을 때 사용.
    /// </summary>
    public Transform GetCameraLightRoot() => cameraLightRoot;
}
