using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class FusionCallbackHandler : INetworkRunnerCallbacks
{
    public static FusionCallbackHandler Current { get; private set; }

    public bool IsDebugSession { get; set; } = false;

    public FusionCallbackHandler()
    {
        Current = this;
    }

    // ── 외부에서 구독할 이벤트 ────────────────────────────
    public event Action<NetworkRunner, PlayerRef> OnPlayerJoinedEvent;
    public event Action<NetworkRunner, PlayerRef> OnPlayerLeftEvent;
    public event Action<ShutdownReason> OnShutdownEvent;
    public event Action<NetDisconnectReason> OnDisconnectedEvent;
    public event Action<NetConnectFailedReason> OnConnectFailedEvent;
    public event Action<NetworkRunner, NetworkInput> OnInputEvent;
    
    public event Action OnSceneLoadStartEvent;
    public event Action OnSceneLoadDoneEvent;

    // ── INetworkRunnerCallbacks 구현 ──────────────────────
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        => OnPlayerJoinedEvent?.Invoke(runner, player);

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        => OnPlayerLeftEvent?.Invoke(runner, player);

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        OnShutdownEvent?.Invoke(reason);
        Current = null;
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        => OnDisconnectedEvent?.Invoke(reason);

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        => OnConnectFailedEvent?.Invoke(reason);

    public void OnInput(NetworkRunner runner, NetworkInput input)
        => OnInputEvent?.Invoke(runner, input);

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[FusionCallbackHandler] 서버 연결 완료");
    }

    public void OnConnectRequest(NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        if (IsDebugSession)
        {
            request.Accept();
            return;
        }

        // 게임 실행중 입장 거부
        if (GameSessionManager.Instance != null)
        {
            request.Refuse();
            Debug.Log("[FusionCallbackHandler] 게임 진행 중 - 접속 거부");
            return;
        }
        request.Accept();
    }

    public void OnSceneLoadStart(NetworkRunner runner)
        => OnSceneLoadStartEvent?.Invoke();

    public void OnSceneLoadDone(NetworkRunner runner)
        => OnSceneLoadDoneEvent?.Invoke();


    // ── 빈 구현 ───────────────────────────────────────────
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

}
