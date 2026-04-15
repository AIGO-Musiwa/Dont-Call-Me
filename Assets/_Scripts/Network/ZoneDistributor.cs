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

    [Header("시드 참조")]
    [SerializeField] private RoundSeedManager roundSeedManager;

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
        List<PlayerData> orderedPlayerData = new();

        foreach(PlayerRef player in Runner.ActivePlayers)
        {
            PlayerData data = Runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
            if (data == null)
                continue;

            if (data.SlotIndex < 0)
                continue;

            orderedPlayerData.Add(data);
        }

        orderedPlayerData = orderedPlayerData
            .OrderBy(d => d.SlotIndex)
            .ToList();

        if(orderedPlayerData.Count == 0)
        {
            Debug.LogWarning("[ZoneDistributor] 배치할 PlayerData가 없습니다.");
            return;
        }

        roundSeedManager?.EnsureRoundSeed();

        int roundSeed = roundSeedManager != null ? roundSeedManager.CurrentSeed : 0;
        if(roundSeed == 0)
        {
            Debug.LogWarning("[ZoneDistributor] 유효한 라운드 시드가 없어 플레이어 배치를 진행할 수 없습니다.");
            return;
        }

        List<int> slotIndices = orderedPlayerData
            .Select(d => d.SlotIndex)
            .ToList();

        RoundGenerationResult assignResult = RoundGenerator.GeneratePlayerAssignments(roundSeed, slotIndices);
        Dictionary<int, RoundGenerationResult.PlayerAssignmentPlan> assignmentMap = assignResult.PlayerAssignments
            .ToDictionary(p => p.SlotIndex, p => p);

        for (int i = 0; i < orderedPlayerData.Count; i++)
        {
            PlayerData data = orderedPlayerData[i];
            if (data == null)
                continue;

            PlayerRef playerRef = data.Object.InputAuthority;

            //계산된 결과 적용
            if(!assignmentMap.TryGetValue(data.SlotIndex, out RoundGenerationResult.PlayerAssignmentPlan plan))
            {
                Debug.LogWarning($"[ZoneDistributor] SlotIndex = {data.SlotIndex}에 대한 배정 결과가 없습니다");
                continue;
            }

            Zone zone = plan.Zone;
            PlayerRole role = plan.Role;
            Transform spawnPoint = zone == Zone.ZoneA ? zoneASpawnPoint : zoneBSpawnPoint;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            NetworkObject obj = Runner.Spawn(playerPrefab, spawnPos, spawnRot, playerRef);

            PlayerController pc = obj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.NetZone = zone;
                pc.NetPlayerRole = role;

                if (!pc.ServerGrantRoleItemForCurrentRole())
                {
                    Debug.Log($"[ZoneDistributor] 역할 아이템 지급 스킵 또는 실패 : Player = {playerRef}, Role = {role}");
                }
            }
            else
            {
                Debug.LogWarning($"[ZoneDistributor] PlayerController를 찾을 수 없습니다 : {playerRef}");
            }

            data.PlayerControllerNetId = obj.Id;

            _playerZoneMap[playerRef] = zone;
            _playerRoleMap[playerRef] = role;
        }
    }
}
