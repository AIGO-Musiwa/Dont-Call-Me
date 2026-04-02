using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Fusion에 전달할 플레이어 입력 구조체.
/// 
/// 역할:
/// - MoveInput  : WASD 이동 입력
/// - LookInput  : 마우스 시선 입력(틱 누적값)
/// - Buttons    : Sprint / Crouch / Interact / Walkie 같은 버튼 상태
/// 
/// Fusion에서는 로컬 입력을 그대로 쓰지 않고,
/// 이런 구조체로 정리해서 OnInput()에서 Runner에 전달한 뒤
/// FixedUpdateNetwork()에서 소비하는 구조가 기본이다.
/// </summary>
public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MoveInput;
    public Vector2 LookInput;
    public NetworkButtons Buttons;
}

/// <summary>
/// NetworkButtons 내부에서 사용할 버튼 인덱스 정의.
/// 각 버튼은 비트 플래그처럼 저장된다.
/// </summary>
public static class InputButtons
{
    public const int Sprint = 0; // Shift
    public const int Crouch = 1; // Ctrl
    public const int Interact = 2; // 좌클릭
    public const int Walkie = 3; // 우클릭
}

/// <summary>
/// Unity Input System 입력을 수집해서
/// Fusion이 사용하는 PlayerNetworkInput으로 빌드하는 입력 수집기.
///
/// 이 스크립트의 책임은 "입력을 읽어서 버퍼에 저장"하는 것까지다.
/// 실제 이동/시야 적용은 하지 않는다.
///
/// 중요한 설계 포인트:
/// 1. Move / Sprint / Crouch 등은 그대로 상태 저장
/// 2. Look(마우스 델타)는
///    - 프레임 즉시 반응용 : FrameLookDelta
///    - 네트워크 틱 전송용 : _lookInputAccumulated
///    로 나눠서 관리
///
/// 주의:
/// - 이 스크립트는 NetworkRunner와 같은 GameObject 또는 그 자식에 두는 것을 권장
/// - 그러면 Fusion이 StartGame() 시 INetworkRunnerCallbacks를 자동 등록한다
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference walkieAction;

    // ------------------------------------------------------------
    // 내부 입력 버퍼
    // ------------------------------------------------------------

    /// <summary>
    /// 이동 입력은 현재 프레임 상태 그대로 저장.
    /// </summary>
    private Vector2 _moveInput;

    /// <summary>
    /// 마우스 델타를 네트워크 틱까지 누적하는 버퍼.
    /// Update()에서 계속 더하고, OnInput()에서 전송 후 0으로 리셋한다.
    /// </summary>
    private Vector2 _lookInputAccumulated;

    /// <summary>
    /// "이번 프레임"의 마우스 델타.
    /// 로컬 플레이어가 LateUpdate()에서 즉시 시야 회전에 사용한다.
    /// </summary>
    public Vector2 FrameLookDelta { get; private set; }

    private bool _sprintPressed;
    private bool _crouchPressed;
    private bool _interactPressed;
    private bool _walkiePressed;

    // 추가: FusionCallbackHandler 구독 관리
    private FusionCallbackHandler _registeredHandler;

    /// <summary>
    /// 세션 시작 후 FusionMoveMentTestLauncher 또는 NetworkDebugStarter에서 호출.
    /// FusionCallbackHandler.Current가 생성된 시점 이후에 호출해야 한다.
    /// </summary>
    /// <param name="handler"></param>
    public void Initialize(FusionCallbackHandler handler)
    {
        if (_registeredHandler != null)
        {
            _registeredHandler.OnInputEvent -= HandleOnInput;
        }

        _registeredHandler = handler;
        _registeredHandler.OnInputEvent += HandleOnInput;
    }

    private void OnDestroy()
    {
        if (_registeredHandler != null)
        {
            _registeredHandler.OnInputEvent -= HandleOnInput;
        }
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        sprintAction.action.Enable();
        crouchAction.action.Enable();
        interactAction.action.Enable();
        walkieAction.action.Enable();

        // 단발성 입력은 performed / canceled 콜백으로 버퍼링
        interactAction.action.performed += OnInteractPerformed;
        walkieAction.action.performed += OnWalkiePerformed;
        interactAction.action.canceled += OnInteractCanceled;
        walkieAction.action.canceled += OnWalkieCanceled;
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        sprintAction.action.Disable();
        crouchAction.action.Disable();
        interactAction.action.Disable();
        walkieAction.action.Disable();

        interactAction.action.performed -= OnInteractPerformed;
        walkieAction.action.performed -= OnWalkiePerformed;
        interactAction.action.canceled -= OnInteractCanceled;
        walkieAction.action.canceled -= OnWalkieCanceled;
    }

    private void Update()
    {
        // 이동 입력은 현재 상태 그대로 사용
        _moveInput = moveAction.action.ReadValue<Vector2>();

        // 이번 프레임 마우스 델타
        Vector2 currentFrameLook = lookAction.action.ReadValue<Vector2>();

        // 로컬 즉시 반응용 저장
        FrameLookDelta = currentFrameLook;

        // 네트워크 틱 전송용 누적
        _lookInputAccumulated += currentFrameLook;

        _sprintPressed = sprintAction.action.IsPressed();
        _crouchPressed = crouchAction.action.IsPressed();
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx) => _interactPressed = true;
    private void OnWalkiePerformed(InputAction.CallbackContext ctx) => _walkiePressed = true;
    private void OnInteractCanceled(InputAction.CallbackContext ctx) => _interactPressed = false;
    private void OnWalkieCanceled(InputAction.CallbackContext ctx) => _walkiePressed = false;

    /// <summary>
    /// Fusion이 틱 입력을 요청할 때 호출.
    /// 
    /// 여기서 지금까지 누적한 입력을 PlayerNetworkInput으로 묶어 전달한다.
    /// LookInput은 틱 사이에 누적된 마우스 델타 전체를 보낸다.
    /// </summary>
    public void HandleOnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput
        {
            MoveInput = _moveInput,
            LookInput = _lookInputAccumulated,
            Buttons = BuildButtons()
        };

        input.Set(data);

        // 단발 입력은 한 틱 보낸 뒤 리셋
        _interactPressed = false;

        // 마우스 델타 누적값도 틱 전송 후 비움
        _lookInputAccumulated = Vector2.zero;
    }

    /// <summary>
    /// bool 입력들을 NetworkButtons 비트 플래그로 변환.
    /// </summary>
    private NetworkButtons BuildButtons()
    {
        NetworkButtons buttons = new NetworkButtons();
        buttons.Set(InputButtons.Sprint, _sprintPressed);
        buttons.Set(InputButtons.Crouch, _crouchPressed);
        buttons.Set(InputButtons.Interact, _interactPressed);
        buttons.Set(InputButtons.Walkie, _walkiePressed);
        return buttons;
    }
}