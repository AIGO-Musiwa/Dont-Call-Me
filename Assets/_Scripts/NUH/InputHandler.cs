using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
/// <summary>
/// 네트워크 틱마다 전송할 "한 플레이어의 입력 묶음"입니다.
///
/// Fusion에서는 키보드/마우스 입력을 그대로 보내는 게 아니라,
/// 이렇게 구조체로 정리해서 Runner에 전달합니다.
///
/// 장점:
/// - 어떤 입력이 네트워크에 실리는지 명확함
/// - 예측/재현이 쉬움
/// - FixedUpdateNetwork에서 같은 입력을 기준으로 동작 가능
/// </summary>
public struct PlayerNetworkInput : INetworkInput
{
    /// <summary>
    /// 이동 입력.
    /// 보통 x = 좌우(A/D), y = 전후(W/S)
    /// </summary>
    public Vector2 MoveInput;
    /// <summary>
    /// 마우스 델타 입력.
    /// x = 좌우 회전, y = 상하 회전
    /// </summary>
    public Vector2 LookInput;
    /// <summary>
    /// 버튼 입력을 비트 단위로 묶어 저장하는 구조.
    /// Sprint, Crouch, Interact, Walkie 등을 담습니다.
    /// </summary>
    public NetworkButtons Buttons;
}
/// <summary>
/// NetworkButtons 안에서 각 버튼이 몇 번 칸을 쓰는지 정한 상수입니다.
///
/// 예를 들어 Sprint = 0이면,
/// Buttons 내부의 0번 비트가 Sprint 키 상태를 뜻합니다.
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
/// Fusion이 이해할 수 있는 PlayerNetworkInput으로 바꾸는 클래스입니다.
///
/// 역할 요약:
/// 1. 로컬 플레이어 입력을 읽는다.
/// 2. 버튼/축 입력을 내부 버퍼에 저장한다.
/// 3. Fusion Runner가 OnInput을 요청하면 네트워크 입력 구조체로 만들어 넘긴다.
/// </summary>
public class InputHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction; // WASD용 Action
    [SerializeField] private InputActionReference lookAction; // 마우스 델타용 Action
    [SerializeField] private InputActionReference sprintAction; // 달리기 Action
    [SerializeField] private InputActionReference crouchAction; // 앉기 Action
    [SerializeField] private InputActionReference interactAction; // 좌클릭 Action
    [SerializeField] private InputActionReference walkieAction; // 우클릭 Action


    // 매 프레임 수집한 입력 버퍼.
    // OnInput이 호출될 때 이 값들을 묶어서 전송합니다.
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _sprintPressed;
    private bool _crouchPressed;
    private bool _interactPressed;
    private bool _walkiePressed;


    private void OnEnable()
    {
        // Input System 액션을 활성화합니다.
        // Enable 하지 않으면 ReadValue/IsPressed가 동작하지 않습니다.
        moveAction.action.Enable();
        lookAction.action.Enable();
        sprintAction.action.Enable();
        crouchAction.action.Enable();
        interactAction.action.Enable();
        walkieAction.action.Enable();
        // 단발성 버튼 입력은 performed/canceled로 받습니다.
        // 이유:
        // 네트워크 틱과 Unity 프레임 타이밍이 어긋날 때,
        // 클릭 순간이 Update 한 번만 지나가면 놓칠 수 있기 때문입니다.
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
        // 축 입력/홀드 입력은 매 프레임 갱신합니다.
        // Move/Look은 연속값이라 ReadValue가 적합합니다.
        _moveInput = moveAction.action.ReadValue<Vector2>();
        _lookInput = lookAction.action.ReadValue<Vector2>();

        // Sprint/Crouch는 누르고 있는 동안 true인 홀드성 입력이라 IsPressed 사용.
        _sprintPressed = sprintAction.action.IsPressed();
        _crouchPressed = crouchAction.action.IsPressed();
    }
    // 단발 입력용 콜백.
    // 클릭이 들어오면 true, 버튼이 떼지면 false로 바꿉니다.
    private void OnInteractPerformed(InputAction.CallbackContext ctx) => _interactPressed = true;
    private void OnWalkiePerformed(InputAction.CallbackContext ctx) => _walkiePressed = true;
    private void OnInteractCanceled(InputAction.CallbackContext ctx) => _interactPressed = false;
    private void OnWalkieCanceled(InputAction.CallbackContext ctx) => _walkiePressed = false;


    /// <summary>
    /// Fusion Runner가 "이번 틱 입력 줘"라고 요청할 때 호출됩니다.
    ///
    /// 여기서 로컬 입력 버퍼를 PlayerNetworkInput으로 묶어
    /// Fusion 쪽에 넘깁니다.
    /// </summary>
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new PlayerNetworkInput();
        data.MoveInput = _moveInput;
        data.LookInput = _lookInput;
        data.Buttons = BuildButtons();
        input.Set(data);
        // Interact는 "한 번 눌림" 성격이 강해서 틱 전송 후 리셋합니다.
        // 그렇지 않으면 클릭 한 번이 여러 틱에 반복 전송될 수 있습니다.
        _interactPressed = false;
        // Walkie는 홀드 입력이라 canceled 때 false가 되므로 여기서 리셋 안 합니다.
    }


    /// <summary>
    /// bool 입력들을 NetworkButtons 비트 플래그로 묶는 함수입니다.
    /// </summary>
    private NetworkButtons BuildButtons()
    {
        var buttons = new NetworkButtons();
        buttons.Set(InputButtons.Sprint, _sprintPressed);
        buttons.Set(InputButtons.Crouch, _crouchPressed);
        buttons.Set(InputButtons.Interact, _interactPressed);
        buttons.Set(InputButtons.Walkie, _walkiePressed);
        return buttons;
    }


    // 아래는 INetworkRunnerCallbacks 구현용 빈 함수들입니다.
    // 지금은 사용하지 않지만, 인터페이스를 구현했기 때문에 선언해 둔 상태입니다.
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}