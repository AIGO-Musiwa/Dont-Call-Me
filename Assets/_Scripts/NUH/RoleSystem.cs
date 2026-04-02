using Fusion;
using UnityEngine;
using System.Collections.Generic;
/// <summary>
/// 게임 시작 시 역할(손전등/무전기)과 건물(A/B)을 배정하는 시스템입니다.
///
/// 설계 규칙:
/// - 총 4명
/// - 건물 A: 손전등 1, 무전기 1
/// - 건물 B: 손전등 1, 무전기 1
/// </summary>
public class RoleSystem : NetworkBehaviour
{
    [Networked] public PlayerRole AssignedRole { get; private set; } // 현재 내 역할
    [Networked] public int AssignedBuilding { get; private set; } // 0=A, 1=B
    [Networked] public bool IsRoleAssigned { get; private set; } // 배정 완료 여부

    private HandSystem _handSystem;
    private PlayerController _playerController;

    [Header("역할 아이템 프리팹")]
    [SerializeField] private NetworkObject flashlightPrefab;
    [SerializeField] private NetworkObject walkiePrefab;

    public override void Spawned()
    {
        _handSystem = GetComponent<HandSystem>();
        _playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// 4명 접속 완료 후 Host가 한 번 호출하는 정적 함수.
    /// 플레이어 순서를 섞은 뒤, 역할/건물을 배정합니다.
    /// </summary>
    public static void AssignRoles(NetworkRunner runner, List<PlayerRef> players)
    {
        if (!runner.IsServer) return;
        if (players.Count != 4)
        {
            Debug.LogWarning($"[RoleSystem] 플레이어 수가 4명이 아님: {players.Count}");
            return;
        }

        // 랜덤 순서 섞기
        List<PlayerRef> shuffled = new List<PlayerRef>(players);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        
        // 셔플된 순서대로 배정
        (PlayerRole role, int building)[] assignments =
        {
            (PlayerRole.Flashlight, 0),
            (PlayerRole.Walkie, 0),
            (PlayerRole.Flashlight, 1),
            (PlayerRole.Walkie, 1),
        };
        
        for (int i = 0; i < shuffled.Count; i++)
        {
            NetworkObject playerObj = runner.GetPlayerObject(shuffled[i]);
            if (playerObj == null) continue;
            RoleSystem roleSystem = playerObj.GetComponent<RoleSystem>();
            if (roleSystem == null) continue;
            roleSystem.RPC_SetRole(assignments[i].role, assignments[i].building);
        }
    }

    /// <summary>
    /// 역할/건물 배정을 각 플레이어 오브젝트에 반영하는 RPC.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SetRole(PlayerRole role, int building)
    {
        AssignedRole = role;
        AssignedBuilding = building;
        IsRoleAssigned = true;
        Debug.Log($"[RoleSystem] 역할 배정 완료 — Role: {role}, Building: {building}");

        // 건물 가시성/관심영역 설정용 훅
        BuildingInterestManager bim = GetComponent<BuildingInterestManager>();
        bim?.OnBuildingAssigned(building);

        // 역할 아이템 생성은 서버만 수행
        if (Runner.IsServer)
            SpawnRoleItem();
    }

    /// <summary>
    /// 배정된 역할에 맞는 아이템을 네트워크 스폰하고
    /// 플레이어 왼손에 쥐여 줍니다.
    /// </summary>
    private void SpawnRoleItem()
    {
        NetworkObject prefab = AssignedRole == PlayerRole.Flashlight
        ? flashlightPrefab
        : walkiePrefab;
        
        if (prefab == null)
        {
            Debug.LogWarning("[RoleSystem] 역할 아이템 프리팹이 할당되지 않았습니다.");
            return;
        }

        NetworkObject spawnedItem = Runner.Spawn(
        prefab,
        transform.position,
        Quaternion.identity,
        Object.InputAuthority
        );

        ItemObject item = spawnedItem.GetComponent<ItemObject>();

        if (item != null)
        {
            item.IsRoleItem = true;
            item.OriginalOwner = Object.InputAuthority;
        }
        _handSystem?.PickupItem(item);
    }

    /// <summary>
    /// 이 아이템이 내 역할 아이템인지 판정합니다.
    /// </summary>
    public bool IsMyRoleItem(ItemObject item)
    {
        if (item == null || !item.IsRoleItem) return false;
        return item.OriginalOwner == Object.InputAuthority;
    }
    public PlayerRole GetRole() => AssignedRole;
    public int GetBuilding() => AssignedBuilding;
}
public enum PlayerRole
{
    Flashlight,
    Walkie
}