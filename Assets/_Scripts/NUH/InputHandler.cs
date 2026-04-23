using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fusion에 전달할 플레이어 입력 구조체.
/// 
/// 역할:
/// - MoveInput  : WASD 이동 입력
/// - LookInput  : 마우스 시선 입력(틱 누적값)
/// - ZoomInput  : 마우스 휠 입력(관전 줌용)
/// - Buttons    : Sprint / Crouch / Interact / Walkie 같은 버튼 상태
/// </summary>
public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MoveInput;
    public Vector2 LookInput;
    public float ZoomInput;
    public NetworkButtons Buttons;
}

/// <summary>
/// NetworkButtons 내부에서 사용할 버튼 인덱스 정의.
/// 각 버튼은 비트 플래그처럼 저장된다.
/// </summary>
public static class InputButtons
{
    public const int Sprint = 0;    // Shift
    public const int Crouch = 1;    // Ctrl
    public const int Interact = 2;  // 좌클릭
    public const int Walkie = 3;    // 우클릭
}

/// <summary>
/// Unity Input System 입력을 수집해서
/// Fusion이 사용하는 PlayerNetworkInput으로 빌드하는 입력 수집기.
/// 
/// 이 스크립트의 책임은 "입력을 읽어서 버퍼에 저장"하는 것까지다.
/// 실제 이동/시야 적용은 하지 않는다.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference zoomAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference walkieAction;

    private Vector2 _moveInput;
    private Vector2 _lookInputAccumulated;
    private float _zoomInput;

    /// <summary>
    /// 이번 프레임의 마우스 델타.
    /// 로컬 플레이어가 즉시 시야 회전에 사용할 수 있도록 공개한다.
    /// </summary>
    public Vector2 FrameLookDelta { get; private set; }

    private bool _sprintPressed;
    private bool _crouchPressed;
    private bool _interactPressed;
    private bool _walkiePressed;

    private FusionCallbackHandler _registeredHandler;

    /// <summary>
    /// FusionCallbackHandler에 OnInput 콜백을 연결한다.
    /// </summary>
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
        zoomAction.action.Enable();
        sprintAction.action.Enable();
        crouchAction.action.Enable();
        interactAction.action.Enable();
        walkieAction.action.Enable();

        interactAction.action.performed += OnInteractPerformed;
        walkieAction.action.performed += OnWalkiePerformed;
        interactAction.action.canceled += OnInteractCanceled;
        walkieAction.action.canceled += OnWalkieCanceled;
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        zoomAction.action.Disable();
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
        _moveInput = moveAction.action.ReadValue<Vector2>();

        Vector2 currentFrameLook = lookAction.action.ReadValue<Vector2>();
        FrameLookDelta = currentFrameLook;
        _lookInputAccumulated += currentFrameLook;

        // 마우스 휠은 한 프레임 단위 값이므로 누적하지 않고 현재값만 사용한다.
        _zoomInput = zoomAction.action.ReadValue<float>();

        _sprintPressed = sprintAction.action.IsPressed();
        _crouchPressed = crouchAction.action.IsPressed();
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx) => _interactPressed = true;
    private void OnWalkiePerformed(InputAction.CallbackContext ctx) => _walkiePressed = true;
    private void OnInteractCanceled(InputAction.CallbackContext ctx) => _interactPressed = false;
    private void OnWalkieCanceled(InputAction.CallbackContext ctx) => _walkiePressed = false;

    /// <summary>
    /// Fusion이 틱 입력을 요청할 때 누적된 입력을 PlayerNetworkInput으로 전달한다.
    /// </summary>
    public void HandleOnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput
        {
            MoveInput = _moveInput,
            LookInput = _lookInputAccumulated,
            ZoomInput = _zoomInput,
            Buttons = BuildButtons()
        };

        input.Set(data);

        //_interactPressed = false;
        _lookInputAccumulated = Vector2.zero;
        _zoomInput = 0f;
    }

    /// <summary>
    /// bool 입력들을 NetworkButtons 비트 플래그로 변환한다.
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
