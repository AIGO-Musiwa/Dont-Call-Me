using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fusion 기준 퍼즐/힌트 랜덤 배치 매니저
/// 인게임 씬 진입 후 서버가 자동으로 퍼즐/힌트를 스폰한다
/// 
/// 역할
/// - 퍼즐/힌트 랜덤 배치
/// - 스폰된 퍼즐을 Zone / Stage 기준으로 분류
/// - PuzzleProgressManager에 이번 판 진행도 등록 데이터를 전달
/// 
/// 주의
/// - 진행도 판정은 하지 않는다
/// - Stage 변경도 하지 않는다
/// - 맵 변화도 하지 않는다
/// </summary>
public class PuzzleSpawnManager : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private PuzzleDefinitionDatabase puzzleDefinitionDatabase; // 퍼즐 정의 데이터베이스
    [SerializeField] private PuzzleProgressManager puzzleProgressManager;       // 퍼즐 진행도 매니저
    [SerializeField] private RoundSeedManager roundSeedManager;                 // 라운드 시드 매니저

    [Header("구역 슬롯 세트")]
    [SerializeField] private ZonePuzzleSlotSet aZoneSlots;                      // A동 슬롯 세트
    [SerializeField] private ZonePuzzleSlotSet bZoneSlots;                      // B동 슬롯 세트

    [Header("각 구역 퍼즐 개수")]
    [SerializeField] private int stage1SelectCount = 3;                         // 각 구역 1단계 퍼즐 개수
    [SerializeField] private int stage2SelectCount = 3;                         // 각 구역 2단계 퍼즐 개수

    [Header("자동 시작")]
    [SerializeField] private bool autoSpawnOnSpawned = true;                    // Spawned 시 자동 스폰 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                        // 디버그 로그 출력 여부

    private readonly List<NetworkObject> _spawnedObjects = new();               // 이번 판 스폰된 네트워크 오브젝트들

    // ZoneA 진행도 등록용 1단계 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new();

    // ZoneB 진행도 등록용 1단계 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new();

    // ZoneA 2단계 화면 목록
    private readonly List<GameObject> _zoneAStage2Screens = new();

    // ZoneB 2단계 화면 목록
    private readonly List<GameObject> _zoneBStage2Screens = new();

    private bool _hasSpawnedRound;                                              // 이미 한 번 스폰했는지

    public override void Spawned()
    {
        Log($"Spawned 호출 | IsServer={Runner.IsServer} | HasStateAuthority={HasStateAuthority}");

        if (!autoSpawnOnSpawned)
            return;

        if (!Runner.IsServer)
        {
            Log("클라이언트 인스턴스이므로 자동 스폰하지 않음");
            return;
        }

        TrySpawnRoundPuzzles();
    }

    /// <summary>
    /// 서버에서 한 판의 퍼즐/힌트 랜덤 배치를 실행한다.
    /// </summary>
    [ContextMenu("Spawn Round Puzzles")]
    public void TrySpawnRoundPuzzles()
    {
        if (_hasSpawnedRound)
        {
            LogWarning("이미 이번 라운드 퍼즐 스폰이 완료되어 중복 실행을 막음");
            return;
        }

        if (Runner == null)
        {
            LogWarning("Runner가 없어 퍼즐 랜덤 배치를 시작할 수 없습니다.");
            return;
        }

        if (!Runner.IsServer)
        {
            LogWarning("PuzzleSpawnManager는 서버에서만 실행해야 합니다.");
            return;
        }

        if (puzzleDefinitionDatabase == null ||
            puzzleProgressManager == null ||
            roundSeedManager == null ||
            aZoneSlots == null ||
            bZoneSlots == null)
        {
            LogWarning("필수 참조가 비어 있어 랜덤 배치를 시작할 수 없습니다.");
            return;
        }

        ClearSpawnedObjects();

        roundSeedManager.EnsureRoundSeed();

        int roundSeed = roundSeedManager.CurrentSeed;
        if (roundSeed == 0)
        {
            LogWarning("유효한 라운드 시드가 없어 퍼즐 배치를 진행할 수 없습니다.");
            return;
        }

        RoundGenerationResult result = RoundGenerator.GeneratePuzzlePlans(
            roundSeed,
            puzzleDefinitionDatabase,
            aZoneSlots,
            bZoneSlots,
            stage1SelectCount,
            stage2SelectCount);

        Log($"RoundGenerationResult 생성 완료 | PuzzlePlans={result.PuzzlePlans.Count}");

        for (int i = 0; i < result.PuzzlePlans.Count; i++)
        {
            RoundGenerationResult.PuzzleSpawnPlan plan = result.PuzzlePlans[i];
            if (plan == null || plan.Definition == null)
                continue;

            ZonePuzzleSlotSet puzzleZoneSet = GetZoneSlotSet(plan.Zone);
            ZonePuzzleSlotSet hintZoneSet = GetHintZoneSlotSet(plan.Zone);

            if (puzzleZoneSet == null || hintZoneSet == null)
                continue;

            List<PuzzlePlacementSlot> puzzleSlots = GetPuzzleSlotsByStage(puzzleZoneSet, plan.Stage);
            List<HintPlacementSlot> hintSlots = GetHintSlotsByStage(hintZoneSet, plan.Stage);

            if (puzzleSlots == null || hintSlots == null)
                continue;

            if (plan.PuzzleSlotIndex < 0 || plan.PuzzleSlotIndex >= puzzleSlots.Count)
                continue;

            if (plan.HintSlotIndex < 0 || plan.HintSlotIndex >= hintSlots.Count)
                continue;

            PuzzlePlacementSlot puzzleSlot = puzzleSlots[plan.PuzzleSlotIndex];
            HintPlacementSlot hintSlot = hintSlots[plan.HintSlotIndex];

            if (puzzleSlot == null || hintSlot == null)
                continue;

            NetworkObject spawnedPuzzle = SpawnNetworkPrefabAt(
                plan.Definition.PuzzlePrefab,
                puzzleSlot.transform,
                plan.Definition.PuzzlePositionOffset,
                plan.Definition.PuzzleRotationOffset);

            if (spawnedPuzzle != null)
            {
                ApplyAnswerSeedToSpawnedObject(spawnedPuzzle, plan.AnswerSeed);

                // Zone / Stage 정보를 같이 넘겨서 등록한다.
                RegisterSpawnedPuzzle(spawnedPuzzle.gameObject, plan.Zone, plan.Stage);

                Log($"{plan.Zone} 퍼즐 배치 : {plan.Definition.PuzzleId} -> {puzzleSlot.SlotId} | Stage={plan.Stage} | AnswerSeed={plan.AnswerSeed}");
            }

            NetworkObject spawnedHint = SpawnNetworkPrefabAt(
                plan.Definition.HintPrefab,
                hintSlot.transform,
                plan.Definition.HintPositionOffset,
                plan.Definition.HintRotationOffset);

            if (spawnedHint != null)
            {
                ApplyAnswerSeedToSpawnedObject(spawnedHint, plan.AnswerSeed);
                Log($"{GetHintZone(plan.Zone)} 힌트 교차 배치 : {plan.Definition.PuzzleId} -> {hintSlot.SlotId} | AnswerSeed={plan.AnswerSeed}");
            }

            // LightPattern 퍼즐은 힌트와 직접 연결이 필요하다.
            TryLinkLightPatternPair(spawnedPuzzle, spawnedHint);
        }

        // 이번 판 등록 데이터를 zone별로 넘긴다.
        puzzleProgressManager.InitializeRound(
            _zoneAStage1Puzzles,
            _zoneBStage1Puzzles,
            _zoneAStage2Screens,
            _zoneBStage2Screens);

        _hasSpawnedRound = true;

        Log($"퍼즐 랜덤 배치 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count} | ZoneB Stage1={_zoneBStage1Puzzles.Count} | ZoneA Stage2Screen={_zoneAStage2Screens.Count} | ZoneB Stage2Screen={_zoneBStage2Screens.Count}");
    }

    /// <summary>
    /// 네트워크 프리팹을 슬롯 위치 기준으로 Runner.Spawn 한다.
    /// </summary>
    private NetworkObject SpawnNetworkPrefabAt(
        NetworkObject prefab,
        Transform slotTransform,
        Vector3 localPositionOffset,
        Vector3 localRotationOffset)
    {
        if (prefab == null || slotTransform == null)
            return null;

        Vector3 worldPosition = slotTransform.TransformPoint(localPositionOffset);
        Quaternion worldRotation = slotTransform.rotation * Quaternion.Euler(localRotationOffset);

        NetworkObject spawned = Runner.Spawn(prefab, worldPosition, worldRotation, null);
        if (spawned != null)
            _spawnedObjects.Add(spawned);

        return spawned;
    }

    /// <summary>
    /// 스폰된 오브젝트에 정답 시드를 적용한다.
    /// PuzzleSeedSync가 붙어 있어야 한다.
    /// </summary>
    private void ApplyAnswerSeedToSpawnedObject(NetworkObject spawnedObject, int answerSeed)
    {
        if (spawnedObject == null)
            return;

        PuzzleSeedSync seedSync = spawnedObject.GetComponent<PuzzleSeedSync>();
        if (seedSync == null)
        {
            LogWarning($"PuzzleSeedSync 누락 : {spawnedObject.name}");
            return;
        }

        seedSync.ServerSetAnswerSeed(answerSeed);
    }

    /// <summary>
    /// 스폰된 퍼즐을 Zone / Stage 기준으로 진행도 매니저 등록용 데이터에 분류한다.
    /// </summary>
    private void RegisterSpawnedPuzzle(GameObject spawnedPuzzle, Zone zone, PuzzleStage stage)
    {
        if (spawnedPuzzle == null)
            return;

        PuzzleSpawnEntry entry = spawnedPuzzle.GetComponent<PuzzleSpawnEntry>();
        if (entry == null)
        {
            LogWarning($"PuzzleSpawnEntry 누락 : {spawnedPuzzle.name}");
            return;
        }

        // 1단계 퍼즐은 진행도 집계 대상으로 등록한다.
        if (stage == PuzzleStage.Stage1 && entry.ProgressTarget != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage1Puzzles.Add(entry.ProgressTarget);
            else
                _zoneBStage1Puzzles.Add(entry.ProgressTarget);

            return;
        }

        // 2단계 퍼즐은 화면 루트를 등록한다.
        if (stage == PuzzleStage.Stage2 && entry.Stage2ScreenRoot != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage2Screens.Add(entry.Stage2ScreenRoot);
            else
                _zoneBStage2Screens.Add(entry.Stage2ScreenRoot);
        }
    }

    /// <summary>
    /// LightPattern 퍼즐과 힌트가 함께 스폰된 경우 서로 연결한다.
    /// </summary>
    private void TryLinkLightPatternPair(NetworkObject spawnedPuzzle, NetworkObject spawnedHint)
    {
        if (spawnedPuzzle == null || spawnedHint == null)
            return;

        LightPatternPuzzle puzzle = spawnedPuzzle.GetComponent<LightPatternPuzzle>();
        LightPatternHint hint = spawnedHint.GetComponent<LightPatternHint>();

        if (puzzle == null || hint == null)
            return;

        hint.SetObservedPuzzle(puzzle);

        Log($"LightPattern 퍼즐-힌트 연결 완료 | Puzzle={spawnedPuzzle.name} | Hint={spawnedHint.name}");
    }

    /// <summary>
    /// Zone에 맞는 슬롯 세트를 반환한다.
    /// </summary>
    private ZonePuzzleSlotSet GetZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? aZoneSlots : bZoneSlots;
    }

    /// <summary>
    /// 힌트는 반대편 Zone에 배치되므로 반대쪽 슬롯 세트를 반환한다.
    /// </summary>
    private ZonePuzzleSlotSet GetHintZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? bZoneSlots : aZoneSlots;
    }

    /// <summary>
    /// 힌트가 실제로 어느 Zone에 배치되는지 반환한다.
    /// </summary>
    private Zone GetHintZone(Zone zone)
    {
        return zone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
    }

    /// <summary>
    /// Stage에 맞는 퍼즐 슬롯 목록을 반환한다.
    /// </summary>
    private List<PuzzlePlacementSlot> GetPuzzleSlotsByStage(ZonePuzzleSlotSet zoneSlotSet, PuzzleStage stage)
    {
        if (zoneSlotSet == null)
            return null;

        return stage switch
        {
            PuzzleStage.Stage1 => zoneSlotSet.Stage1PuzzleSlots,
            PuzzleStage.Stage2 => zoneSlotSet.Stage2PuzzleSlots,
            _ => null
        };
    }

    /// <summary>
    /// Stage에 맞는 힌트 슬롯 목록을 반환한다.
    /// </summary>
    private List<HintPlacementSlot> GetHintSlotsByStage(ZonePuzzleSlotSet zoneSlotSet, PuzzleStage stage)
    {
        if (zoneSlotSet == null)
            return null;

        return stage switch
        {
            PuzzleStage.Stage1 => zoneSlotSet.Stage1HintSlots,
            PuzzleStage.Stage2 => zoneSlotSet.Stage2HintSlots,
            _ => null
        };
    }

    /// <summary>
    /// 이전 판에 스폰했던 네트워크 오브젝트를 전부 제거하고
    /// 등록용 캐시도 초기화한다.
    /// </summary>
    private void ClearSpawnedObjects()
    {
        for (int i = 0; i < _spawnedObjects.Count; i++)
        {
            NetworkObject obj = _spawnedObjects[i];
            if (obj == null)
                continue;

            if (obj.IsValid)
                Runner.Despawn(obj);
        }

        _spawnedObjects.Clear();

        _zoneAStage1Puzzles.Clear();
        _zoneBStage1Puzzles.Clear();

        _zoneAStage2Screens.Clear();
        _zoneBStage2Screens.Clear();

        _hasSpawnedRound = false;
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleSpawnManager] {message}", this);
    }

    /// <summary>
    /// 경고 로그 출력.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PuzzleSpawnManager] {message}", this);
    }
}