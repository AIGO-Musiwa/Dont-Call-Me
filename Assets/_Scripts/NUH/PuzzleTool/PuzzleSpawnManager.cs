using System.Collections.Generic;
using UnityEngine;

public class PuzzleSpawnManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PuzzleDefinitionDatabase puzzleDefinitionDatabase;     // 퍼즐 정의 데이터베이스
    [SerializeField] private PuzzleProgressManager puzzleProgressManager;           // 퍼즐 진행도 매니저
    [SerializeField] private RoundSeedManager roundSeedManager;                     // 라운드 시드 매니저

    [Header("건물 슬롯 세트")]
    [SerializeField] private ZonePuzzleSlotSet aZoneSlots;                          // A동 슬롯 세트
    [SerializeField] private ZonePuzzleSlotSet bZoneSlots;                          // B동 슬롯 세트

    [Header("각 건물 퍼즐 갯수")]
    [SerializeField] private int stage1SelectCount = 3;
    [SerializeField] private int stage2SelectCount = 3;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                            // 디버그 로그 출력 여부


    private readonly List<GameObject> _spawnedObjects = new();                      // 이번 판에 생성한 퍼즐 오브젝트들
    private readonly List<PuzzleInteractableBase> _spawnedStage1Puzzles = new();    // 이번 판 1단계 퍼즐 목록
    private readonly List<GameObject> _spawnedStage2Screens = new();                // 이번 판 2단계 퍼즐 화면 목록

    /// <summary>
    /// 한 판의 퍼즐/힌트 랜덤 배치를 실행
    /// </summary>
    [ContextMenu("Spawn Round Puzzles")]
    public void SpawnRoundPuzzles()
    {
        ClearSpawnedObjects();

        if (puzzleDefinitionDatabase == null || puzzleProgressManager == null || roundSeedManager == null ||aZoneSlots == null || bZoneSlots == null)
        {
            Debug.LogWarning("필수 참조가 비어있어 랜덤 배치 시작 불가능");
            return;
        }

        roundSeedManager.EnsureRoundSeed();

        int roundSeed = roundSeedManager.CurrentSeed;
        if(roundSeed == 0)
        {
            LogWarning("유효한 라운드 시드가 없어 퍼즐 배치를 진행할 수 없음");
            return;
        }

        RoundGenerationResult result = RoundGenerator.GeneratePuzzlePlans(
            roundSeed,
            puzzleDefinitionDatabase,
            aZoneSlots,
            bZoneSlots,
            stage1SelectCount,
            stage2SelectCount);

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

            if (plan.PuzzleSlotIndex < 0 || plan.PuzzleSlotIndex > puzzleSlots.Count)
                continue;

            if (plan.HintSlotIndex < 0 || plan.HintSlotIndex > hintSlots.Count)
                continue;

            PuzzlePlacementSlot puzzleSlot = puzzleSlots[plan.PuzzleSlotIndex];
            HintPlacementSlot hintSlot = hintSlots[plan.HintSlotIndex];

            if (puzzleSlot == null || hintSlot == null)
                continue;

            // 퍼즐 배치
            GameObject spawnedPuzzle = SpawnPrefabAt(
                plan.Definition.PuzzlePrefab,
                puzzleSlot.transform,
                plan.Definition.PuzzlePositionOffset,
                plan.Definition.PuzzleRotationOffset);

            if(spawnedPuzzle != null)
            {
                ApplyAnswerSeedToPuzzle(spawnedPuzzle, plan.AnswerSeed);

                RegisterSpawnedPuzzle(spawnedPuzzle, plan.Stage == PuzzleStage.Stage1);
                Log($"{plan.Zone} 퍼즐 배치 : {plan.Definition.PuzzleId} -> {puzzleSlot.SlotId} | AnswerSeed = {plan.AnswerSeed}");
            }

            // 힌트 배치
            GameObject spawnedHint = SpawnPrefabAt(
                plan.Definition.HintPrefab,
                hintSlot.transform,
                plan.Definition.HintPositionOffset,
                plan.Definition.HintRotationOffset);

            if(spawnedHint != null)
            {
                Log($"{GetHintZone(plan.Zone)} 힌트 교차 배치 : {plan.Definition.PuzzleId} -> {hintSlot.SlotId}");
            }
        }
        

        puzzleProgressManager.InitializeRound(_spawnedStage1Puzzles, _spawnedStage2Screens);

        Debug.Log($"퍼즐 랜덤 배치 완료. 1단계 퍼즐 {_spawnedStage1Puzzles.Count}개, 2단계 퍼즐 화면 {_spawnedStage2Screens.Count}개 배치");
    }

    private void ApplyAnswerSeedToPuzzle(GameObject spawnedPuzzle, int answerSeed)
    {
        if (spawnedPuzzle == null)
            return;

        IPuzzleSeedReceiver[] receivers = spawnedPuzzle.GetComponentsInChildren<IPuzzleSeedReceiver>(true);
        for(int i = 0; i < receivers.Length; i++)
            receivers[i].ApplyAnswerSeed(answerSeed);
    }

    /// <summary>
    /// 프리팹을 슬롯 위치에 생성하고 로컬 오프셋 적용 후 생성 목록에 기록
    /// </summary>
    private GameObject SpawnPrefabAt(GameObject prefab, Transform slotTransform, Vector3 posOffset, Vector3 rotOffset)
    {
        if (prefab == null || slotTransform == null)
            return null;

        GameObject instance = Instantiate(prefab, slotTransform.position, slotTransform.rotation, slotTransform);

        instance.transform.localPosition = posOffset;
        instance.transform.localRotation = Quaternion.Euler(rotOffset);

        _spawnedObjects.Add(instance);
        return instance;
    }

    /// <summary>
    /// 스폰된 퍼즐 오브젝트에서 진행도 대표 퍼즐과 2단계 화면 대상을 추출
    /// </summary>
    private void RegisterSpawnedPuzzle(GameObject spawnedPuzzle, bool collectStage1Progress)
    {
        if (spawnedPuzzle == null)
            return;

        PuzzleSpawnEntry entry = spawnedPuzzle.GetComponent<PuzzleSpawnEntry>();
        if (entry == null)
        {
            Debug.LogWarning($"PuzzleSpawnEntry 누락 : {spawnedPuzzle.name}");
            return;
        }

        if(collectStage1Progress && entry.ProgressTarget != null)
          _spawnedStage1Puzzles.Add(entry.ProgressTarget);

        if (!collectStage1Progress)
            _spawnedStage2Screens.Add(entry.Stage2ScreenRoot);
    }

    /// <summary>
    /// Zone에 맞는 슬롯 세트 반환
    /// </summary>
    private ZonePuzzleSlotSet GetZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? aZoneSlots : bZoneSlots;
    }

    /// <summary>
    /// 힌트(반대) Zone의 슬롯 세트 반환
    /// </summary>
    /// <param name="zone"></param>
    /// <returns></returns>
    private ZonePuzzleSlotSet GetHintZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? bZoneSlots : aZoneSlots;
    }

    /// <summary>
    /// 반대 Zone enum 반환
    /// </summary>
    private Zone GetHintZone(Zone zone)
    {
        return zone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
    }

    /// <summary>
    /// 단계에 맞는 퍼즐 슬롯 리스트 반환
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
    /// 이전 판에 생성했던 퍼즐/힌트 오브젝트를 전부 제거
    /// </summary>
    private void ClearSpawnedObjects()
    {
        for(int i = 0; i < _spawnedObjects.Count; i++)
        {
            if (_spawnedObjects[i] != null)
                Destroy(_spawnedObjects[i]);
        }

        _spawnedObjects.Clear();
        _spawnedStage1Puzzles.Clear();
        _spawnedStage2Screens.Clear();
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
