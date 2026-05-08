using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Fusion에 전달할 플레이어 입력 구조체.
/// 
/// 역할:
/// - MoveInput        : WASD 이동 입력
/// - LookInput        : 마우스 시선 입력(틱 누적값)
/// - ZoomInput        : 마우스 휠 입력(관전 줌용)
/// - Buttons          : Sprint / Crouch / InteractPressed / InteractHeld / Walkie 같은 버튼 상태
/// </summary>
public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 MoveInput;   // 이동 입력
    public Vector2 LookInput;   // 시선 입력
    public float ZoomInput;     // 줌 입력
    public NetworkButtons Buttons; // 버튼 비트 플래그 묶음
}

/// <summary>
/// NetworkButtons 내부에서 사용할 버튼 인덱스 정의.
/// 각 버튼은 비트 플래그처럼 저장된다.
/// </summary>
public static class InputButtons
{
    public const int Sprint = 0;          // Shift
    public const int Crouch = 1;          // Ctrl
    public const int InteractPressed = 2; // 좌클릭 눌린 순간 1회성 입력
    public const int InteractHeld = 3;    // 좌클릭 누르고 있는 유지 입력
    public const int Walkie = 4;          // 우클릭 유지 입력
}

/// <summary>
/// Unity Input System 입력을 수집해서
/// Fusion이 사용하는 PlayerNetworkInput으로 빌드하는 입력 수집기.
/// 
/// 책임:
/// - 입력을 읽어서 로컬 버퍼에 저장한다.
/// - Fusion OnInput 시점에 버퍼를 PlayerNetworkInput으로 전달한다.
/// - 실제 이동/시야 적용은 하지 않는다.
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;     // 이동 입력 액션
    [SerializeField] private InputActionReference lookAction;     // 시선 입력 액션
    [SerializeField] private InputActionReference zoomAction;     // 줌 입력 액션
    [SerializeField] private InputActionReference sprintAction;   // 달리기 입력 액션
    [SerializeField] private InputActionReference crouchAction;   // 앉기 입력 액션
    [SerializeField] private InputActionReference interactAction; // 상호작용 입력 액션
    [SerializeField] private InputActionReference walkieAction;   // 무전기 입력 액션
    [SerializeField] private InputActionReference minigameInput;  // 미니게임 입력 키 (스페이스바)

    private float _mouseSensitivity = 1.0f; //마우스 감도

    private Vector2 _moveInput;             // 현재 프레임 이동 입력
    private Vector2 _lookInputAccumulated; // Fusion 틱 동안 누적된 시선 입력
    private float _zoomInput;              // 현재 프레임 줌 입력

    /// <summary>
    /// 이번 프레임의 마우스 델타.
    /// 로컬 플레이어가 즉시 시야 회전에 사용할 수 있도록 공개한다.
    /// </summary>
    public Vector2 FrameLookDelta { get; private set; } // 이번 프레임 마우스 델타

    // 🛠️ [신규 개조] 미니게임 전용 스페이스바 입력 버퍼 (단타 전용, 네트워크 전송 X)
    private bool _minigamePressedBuffer;

    private bool _sprintPressed;   // 달리기 유지 상태
    private bool _crouchPressed;   // 앉기 유지 상태
    private bool _interactPressed; // 상호작용 눌린 순간 1회성 상태
    private bool _interactHeld;    // 상호작용 누르고 있는 유지 상태
    private bool _walkiePressed;   // 무전기 누르고 있는 유지 상태

    private FusionCallbackHandler _registeredHandler; // Fusion OnInput 콜백 연결 대상

    /// <summary>
    /// 미니게임 UI에서 스페이스바 입력을 가져가는 단자.
    /// 값을 가져감과 동시에 버퍼를 비운다(소비한다).
    /// </summary>
    public bool ConsumeMinigameInput()
    {
        if (_minigamePressedBuffer)
        {
            _minigamePressedBuffer = false; // 읽었으니 스위치 끔
            return true;
        }
        return false;
    }

    /// <summary>
    /// FusionCallbackHandler에 OnInput 콜백을 연결한다.
    /// </summary>
    public void Initialize(FusionCallbackHandler handler)
    {
        if (_registeredHandler != null)
            _registeredHandler.OnInputEvent -= HandleOnInput; // 이전 핸들러 해제

        _registeredHandler = handler; // 새 핸들러 저장
        _registeredHandler.OnInputEvent += HandleOnInput; // OnInput 콜백 등록
    }

    private void OnDestroy()
    {
        if (_registeredHandler != null)
            _registeredHandler.OnInputEvent -= HandleOnInput; // 파괴 시 콜백 해제
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
        minigameInput.action.Enable(); // 미니게임 액션 활성화

        interactAction.action.performed += OnInteractPerformed; // 좌클릭 눌림 순간 감지
        interactAction.action.canceled += OnInteractCanceled;   // 좌클릭 해제 감지
        walkieAction.action.performed += OnWalkiePerformed;     // 우클릭 눌림 감지
        walkieAction.action.canceled += OnWalkieCanceled;       // 우클릭 해제 감지

        // 🛠️ [신규 개조] 미니게임 스페이스바 눌림 순간 이벤트 연결
        minigameInput.action.performed += OnMinigamePerformed;
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
        minigameInput.action.Disable(); // 미니게임 액션 비활성화

        interactAction.action.performed -= OnInteractPerformed;
        interactAction.action.canceled -= OnInteractCanceled;
        walkieAction.action.performed -= OnWalkiePerformed;
        walkieAction.action.canceled -= OnWalkieCanceled;

        // 🛠️ [신규 개조] 미니게임 스페이스바 눌림 순간 이벤트 해제
        minigameInput.action.performed -= OnMinigamePerformed;
    }

    // SettingsUI에서 이 함수를 불러 감도를 덮어씌울 거임
    public void SetMouseSensitivity(float newSensitivity)
    {
        _mouseSensitivity = newSensitivity;
    }

    private void Start()
    {
        // 처음 시작할 때도 저장된 값을 불러오도록
        _mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
    }

    private void Update()
    {
        if (ESCUI.IsOpen) return;

        _moveInput = moveAction.action.ReadValue<Vector2>(); // 현재 이동 입력 갱신

        Vector2 currentFrameLook = lookAction.action.ReadValue<Vector2>() * _mouseSensitivity; // 현재 프레임 마우스 델타 읽기
        FrameLookDelta = currentFrameLook; // 로컬 즉시 회전용 공개값 갱신
        _lookInputAccumulated += currentFrameLook; // Fusion 틱 전달용 누적

        _zoomInput = zoomAction.action.ReadValue<float>(); // 현재 줌 입력 읽기

        _sprintPressed = sprintAction.action.IsPressed(); // 달리기 유지 상태 갱신
        _crouchPressed = crouchAction.action.IsPressed(); // 앉기 유지 상태 갱신

        // ❌ 휘발성 데이터 읽기 부분 삭제 완료
    }

    /// <summary>
    /// 스페이스바 눌린 순간 버퍼 스위치를 켠다! (미니게임용)
    /// </summary>
    private void OnMinigamePerformed(InputAction.CallbackContext ctx)
    {
        if (ESCUI.IsOpen) return;
        _minigamePressedBuffer = true;
    }

    /// <summary>
    /// 좌클릭 눌린 순간 호출된다.
    /// - Pressed는 이번 틱 1회성 입력
    /// - Held는 버튼을 누르고 있는 유지 상태
    /// 둘 다 true로 올린다.
    /// </summary>
    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (ESCUI.IsOpen) return;
        _interactPressed = true; // 이번 입력 틱에서 눌림 순간 기록
        _interactHeld = true;    // 버튼 유지 상태 시작
    }

    /// <summary>
    /// 좌클릭 해제 시 호출된다.
    /// 유지 상태만 해제한다.
    /// </summary>
    private void OnInteractCanceled(InputAction.CallbackContext ctx)
    {
        if (ESCUI.IsOpen) return;
        _interactHeld = false; // 버튼 유지 상태 종료
    }

    /// <summary>
    /// 우클릭 눌림 시 호출된다.
    /// </summary>
    private void OnWalkiePerformed(InputAction.CallbackContext ctx)
    {
        if (ESCUI.IsOpen) return;
        _walkiePressed = true; // 무전기 유지 상태 시작
    }

    /// <summary>
    /// 우클릭 해제 시 호출된다.
    /// </summary>
    private void OnWalkieCanceled(InputAction.CallbackContext ctx)
    {
        if (ESCUI.IsOpen) return;
        _walkiePressed = false; // 무전기 유지 상태 종료
    }

    /// <summary>
    /// Fusion이 틱 입력을 요청할 때 누적된 입력을 PlayerNetworkInput으로 전달한다.
    /// </summary>
    public void HandleOnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput
        {
            MoveInput = _moveInput,                 // 이동 입력 전달
            LookInput = _lookInputAccumulated,      // 누적 시선 입력 전달
            ZoomInput = _zoomInput,                 // 현재 줌 입력 전달
            Buttons = BuildButtons()                // 버튼 비트 플래그 생성
        };

        input.Set(data); // Fusion 입력으로 전달

        _interactPressed = false;        // Pressed는 1틱짜리 입력이므로 사용 후 소모
        _lookInputAccumulated = Vector2.zero; // 시선 입력 누적 초기화
        _zoomInput = 0f;                 // 줌 입력 초기화
    }

    /// <summary>
    /// bool 입력들을 NetworkButtons 비트 플래그로 변환한다.
    /// </summary>
    private NetworkButtons BuildButtons()
    {
        NetworkButtons buttons = new NetworkButtons(); // 새 버튼 비트 플래그 생성
        buttons.Set(InputButtons.Sprint, _sprintPressed);           // 달리기 상태 기록
        buttons.Set(InputButtons.Crouch, _crouchPressed);           // 앉기 상태 기록
        buttons.Set(InputButtons.InteractPressed, _interactPressed); // 상호작용 눌림 순간 기록
        buttons.Set(InputButtons.InteractHeld, _interactHeld);       // 상호작용 유지 상태 기록
        buttons.Set(InputButtons.Walkie, _walkiePressed);           // 무전기 유지 상태 기록
        return buttons;
    }
}