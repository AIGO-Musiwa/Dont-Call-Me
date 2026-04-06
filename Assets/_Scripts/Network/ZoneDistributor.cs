using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ZoneDistributor : NetworkBehaviour
{
    [Header("플레이어 프리팹")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Zone A 스폰 포인트")]
    [SerializeField] private Transform zoneASpawnPoint;

    [Header("Zone B 스폰 포인트")]
    [SerializeField] private Transform zoneBSpawnPoint;

    [Tooltip("배치를 시작할 최소 플레이어 수. 실제 게임: 4 / 테스트: 인원에 맞게 조정")]
    [SerializeField] private int requiredPlayers = 4;

    // ── 외부에서 배치 결과 조회용 ─────────────────────────
    private readonly Dictionary<PlayerRef, Zone> _playerZoneMap = new();
    private readonly Dictionary<PlayerRef, PlayerRole> _playerRoleMap = new();

    public IReadOnlyDictionary<PlayerRef, Zone> PlayerZoneMap => _playerZoneMap;
    public IReadOnlyDictionary<PlayerRef, PlayerRole> PlayerRoleMap => _playerRoleMap;

    // ─────────────────────────────────────────────────────
    public override void Spawned()
    {
        if (!Runner.IsServer) return;

        // 이미 필요한 인원이 다 모였으면 바로 배치
        if (Runner.ActivePlayers.Count() >= requiredPlayers)
        {
            DistributePlayers();
            return;
        }

        // 아직 다 안 모였으면 플레이어 입장 이벤트 구독 후 대기
        FusionCallbackHandler.Current.OnPlayerJoinedEvent += OnPlayerJoined;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (FusionCallbackHandler.Current != null)
            FusionCallbackHandler.Current.OnPlayerJoinedEvent -= OnPlayerJoined;
    }

    private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
        if (runner.ActivePlayers.Count() < requiredPlayers) return;

        FusionCallbackHandler.Current.OnPlayerJoinedEvent -= OnPlayerJoined;
        DistributePlayers();
    }

    // ─────────────────────────────────────────────────────
    private void DistributePlayers()
    {
        var players = new List<PlayerRef>(Runner.ActivePlayers);

        if (players.Count == 0)
        {
            Debug.LogWarning("[ZoneDistributor] 배치할 플레이어가 없습니다.");
            return;
        }

        // Fisher-Yates 셔플로 순서 무작위화
        for(int i = players.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (players[i], players[j]) = (players[j], players[i]);
        }

        // 역할은 슬롯 순서로 고정 (플레이어 순서가 셔플됐으므로 결과는 랜덤
        // 슬롯 0 -> 무전기, 슬롯 1 -> 손전등
        for (int i = 0; i < players.Count; i++)
        {
            // 앞 2명 -> Zone A, 뒤 2명 -> Zone B
            Zone zone = i < 2 ? Zone.ZoneA : Zone.ZoneB;
            int slotInZone = i < 2 ? i : i - 2;
            PlayerRole role = slotInZone == 0 ? PlayerRole.WalkieTalkie : PlayerRole.Flashlight;
            Transform spawnPoint = zone == Zone.ZoneA ? zoneASpawnPoint : zoneBSpawnPoint;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            var obj = Runner.Spawn(playerPrefab, spawnPos, spawnRot, players[i]);
            Runner.SetPlayerObject(players[i], obj);

            // PlayerController에 구역 / 역할 설정
            var pc = obj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.NetZone = zone;
                pc.NetPlayerRole = role;

                if (!pc.ServerGrantRoleItemForCurrentRole())
                {
                    Debug.Log($"[ZoneDistributor] 역할 아이템 지급 스킵 또는 실패: Player={players[i]}, Role={role}");
                }

            }
            else
            {
                Debug.LogWarning($"[ZoneDistributor] PlayerController를 찾을 수 없습니다: {players[i]}");
            }

            _playerZoneMap[players[i]] = zone;
            _playerRoleMap[players[i]] = role;
        }
    }
}
