using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fusion 기준 퍼즐/힌트 랜덤 배치 매니저
/// 인게임 씬 진입 후 서버가 자동으로 퍼즐/힌트를 스폰한다
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
    private readonly List<PuzzleInteractableBase> _spawnedStage1Puzzles = new();// 이번 판 1단계 퍼즐 목록
    private readonly List<GameObject> _spawnedStage2Screens = new();            // 이번 판 2단계 화면 목록

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
    /// 서버에서 한 판의 퍼즐/힌트 랜덤 배치를 실행
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

        if (puzzleDefinitionDatabase == null || puzzleProgressManager == null || roundSeedManager == null || aZoneSlots == null || bZoneSlots == null)
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
                RegisterSpawnedPuzzle(spawnedPuzzle.gameObject, plan.Stage == PuzzleStage.Stage1);

                Log($"{plan.Zone} 퍼즐 배치 : {plan.Definition.PuzzleId} -> {puzzleSlot.SlotId} | AnswerSeed={plan.AnswerSeed}");
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
        }

        puzzleProgressManager.InitializeRound(_spawnedStage1Puzzles, _spawnedStage2Screens);
        _hasSpawnedRound = true;

        Log($"퍼즐 랜덤 배치 완료 | 1단계 퍼즐={_spawnedStage1Puzzles.Count} | 2단계 화면={_spawnedStage2Screens.Count}");
    }

    /// <summary>
    /// 네트워크 프리팹을 슬롯 위치 기준으로 Runner.Spawn 한다
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
        {
            _spawnedObjects.Add(spawned);
        }

        return spawned;
    }

    /// <summary>
    /// 스폰된 오브젝트에 정답 시드를 적용
    /// PuzzleSeedSync가 붙어 있어야 한다
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
    /// 스폰된 퍼즐에서 진행도 대표 퍼즐과 2단계 화면 루트를 추출
    /// </summary>
    private void RegisterSpawnedPuzzle(GameObject spawnedPuzzle, bool collectStage1Progress)
    {
        if (spawnedPuzzle == null)
            return;

        PuzzleSpawnEntry entry = spawnedPuzzle.GetComponent<PuzzleSpawnEntry>();
        if (entry == null)
        {
            LogWarning($"PuzzleSpawnEntry 누락 : {spawnedPuzzle.name}");
            return;
        }

        if (collectStage1Progress && entry.ProgressTarget != null)
            _spawnedStage1Puzzles.Add(entry.ProgressTarget);

        if (!collectStage1Progress && entry.Stage2ScreenRoot != null)
            _spawnedStage2Screens.Add(entry.Stage2ScreenRoot);
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
            _ => null
        };
    }

    /// <summary>
    /// 이전 판에 스폰했던 네트워크 오브젝트를 전부 제거
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
        _spawnedStage1Puzzles.Clear();
        _spawnedStage2Screens.Clear();
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