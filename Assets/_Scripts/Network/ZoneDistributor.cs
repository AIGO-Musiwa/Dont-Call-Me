using Fusion;
using System.Collections;
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

    [Header("지연 설정")]
    [SerializeField] private float distributeDelaySeconds = 0.1f; // 씬 진입 후 배치 시작 전 대기 시간

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly Dictionary<PlayerRef, Zone> _playerZoneMap = new();
    private readonly Dictionary<PlayerRef, PlayerRole> _playerRoleMap = new();

    private bool _hasDistributed;

    public IReadOnlyDictionary<PlayerRef, Zone> PlayerZoneMap => _playerZoneMap;
    public IReadOnlyDictionary<PlayerRef, PlayerRole> PlayerRoleMap => _playerRoleMap;

    public override void Spawned()
    {
        if (!Runner.IsServer)
            return;

        Log($"Spawned 호출 | ActivePlayers={Runner.ActivePlayers.Count()}");

        if (!_hasDistributed)
            StartCoroutine(DistributeAfterDelay());
    }

    private IEnumerator DistributeAfterDelay()
    {
        if (distributeDelaySeconds > 0f)
            yield return new WaitForSeconds(distributeDelaySeconds);
        else
            yield return null;

        TryDistributeOnce();
    }

    private void TryDistributeOnce()
    {
        if (_hasDistributed)
            return;

        int activeCount = Runner.ActivePlayers.Count();
        if (activeCount < requiredPlayers)
        {
            LogWarning($"인원 부족으로 배치 중단 | ActivePlayers={activeCount}/{requiredPlayers}");
            return;
        }

        List<PlayerData> orderedPlayerData = GetOrderedReadyPlayerData();
        if (orderedPlayerData.Count < requiredPlayers)
        {
            LogWarning($"PlayerData 준비 미완료 | ReadyPlayerData={orderedPlayerData.Count}/{requiredPlayers}");
            return;
        }

        DistributePlayers(orderedPlayerData);
        _hasDistributed = true;

        Log("플레이어 Zone/Role 배치 완료");
    }

    private List<PlayerData> GetOrderedReadyPlayerData()
    {
        List<PlayerData> result = new();

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(player);
            if (playerObject == null)
                continue;

            PlayerData data = playerObject.GetComponent<PlayerData>();
            if (data == null)
                continue;

            if (data.SlotIndex < 0)
                continue;

            result.Add(data);
        }

        return result
            .OrderBy(d => d.SlotIndex)
            .ToList();
    }

    private void DistributePlayers(List<PlayerData> orderedPlayerData)
    {
        if (orderedPlayerData == null || orderedPlayerData.Count == 0)
        {
            LogWarning("배치할 PlayerData가 없습니다.");
            return;
        }

        if (roundSeedManager == null)
        {
            LogWarning("RoundSeedManager 참조가 없습니다.");
            return;
        }

        roundSeedManager.EnsureRoundSeed();

        int roundSeed = roundSeedManager.CurrentSeed;
        if (roundSeed == 0)
        {
            LogWarning("유효한 라운드 시드가 없어 플레이어 배치를 진행할 수 없습니다.");
            return;
        }

        List<int> slotIndices = orderedPlayerData.Select(d => d.SlotIndex).ToList();

        RoundGenerationResult assignResult = RoundGenerator.GeneratePlayerAssignments(roundSeed, slotIndices);
        Dictionary<int, RoundGenerationResult.PlayerAssignmentPlan> assignmentMap =
            assignResult.PlayerAssignments.ToDictionary(p => p.SlotIndex, p => p);

        for (int i = 0; i < orderedPlayerData.Count; i++)
        {
            PlayerData data = orderedPlayerData[i];
            if (data == null)
                continue;

            PlayerRef playerRef = data.Object.InputAuthority;

            if (!assignmentMap.TryGetValue(data.SlotIndex, out RoundGenerationResult.PlayerAssignmentPlan plan))
            {
                LogWarning($"SlotIndex={data.SlotIndex}에 대한 배정 결과가 없습니다.");
                continue;
            }

            Zone zone = plan.Zone;
            PlayerRole role = plan.Role;
            Transform spawnPoint = zone == Zone.ZoneA ? zoneASpawnPoint : zoneBSpawnPoint;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            NetworkObject obj = Runner.Spawn(playerPrefab, spawnPos, spawnRot, playerRef);
            if (obj == null)
            {
                LogWarning($"PlayerController Spawn 실패: Player={playerRef}");
                continue;
            }

            PlayerController pc = obj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.NetZone = zone;
                pc.NetPlayerRole = role;
                pc.ServerGrantRoleItemForCurrentRole();
            }

            data.PlayerControllerNetId = obj.Id;

            _playerZoneMap[playerRef] = zone;
            _playerRoleMap[playerRef] = role;

            Log($"배치 완료 | Player={playerRef} | SlotIndex={data.SlotIndex} | Zone={zone} | Role={role}");
        }
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[ZoneDistributor] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[ZoneDistributor] {message}", this);
    }
}