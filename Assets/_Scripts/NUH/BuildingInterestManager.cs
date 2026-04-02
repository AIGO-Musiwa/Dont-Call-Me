using Fusion;
using UnityEngine;

/// <summary>
/// 건물별 카메라 Culling Mask 및 Fusion Interest Management 설정
/// 두 건물은 서로를 렌더링하지 않음
///
/// 설정 방법:
///   씬에서 Building A 오브젝트들 → Layer "BuildingA" 설정
///   씬에서 Building B 오브젝트들 → Layer "BuildingB" 설정
///   플레이어 카메라의 Culling Mask를 자기 건물 Layer만 포함하도록 설정
/// </summary>
public class BuildingInterestManager : NetworkBehaviour
{
    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("건물 레이어")]
    [SerializeField] private LayerMask buildingA_Layers;  // Building A에 속하는 Layer들
    [SerializeField] private LayerMask buildingB_Layers;  // Building B에 속하는 Layer들
    [SerializeField] private LayerMask commonLayers;      // 공통 레이어 (UI, Default 등)

    [Header("레퍼런스")]
    [SerializeField] private Camera fpCamera;

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        if (fpCamera == null)
            fpCamera = GetComponentInChildren<Camera>();
    }

    // ─────────────────────────────────────────
    // 건물 배정 처리
    // ─────────────────────────────────────────

    /// <summary>
    /// RoleSystem.RPC_SetRole() 완료 후 호출
    /// 자기 건물 Layer만 렌더링하도록 Culling Mask 설정
    /// </summary>
    public void OnBuildingAssigned(int buildingIndex)
    {
        if (!HasInputAuthority) return;
        SetCullingMask(buildingIndex);
        SetInterestArea(buildingIndex);
    }

    /// <summary>
    /// 자기 건물 Layer + 공통 Layer만 렌더
    /// 상대 건물은 Culling Mask에서 제외
    /// </summary>
    public void SetCullingMask(int buildingIndex)
    {
        if (fpCamera == null) return;

        LayerMask myBuildingLayers = buildingIndex == 0
            ? buildingA_Layers
            : buildingB_Layers;

        // 공통 레이어 + 자기 건물 레이어 합산
        fpCamera.cullingMask = commonLayers | myBuildingLayers;

        Debug.Log($"[BuildingInterestManager] 건물 {(buildingIndex == 0 ? "A" : "B")} Culling Mask 적용");
    }

    /// <summary>
    /// Fusion Interest Management — 자기 건물 영역만 관심 영역으로 설정
    /// 네트워크 트래픽 최적화 (상대 건물 NetworkObject 동기화 제외)
    /// </summary>
    public void SetInterestArea(int buildingIndex)
    {
        if (Runner == null) return;

        // Fusion 2 Area of Interest 설정
        // 건물 중심 위치 기준으로 Interest Area 반경 설정
        NetworkObject[] buildingObjects = buildingIndex == 0
            ? GetBuildingObjects(0)
            : GetBuildingObjects(1);

        if (buildingObjects == null) return;

        foreach (NetworkObject obj in buildingObjects)
        {
            if (obj == null) continue;
            Runner.AddPlayerAreaOfInterest(Runner.LocalPlayer, obj.transform.position, 100f);
        }
    }

    // ─────────────────────────────────────────
    // 관전 모드 전환 시 모든 건물 렌더링
    // ─────────────────────────────────────────

    /// <summary>
    /// SpectatorSystem에서 호출
    /// 관전 시 모든 건물을 볼 수 있도록 Culling Mask 전체 해제
    /// </summary>
    public void EnableAllBuildings()
    {
        if (fpCamera == null) return;
        fpCamera.cullingMask = ~0;  // 모든 레이어 렌더링
    }

    // ─────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────

    /// <summary>
    /// 건물 인덱스에 해당하는 NetworkObject 배열 반환
    /// 씬에서 BuildingAnchor 태그로 구분
    /// </summary>
    private NetworkObject[] GetBuildingObjects(int buildingIndex)
    {
        string tag = buildingIndex == 0 ? "BuildingA" : "BuildingB";
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(tag);

        NetworkObject[] result = new NetworkObject[tagged.Length];
        for (int i = 0; i < tagged.Length; i++)
            result[i] = tagged[i].GetComponent<NetworkObject>();

        return result;
    }
}
