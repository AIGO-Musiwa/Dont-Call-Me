using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SecurityCameraCreatureManager : NetworkBehaviour
{
    public static SecurityCameraCreatureManager Instance { get; private set; }

    [Header("구역당 동시 활성화 수")]
    [Tooltip("각 구역에서 동시에 활성화할 감시 카메라 수.")]
    public int activateCountPerZone = 2;

    // 구역별 전체 카메라 목록
    private readonly List<SubCreatureController> cameraPoolA = new();
    private readonly List<SubCreatureController> cameraPoolB = new();

    // 구역별 현재 활성화된 카메라
    private readonly List<SubCreatureController> activeCamerasA = new();
    private readonly List<SubCreatureController> activeCamerasB = new();

    #region 초기화

    public override void Spawned()
    {
        Debug.Log($"[SecurityCameraManager] Spawned 호출 HasStateAuthority={HasStateAuthority}");

        Instance = this;

        if (!HasStateAuthority) return;

        // 씬에 배치된 SubCreatureController 전체 수집
        SubCreatureController[] allCameras = FindObjectsByType<SubCreatureController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (SubCreatureController cam in allCameras)
        {
            if (cam.myZone == Zone.ZoneA) cameraPoolA.Add(cam);
            else cameraPoolB.Add(cam);
        }

        // 구역별 초기 활성화
        ActivateNext(Zone.ZoneA, activateCountPerZone);
        ActivateNext(Zone.ZoneB, activateCountPerZone);

        Debug.Log($"[SecurityCameraCreatureManager] ZoneA 카메라 {cameraPoolA.Count}개 / ZoneB 카메라 {cameraPoolB.Count}개 수집");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region 서브 크리처 활성화/바활성화

    // 비활성화된 카메라를 active 목록에서 제거하고 새 카메라를 활성화한다.
    public void OnCameraDeactivated(SubCreatureController camera)
    {
        if (!HasStateAuthority) return;

        Zone zone = camera.myZone;
        List<SubCreatureController> active = GetActiveList(zone);

        active.Remove(camera);

        // 부족한 수만큼 새로 활성화
        int shortage = activateCountPerZone - active.Count;
        if (shortage > 0)
            ActivateNext(zone, shortage);
    }

    // 카메라 활성화 처리
    private void ActivateNext(Zone zone, int count)
    {
        List<SubCreatureController> pool = GetPool(zone);
        List<SubCreatureController> active = GetActiveList(zone);

        // 현재 비활성화 상태인 카메라만 후보로
        List<SubCreatureController> candidates = new();
        foreach (SubCreatureController cam in pool)
        {
            if (cam.NetState == SubCreatureState.Inactive)
                candidates.Add(cam);
        }

        // Fisher-Yates 셔플
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int activated = 0;
        foreach (SubCreatureController cam in candidates)
        {
            if (activated >= count) break;

            cam.Activate();
            active.Add(cam);
            activated++;

            Debug.Log($"[SecurityCameraCreatureManager] {zone} 카메라 활성화 → {cam.name}");
        }

        if (activated < count)
            Debug.LogWarning($"[SecurityCameraCreatureManager] {zone} 활성화 가능한 카메라 부족 ({activated}/{count})");
    }

    #endregion

    #region 유틸

    private List<SubCreatureController> GetPool(Zone zone)
        => zone == Zone.ZoneA ? cameraPoolA : cameraPoolB;

    private List<SubCreatureController> GetActiveList(Zone zone)
        => zone == Zone.ZoneA ? activeCamerasA : activeCamerasB;

    #endregion
}