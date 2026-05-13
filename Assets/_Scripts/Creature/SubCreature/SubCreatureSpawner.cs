using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SubCreatureSpawner : NetworkBehaviour
{
    [Header("서브 크리처 프리팹 풀")]
    [Tooltip("기믹이 각각 다른 SubCreature 프리팹 배열")]
    public NetworkObject[] subCreaturePrefabs;

    [Header("스폰 후보 위치")]
    public Transform[] spawnPointsZoneA;
    public Transform[] spawnPointsZoneB;

    [Header("구역당 소환 수")]
    [Tooltip("각 구역에서 프리팹 풀 중 몇 개를 랜덤 소환할지")]
    public int spawnCountPerZone = 2;

    #region 초기화

    public override void Spawned()
    {
        if (!HasStateAuthority) return;

        SpawnForZone(Zone.ZoneA, spawnPointsZoneA);
        SpawnForZone(Zone.ZoneB, spawnPointsZoneB);
    }

    #endregion

    #region 소환 처리

    private void SpawnForZone(Zone zone, Transform[] points)
    {
        if (subCreaturePrefabs == null || subCreaturePrefabs.Length == 0)
        {
            Debug.LogError("[SubCreatureSpawner] subCreaturePrefabs가 비어 있습니다.");
            return;
        }

        if (points == null || points.Length == 0)
        {
            Debug.LogWarning($"[SubCreatureSpawner] {zone} 스폰 포인트가 없습니다.");
            return;
        }

        // 실제 소환 수 결정 (프리팹 수, 스폰 포인트 수, 설정값 중 최솟값)
        int count = Mathf.Min(spawnCountPerZone, subCreaturePrefabs.Length, points.Length);

        // 프리팹 인덱스 셔플 (비복원 추출)
        List<int> prefabIndices = BuildShuffledIndices(subCreaturePrefabs.Length);

        // 스폰 포인트 인덱스 셔플 (비복원 추출)
        List<int> pointIndices = BuildShuffledIndices(points.Length);

        for (int i = 0; i < count; i++)
        {
            NetworkObject prefab = subCreaturePrefabs[prefabIndices[i]];
            Transform spawnPoint = points[pointIndices[i]];

            if (prefab == null)
            {
                Debug.LogWarning($"[SubCreatureSpawner] prefab[{prefabIndices[i]}]가 null입니다. 건너뜀.");
                continue;
            }

            NetworkObject no = Runner.Spawn(
                prefab,
                spawnPoint.position,
                spawnPoint.rotation,
                PlayerRef.None);

            if (no == null)
            {
                Debug.LogError($"[SubCreatureSpawner] {zone} 서브 크리처 소환 실패 (포인트: {spawnPoint.name})");
                continue;
            }

            SubCreatureController controller = no.GetComponent<SubCreatureController>();
            if (controller == null)
            {
                Debug.LogError($"[SubCreatureSpawner] {no.name}에 SubCreatureController가 없습니다.");
                continue;
            }

            // 구역 배정
            controller.myZone = zone;

            // 스폰 포인트 배열을 relocatePoints로 주입
            controller.relocatePoints = points;

            Debug.Log($"[SubCreatureSpawner] {zone} / {no.name} 소환 완료 → {spawnPoint.name}");
        }
    }

    #endregion

    #region 유틸

    private static List<int> BuildShuffledIndices(int length)
    {
        List<int> indices = new();
        for (int i = 0; i < length; i++) indices.Add(i);

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }

    #endregion
}
