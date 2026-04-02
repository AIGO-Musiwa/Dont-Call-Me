using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fusion 2 기반 플레이어 생성 및 건물별 스폰 포인트 배정
/// INetworkRunnerCallbacks 구현 — 플레이어 입장/퇴장 처리
///
/// 스폰 흐름:
///   OnPlayerJoined → Runner.Spawn(playerPrefab) → RoleSystem.AssignRoles()
///   4명 모두 스폰 완료 시 역할 배정 시작
/// </summary>
public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("프리팹")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("건물 A 스폰 포인트 (2개)")]
    [SerializeField] private Transform[] spawnPointsA;

    [Header("건물 B 스폰 포인트 (2개)")]
    [SerializeField] private Transform[] spawnPointsB;

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();
    private int _spawnCount;   // 현재까지 스폰된 플레이어 수

    // ─────────────────────────────────────────
    // INetworkRunnerCallbacks
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어 입장 시 호출
    /// Host만 Spawn 처리
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        // 스폰 포인트 결정
        // 0,1번 → 건물 A, 2,3번 → 건물 B
        Transform spawnPoint = GetSpawnPoint(_spawnCount);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[PlayerSpawner] 스폰 포인트 없음: index {_spawnCount}");
            return;
        }

        NetworkObject playerObj = runner.Spawn(
            playerPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            player
        );

        _spawnedPlayers[player] = playerObj;
        runner.SetPlayerObject(player, playerObj);
        _spawnCount++;

        Debug.Log($"[PlayerSpawner] 플레이어 스폰 완료: {player}, 총 {_spawnCount}명");

        // 4명 모두 스폰 완료 시 역할 배정
        if (_spawnCount >= 4)
        {
            List<PlayerRef> allPlayers = new List<PlayerRef>(_spawnedPlayers.Keys);
            RoleSystem.AssignRoles(runner, allPlayers);
        }
    }

    /// <summary>
    /// 플레이어 퇴장 시 호출 — 해당 플레이어 오브젝트 제거
    /// </summary>
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (_spawnedPlayers.TryGetValue(player, out NetworkObject playerObj))
        {
            runner.Despawn(playerObj);
            _spawnedPlayers.Remove(player);
            _spawnCount--;
        }
    }

    // ─────────────────────────────────────────
    // 스폰 포인트 결정
    // ─────────────────────────────────────────

    /// <summary>
    /// 스폰 순서에 따라 건물 A/B의 포인트 반환
    /// index 0,1 → Building A
    /// index 2,3 → Building B
    /// </summary>
    private Transform GetSpawnPoint(int index)
    {
        if (index < 2)
        {
            if (spawnPointsA == null || index >= spawnPointsA.Length) return null;
            return spawnPointsA[index];
        }
        else
        {
            int bIndex = index - 2;
            if (spawnPointsB == null || bIndex >= spawnPointsB.Length) return null;
            return spawnPointsB[bIndex];
        }
    }

    // ─────────────────────────────────────────
    // INetworkRunnerCallbacks — 미사용 (필수 구현)
    // ─────────────────────────────────────────
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
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
