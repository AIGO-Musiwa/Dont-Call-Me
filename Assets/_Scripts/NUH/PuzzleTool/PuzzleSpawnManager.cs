using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fusion 기준 퍼즐/힌트 랜덤 배치 매니저
/// 인게임 씬 진입 후 서버가 자동으로 퍼즐/힌트를 스폰한다.
/// 
/// 역할
/// - 퍼즐/힌트 랜덤 배치
/// - 스폰된 퍼즐을 Zone / Stage 기준으로 분류
/// - PuzzleProgressManager에 이번 판 진행도 등록 데이터를 전달
/// </summary>
public class PuzzleSpawnManager : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private PuzzleDefinitionDatabase puzzleDefinitionDatabase;
    [SerializeField] private PuzzleProgressManager puzzleProgressManager;
    [SerializeField] private RoundSeedManager roundSeedManager;

    [Header("구역 슬롯 세트")]
    [SerializeField] private ZonePuzzleSlotSet aZoneSlots;
    [SerializeField] private ZonePuzzleSlotSet bZoneSlots;

    [Header("각 구역 퍼즐 개수")]
    [SerializeField] private int stage1SelectCount = 3;
    [SerializeField] private int stage2SelectCount = 3;

    [Header("자동 시작")]
    [SerializeField] private bool autoSpawnOnSpawned = true;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<NetworkObject> _spawnedObjects = new();

    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new();
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new();
    private readonly List<GameObject> _zoneAStage2Screens = new();
    private readonly List<GameObject> _zoneBStage2Screens = new();

    private bool _hasSpawnedRound;

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

            PuzzlePlacementSlot puzzleSlot = puzzleSlots[plan.PuzzleSlotIndex];
            if (puzzleSlot == null)
                continue;

            NetworkObject spawnedPuzzle = SpawnNetworkPrefabAt(
                plan.Definition.PuzzlePrefab,
                puzzleSlot.transform,
                plan.Definition.PuzzlePositionOffset,
                plan.Definition.PuzzleRotationOffset);

            if (spawnedPuzzle != null)
            {
                ApplyAnswerSeedToSpawnedObject(spawnedPuzzle, plan.AnswerSeed);
                RegisterSpawnedPuzzle(spawnedPuzzle.gameObject, plan.Zone, plan.Stage);

                Log($"{plan.Zone} 퍼즐 배치 : {plan.Definition.PuzzleId} -> {puzzleSlot.SlotId} | Stage={plan.Stage} | AnswerSeed={plan.AnswerSeed}");
            }

            // 연결된 힌트 여러 개를 각각 스폰
            for (int hintIndex = 0; hintIndex < plan.HintPlans.Count; hintIndex++)
            {
                RoundGenerationResult.HintSpawnPlan hintPlan = plan.HintPlans[hintIndex];
                if (hintPlan == null || hintPlan.HintPrefab == null)
                    continue;

                if (hintPlan.HintSlotIndex < 0 || hintPlan.HintSlotIndex >= hintSlots.Count)
                    continue;

                HintPlacementSlot hintSlot = hintSlots[hintPlan.HintSlotIndex];
                if (hintSlot == null)
                    continue;

                NetworkObject spawnedHint = SpawnNetworkPrefabAt(
                    hintPlan.HintPrefab,
                    hintSlot.transform,
                    hintPlan.HintPositionOffset,
                    hintPlan.HintRotationOffset);

                if (spawnedHint != null)
                {
                    ApplyAnswerSeedToSpawnedObject(spawnedHint, plan.AnswerSeed);
                    TryLinkLightPatternPair(spawnedPuzzle, spawnedHint);

                    Log($"{GetHintZone(plan.Zone)} 힌트 배치 : {plan.Definition.PuzzleId}/{hintPlan.HintId} -> {hintSlot.SlotId} | AnswerSeed={plan.AnswerSeed}");
                }
            }
        }

        puzzleProgressManager.InitializeRound(
            _zoneAStage1Puzzles,
            _zoneBStage1Puzzles,
            _zoneAStage2Screens,
            _zoneBStage2Screens);

        _hasSpawnedRound = true;

        Log($"퍼즐 랜덤 배치 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count} | ZoneB Stage1={_zoneBStage1Puzzles.Count} | ZoneA Stage2Screen={_zoneAStage2Screens.Count} | ZoneB Stage2Screen={_zoneBStage2Screens.Count}");
    }

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

        if (stage == PuzzleStage.Stage1 && entry.ProgressTarget != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage1Puzzles.Add(entry.ProgressTarget);
            else
                _zoneBStage1Puzzles.Add(entry.ProgressTarget);

            return;
        }

        if (stage == PuzzleStage.Stage2 && entry.Stage2ScreenRoot != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage2Screens.Add(entry.Stage2ScreenRoot);
            else
                _zoneBStage2Screens.Add(entry.Stage2ScreenRoot);
        }
    }

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

    private ZonePuzzleSlotSet GetZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? aZoneSlots : bZoneSlots;
    }

    private ZonePuzzleSlotSet GetHintZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? bZoneSlots : aZoneSlots;
    }

    private Zone GetHintZone(Zone zone)
    {
        return zone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
    }

    private List<PuzzlePlacementSlot> GetPuzzleSlotsByStage(ZonePuzzleSlotSet zoneSlotSet, PuzzleStage stage)
    {
        if (zoneSlotSet == null)
            return null;

        return stage switch
        {
            PuzzleStage.Stage1 => zoneSlotSet.Stage1PuzzleSlots,
            PuzzleStage.Stage2 => zoneSlotSet.Stage2PuzzleSlots,
            PuzzleStage.Stage3 => zoneSlotSet.Stage3PuzzleSlots,
            _ => null
        };
    }

    private List<HintPlacementSlot> GetHintSlotsByStage(ZonePuzzleSlotSet zoneSlotSet, PuzzleStage stage)
    {
        if (zoneSlotSet == null)
            return null;

        return stage switch
        {
            PuzzleStage.Stage1 => zoneSlotSet.Stage1HintSlots,
            PuzzleStage.Stage2 => zoneSlotSet.Stage2HintSlots,
            PuzzleStage.Stage3 => zoneSlotSet.Stage3HintSlots,
            _ => null
        };
    }

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

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleSpawnManager] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PuzzleSpawnManager] {message}", this);
    }
}