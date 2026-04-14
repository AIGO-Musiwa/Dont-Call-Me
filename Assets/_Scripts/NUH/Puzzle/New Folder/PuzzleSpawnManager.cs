using System.Collections.Generic;
using UnityEngine;

public class PuzzleSpawnManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PuzzleDefinitionDatabase puzzleDefinitionDatabase;  // 퍼즐 정의 데이터베이스
    [SerializeField] private PuzzleProgressManager puzzleProgressManager;        // 퍼즐 진행도 매니저

    [Header("건물 슬롯 세트")]
    [SerializeField] private BuildingPuzzleSlotSet buildingASlots;               // A동 슬롯 세트
    [SerializeField] private BuildingPuzzleSlotSet buildingBSlots;               // B동 슬롯 세트

    [Header("각 건물 퍼즐 갯수")]
    [SerializeField] private int stage1SelectCount = 3;
    [SerializeField] private int stage2SelectCount = 3;


    private readonly List<GameObject> _spawnedObjects = new();                  // 이번 판에 생성한 퍼즐 오브젝트들
    private readonly List<PuzzleInteractableBase> _spawnedStage1Puzzles = new();// 이번 판 1단계 퍼즐 목록
    private readonly List<GameObject> _spawnedStage2Screens = new();            // 이번 판 2단계 퍼즐 화면 목록

    /// <summary>
    /// 한 판의 퍼즐/힌트 랜덤 배치를 실행
    /// </summary>
    [ContextMenu("Spawn Round Puzzles")]
    public void SpawnRoundPuzzles()
    {
        ClearSpawnedObjects();

        if (puzzleDefinitionDatabase == null || puzzleProgressManager == null || buildingASlots == null || buildingBSlots == null)
        {
            Debug.LogWarning("필수 참조가 비어있어 랜덤 배치 시작 불가능");
            return;
        }

        List<PuzzleDefinition> stage1Pool = puzzleDefinitionDatabase.GetDefinitionsByStage(PuzzleDefinition.PuzzleStage.Stage1);
        List<PuzzleDefinition> stage2Pool = puzzleDefinitionDatabase.GetDefinitionsByStage(PuzzleDefinition.PuzzleStage.Stage2);

        List<PuzzleDefinition> aStage1 = SelectUniqueRandom(stage1Pool, stage1SelectCount);
        List<PuzzleDefinition> bStage1 = SelectUniqueRandom(stage1Pool, stage1SelectCount);
        List<PuzzleDefinition> aStage2 = SelectUniqueRandom(stage2Pool, stage2SelectCount);
        List<PuzzleDefinition> bStage2 = SelectUniqueRandom(stage2Pool, stage2SelectCount);

        SpawnStageGroup(
            buildingASlots.BuildingName,
            aStage1,
            buildingASlots.Stage1PuzzleSlots,
            buildingBSlots.Stage1HintSlots,
            true);

        SpawnStageGroup(
            buildingBSlots.BuildingName,
            bStage1,
            buildingBSlots.Stage1PuzzleSlots,
            buildingASlots.Stage1HintSlots,
            true);

        SpawnStageGroup(
            buildingASlots.BuildingName,
            aStage2,
            buildingASlots.Stage2PuzzleSlots,
            buildingBSlots.Stage2HintSlots,
            false);

        SpawnStageGroup(
            buildingBSlots.BuildingName,
            bStage2,
            buildingBSlots.Stage2PuzzleSlots,
            buildingASlots.Stage2HintSlots,
            false);

        puzzleProgressManager.InitializeRound(_spawnedStage1Puzzles, _spawnedStage2Screens);

        Debug.Log($"퍼즐 랜덤 배치 완료. 1단계 퍼즐 {_spawnedStage1Puzzles.Count}개, 2단계 퍼즐 화면 {_spawnedStage2Screens.Count}개 배치");
    }

    /// <summary>
    /// 후보 풀에서 중복 없이 count개 선택
    /// 건물 내부 퍼즐 중복 방지
    /// </summary>
    private List<PuzzleDefinition> SelectUniqueRandom(List<PuzzleDefinition> pool, int count)
    {
        List<PuzzleDefinition> copied = new(pool);
        List<PuzzleDefinition> selected = new();

        while (copied.Count > 0 && selected.Count < count)
        {
            int randomIndex = Random.Range(0, copied.Count);
            selected.Add(copied[randomIndex]);
            copied.RemoveAt(randomIndex);
        }

        return selected;
    }

    private void SpawnStageGroup(
        string buildingName,
        List<PuzzleDefinition> selectedDefinitions,
        List<PuzzlePlacementSlot> puzzleSlots,
        List<HintPlacementSlot> oppositeHintSlots,
        bool collectStage1Progress)
    {
        int spawnCount = Mathf.Min(selectedDefinitions.Count, puzzleSlots.Count, oppositeHintSlots.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            PuzzleDefinition definition = selectedDefinitions[i];
            PuzzlePlacementSlot puzzleSlot = puzzleSlots[i];
            HintPlacementSlot hintSlot = oppositeHintSlots[i];

            if (definition == null || puzzleSlot == null || hintSlot == null)
                continue;

            // 퍼즐 본체 배치
            GameObject spawnedPuzzle = SpawnPrefabAt(definition.PuzzlePrefab, puzzleSlot.transform);
            if (spawnedPuzzle != null)
            {
                RegisterSpawnedPuzzle(spawnedPuzzle, collectStage1Progress);
                Debug.Log($"{buildingName} 퍼즐 배치 : {definition.PuzzleId} -> {puzzleSlot.SlotId}");
            }

            // 반대편 힌트 배치
            GameObject spawnedHint = SpawnPrefabAt(definition.HintPrefab, hintSlot.transform);
            if (spawnedHint != null)
            {
                Debug.Log($"{buildingName} 힌트 교차 배치 : {definition.PuzzleId} -> {hintSlot.SlotId}");
            }
        }
    }

    /// <summary>
    /// 프리팹을 슬롯 위치에 생성하고 생성 목록에 기록
    /// </summary>
    private GameObject SpawnPrefabAt(GameObject prefab, Transform slotTransform)
    {
        if (prefab == null || slotTransform == null)
            return null;

        GameObject instance = Instantiate(prefab, slotTransform.position, slotTransform.rotation, slotTransform);
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
        {
            _spawnedStage1Puzzles.Add(entry.ProgressTarget);
        }

        if (!collectStage1Progress)
        {
            _spawnedStage2Screens.Add(entry.Stage2ScreenRoot);
        }
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
}
