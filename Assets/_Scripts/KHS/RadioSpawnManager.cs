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
/// 서버(StateAuthority)에서 정해진 시드값에 따라 건물/층별로 라디오를 1개씩 고정 난수 생성하는 매니저
/// </summary>
public class RadioSpawnManager : NetworkBehaviour, IPuzzleSeedReceiver // 🛠️ 시드 수신기 인터페이스 장착
{
    [Header("생성 부품")]
    [SerializeField] private NetworkObject radioPrefab;

    [Header("배치도 데이터")]
    [SerializeField] private List<BuildingData> buildings;

    private bool _isDeployed = false; // 🛠️ 중복 생성 방지용 안전 퓨즈

    public override void Spawned()
    {
        // 🛠️ 예전에는 여기서 바로 스폰했지만, 이제는 메인 시드가 들어올 때까지 대기(Standby) 상태 유지
    }

    // ─── [시드 수신 단자 (IPuzzleSeedReceiver 규약)] ─────────────────────

    /// <summary>
    /// PuzzleSeedSync 모듈에서 시드를 분배할 때 호출됨.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        // 1. 이미 배치가 끝났다면 중복 실행 방지
        if (_isDeployed) return;

        // 2. 퓨전 엔진 규격: 실제 스폰(Runner.Spawn)은 서버에서만 수행해야 함!
        // 서버가 스폰하면 클라이언트들에게는 자동으로 동기화됨.
        if (HasStateAuthority)
        {
            DeployRadios(seed);
        }
    }

    private void DeployRadios(int seed)
    {
        // 🛠️ UnityEngine.Random 대신, 기공사의 정밀 부품인 SeedRandom 사용!
        SeedRandom rng = new SeedRandom(seed);

        _isDeployed = true; // 스위치 차단

        foreach (var building in buildings)
        {
            foreach (var floor in building.floors)
            {
                // 스폰 포인트가 등록되지 않은 층은 패스
                if (floor.spawnPoints == null || floor.spawnPoints.Count == 0)
                    continue;

                // 🛠️ SeedRandom.NextInt를 사용하여 시드에 기반한 완벽하게 통제된 난수 추출
                // NextInt는 maxInclusive가 아니라 maxExclusive처럼 동작하도록 설계되어 있으니 배열 길이를 그대로 넣음
                int randomIndex = rng.NextInt(0, floor.spawnPoints.Count);
                Transform selectedPoint = floor.spawnPoints[randomIndex];

                // 전방(Forward) 축 정렬 및 스폰
                NetworkObject spawnedRadio = Runner.Spawn(
                    radioPrefab,
                    selectedPoint.position,
                    selectedPoint.rotation,
                    PlayerRef.None // 특정 플레이어 소유가 아닌 월드 오브젝트
                );

                // 스폰된 radio에 zone 주입
                if (spawnedRadio.TryGetComponent<Radio>(out var radio))
                {
                    radio.SetZone(building.zone);
                }

                Debug.Log($"<color=yellow>[라디오 배치 완료]</color> {building.zone} - {floor.floorName}에 배치됨. (적용 시드: {seed})");
            }
        }
    }
}