using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using UnityEngine;

public class NetworkRunnerCallbacks : MonoBehaviour, INetworkRunnerCallbacks
{
    // ───────────────────────────────────────────
    // 입력 — InputCollector에 위임
    // ───────────────────────────────────────────

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // TODO: 클라이언트 담당 - InputCollector에서 입력 수집 후 전달
    }

    // ───────────────────────────────────────────
    // 플레이어 입퇴장
    // ───────────────────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // 대기실이 활성화된 경우 LobbyStateManager에 위임
        FindFirstObjectByType<LobbyStateManager>()?.OnPlayerJoined(runner, player);

        // TODO: 서버 담당 - 게임 씬 플레이어 스폰 (GameSessionManager 구현 후)
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        FindFirstObjectByType<LobbyStateManager>()?.OnPlayerLeft(runner, player);

        // TODO: 서버 담당 - 게임 씬 플레이어 오브젝트 제거 처리
    }

    // ───────────────────────────────────────────
    // 연결 상태
    // ───────────────────────────────────────────

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // 의도치 않은 종료(호스트 끊김 등) → NetworkManager가 메인 화면 이동 처리
        NetworkManager.Instance?.NotifyShutdown(shutdownReason);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        // 클라이언트가 호스트에 연결 완료 — Fusion이 씬 이동 처리하므로 별도 작업 불필요
        Debug.Log("[NetworkRunnerCallbacks] 서버 연결 완료");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[NetworkRunnerCallbacks] 서버 연결 끊김: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        // TODO: 서버 담당 - 게임 진행 중 접속 차단 (GameSessionManager 구현 후)
        // 현재는 Fusion의 PlayerCount(4) 제한만 적용
        request.Accept();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[NetworkRunnerCallbacks] 연결 실패: {reason}");
        NetworkManager.Instance?.NotifyConnectFailed(reason);
    }

    // ───────────────────────────────────────────
    // 기타 (현재 미사용)
    // ───────────────────────────────────────────

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
