using Fusion;
using UnityEngine;

// [수정 권한] 서버 담당자만
// 대기실 상태 관리 — Host에서만 실행되는 로직
// 씬에 NetworkObject로 배치, 대기실 씬에서만 사용
public class LobbyStateManager : NetworkBehaviour
{
    [SerializeField] private PlayerLobbyData playerLobbyDataPrefab;

    // 모든 클라이언트가 준비됐는지 (LobbyUIManager에서 시작 버튼 활성화에 사용)
    [Networked] public NetworkBool CanStart { get; set; }

    // ── Host 전용 로직 ─────────────────────────────────────────
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        CanStart = CheckAllReady();
    }

    // 플레이어 입장 시 NetworkRunnerCallbacks에서 호출
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        var data = runner.Spawn(
            playerLobbyDataPrefab,
            inputAuthority: player
        );
        data.Owner = player;
    }

    // 플레이어 퇴장 시 NetworkRunnerCallbacks에서 호출
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        foreach (var data in runner.GetAllBehaviours<PlayerLobbyData>())
        {
            if (data.Owner == player)
            {
                runner.Despawn(data.Object);
                break;
            }
        }
    }

    // 호스트가 게임 시작 버튼 클릭 시 LobbyUIManager에서 호출
    public void StartGame()
    {
        if (!HasStateAuthority || !CanStart) return;
        Runner.LoadScene(SceneRef.FromIndex(SceneNames.GameIndex));
    }

    // ── 내부 유틸 ─────────────────────────────────────────────
    private bool CheckAllReady()
    {
        int playerCount = 0;
        int readyCount  = 0;

        foreach (var data in Runner.GetAllBehaviours<PlayerLobbyData>())
        {
            playerCount++;
            if (data.IsReady) readyCount++;
        }

        // 혼자면 시작 불가 (최소 2명)
        return playerCount >= 2 && readyCount == playerCount;
    }
}
