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
/// 서버(StateAuthority)에서 정해진 시드값에 따라 건물/층별로 라디오를 1개씩 고정 난수 생성하는 매니저.
/// 시드 통제기(PuzzleSeedSync)가 없으면 자체 랜덤 시드로 비상 가동한다.
/// </summary>
public class RadioSpawnManager : NetworkBehaviour, IPuzzleSeedReceiver
{
    [Header("생성 부품")]
    [SerializeField] private NetworkObject radioPrefab;

    [Header("배치도 데이터")]
    [SerializeField] private List<BuildingData> buildings;

    private bool _isDeployed = false; // 중복 생성 방지용 안전 퓨즈

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            // 🛠️ 1. 내 상위 부품 중에 시드 통제기(PuzzleSeedSync)가 있는지 스캔
            PuzzleSeedSync masterSync = GetComponentInParent<PuzzleSeedSync>();

            if (masterSync == null)
            {
                // 🛠️ 2. 통제기가 없다면? 자체적으로 무작위 시드를 뽑아서 즉시 비상 가동!
                int fallbackSeed = Random.Range(1, 999999);
                Debug.Log($"<color=orange>[라디오 공장]</color> 상위 시드 통제기를 찾을 수 없습니다. 자체 랜덤 시드({fallbackSeed})로 비상 가동합니다.");

                ApplyAnswerSeed(fallbackSeed);
            }
            else
            {
                // 🛠️ 3. 통제기가 있다면 얌전히 시드 배달이 올 때까지 대기(Standby)
                Debug.Log("<color=cyan>[라디오 공장]</color> 시드 통제기 확인 완료. 정답 시드 수신 대기 중...");
            }
        }
    }

    // ─── [시드 수신 단자 (IPuzzleSeedReceiver 규약)] ─────────────────────

    public void ApplyAnswerSeed(int seed)
    {
        // 이미 배치가 끝났다면 중복 실행 방지
        if (_isDeployed) return;

        if (HasStateAuthority)
        {
            DeployRadios(seed);
        }
    }

    private void DeployRadios(int seed)
    {
        // UnityEngine.Random 대신, 기공사의 정밀 부품인 SeedRandom 사용!
        SeedRandom rng = new SeedRandom(seed);

        _isDeployed = true; // 스위치 차단

        foreach (var building in buildings)
        {
            foreach (var floor in building.floors)
            {
                if (floor.spawnPoints == null || floor.spawnPoints.Count == 0)
                    continue;

                // SeedRandom.NextInt를 사용하여 시드에 기반한 완벽하게 통제된 난수 추출
                int randomIndex = rng.NextInt(0, floor.spawnPoints.Count);
                Transform selectedPoint = floor.spawnPoints[randomIndex];

                // 전방(Forward) 축 정렬 및 스폰
                NetworkObject spawnedRadio = Runner.Spawn(
                    radioPrefab,
                    selectedPoint.position,
                    selectedPoint.rotation,
                    PlayerRef.None
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

    // ─── [수동 시동 스위치 (디버그용)] ───────────────────────────────────

    [ContextMenu("Debug/Force Spawn Radios (Seed: 777)")]
    private void DebugForceSpawn()
    {
        if (!Application.isPlaying) return;

        if (HasStateAuthority)
        {
            ApplyAnswerSeed(777);
        }
    }
}