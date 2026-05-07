using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ZoneDistributor : NetworkBehaviour
{
    [Header("캐릭터 레지스트리")]
    [SerializeField] private CharacterprefabRegistry characterRegistry;

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

        NotifyTeammateForVoice();
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

        if (characterRegistry == null)
        {
            LogWarning("CharacterPrefabRegistry 참조가 없습니다.");
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

            // CharacterIndex 기반으로 인게임 프리팹 선택
            NetworkObject ingamePrefab = characterRegistry.GetIngamePrefab(data.CharacterIndex);
            if (ingamePrefab == null)
            {
                LogWarning($"인게임 프리팹 없음 | CharacterIndex={data.CharacterIndex} | Player={playerRef}");
                continue;
            }

            NetworkObject obj = Runner.Spawn(ingamePrefab, spawnPos, spawnRot, playerRef);
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
            data.PlayerZone = zone;

            _playerZoneMap[playerRef] = zone;
            _playerRoleMap[playerRef] = role;

            Log($"배치 완료 | Player={playerRef} | SlotIndex={data.SlotIndex} | Zone={zone} | Role={role}");

            Rpc_NotifyGameReady();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_NotifyGameReady()
    {
        GameLauncher.Instance?.NotifyGameReady();
    }

    // 배치 완료 후 각 플레이어에게 같은 구역 팀원 NetworkId 전달
    private void NotifyTeammateForVoice()
    {
        Dictionary<PlayerRef, PlayerController> refToController = new();

        foreach (var kvp in _playerZoneMap)
        {
            if (!Runner.TryGetPlayerObject(kvp.Key, out var playerObj)) continue;
            PlayerData data = playerObj.GetComponent<PlayerData>();
            if (data == null) continue;

            PlayerController pc = data.GetPlayerController();
            if (pc == null) continue;

            refToController[kvp.Key] = pc;
        }

        foreach (var kvp in _playerZoneMap)
        {
            if (!refToController.TryGetValue(kvp.Key, out PlayerController pc)) continue;

            foreach (var otherKvp in _playerZoneMap)
            {
                if (otherKvp.Key == kvp.Key) continue;
                if (otherKvp.Value != kvp.Value) continue;

                if (!refToController.TryGetValue(otherKvp.Key, out PlayerController teammatePc)) continue;

                PlayerVoiceController pvc = pc.GetComponent<PlayerVoiceController>();
                if (pvc != null)
                    pvc.Rpc_SetTeammateForVoice(teammatePc.Object.Id);

                Log($"팀원 Voice 등록 | Player={kvp.Key} → Teammate={otherKvp.Key}");
                break;
            }
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