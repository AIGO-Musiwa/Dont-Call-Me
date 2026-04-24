using UnityEngine;
using Fusion;
using System.Collections.Generic;

// ─── [데이터 구조체] ──────────────────────────────
[System.Serializable]
public class FloorData
{
    public string floorName; // 예: "1층", "2층"
    public List<Transform> spawnPoints; // 이 층에 존재하는 라디오 거치대(빈 오브젝트)들
}

[System.Serializable]
public class BuildingData
{
    public Zone zone;
    public List<FloorData> floors;
}

/// <summary>
/// 서버(StateAuthority)에서 게임 시작 시 건물/층별로 라디오를 1개씩 랜덤 생성하는 매니저
/// </summary>
public class RadioSpawnManager : NetworkBehaviour
{
    [Header("생성 부품")]
    [SerializeField] private NetworkObject radioPrefab;

    [Header("배치도 데이터")]
    [SerializeField] private List<BuildingData> buildings;

    public override void Spawned()
    {
        // 멀티플레이 환경이므로, 맵 생성의 권한을 가진 호스트(서버)만 스폰을 담당함
        if (HasStateAuthority)
        {
            DeployRadios();
        }
    }

    private void DeployRadios()
    {
        foreach (var building in buildings)
        {
            foreach (var floor in building.floors)
            {
                // 스폰 포인트가 등록되지 않은 층은 패스
                if (floor.spawnPoints == null || floor.spawnPoints.Count == 0)
                    continue;

                // 1. 해당 층의 스폰 포인트 중 하나를 랜덤으로 뽑음 (가챠!)
                int randomIndex = Random.Range(0, floor.spawnPoints.Count);
                Transform selectedPoint = floor.spawnPoints[randomIndex];

                // 2. 🛠️ 전방(Forward) 축 정렬 및 스폰
                // selectedPoint.rotation을 넘겨주면, 빈 오브젝트의 Z축(파란 화살표) 방향과
                // 프리팹의 Z축 방향이 완벽하게 일치된 상태로 생성돼.
                NetworkObject spawnedRadio = Runner.Spawn(
                    radioPrefab,
                    selectedPoint.position,
                    selectedPoint.rotation, // ⬅️ 여기가 방향을 맞물리게 하는 핵심 부품!
                    PlayerRef.None // 특정 플레이어 소유가 아닌 월드 오브젝트
                );

                // 스폰된 radio에 zone 주입
                if (spawnedRadio.TryGetComponent<Radio>(out var radio))
                    radio.SetZone(building.zone);

                Debug.Log($"<color=yellow>[라디오 배치 완료]</color> {building.zone} - {floor.floorName}에 배치됨.");
            }
        }
    }
}