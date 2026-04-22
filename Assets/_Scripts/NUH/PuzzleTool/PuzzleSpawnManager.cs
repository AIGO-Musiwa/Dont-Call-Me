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
    [SerializeField] private PuzzleDefinitionDatabase puzzleDefinitionDatabase; // 단계별 퍼즐 정의 DB
    [SerializeField] private PuzzleProgressManager puzzleProgressManager;       // 진행도 등록 대상
    [SerializeField] private RoundSeedManager roundSeedManager;                 // 이번 판 시드 관리

    [Header("구역 슬롯 세트")]
    [SerializeField] private ZonePuzzleSlotSet aZoneSlots; // A존 슬롯 세트
    [SerializeField] private ZonePuzzleSlotSet bZoneSlots; // B존 슬롯 세트

    [Header("각 구역 퍼즐 개수")]
    [SerializeField] private int stage1SelectCount = 3; // 존당 1단계 퍼즐 선택 개수
    [SerializeField] private int stage2SelectCount = 3; // 존당 2단계 퍼즐 선택 개수

    [Header("자동 시작")]
    [SerializeField] private bool autoSpawnOnSpawned = true; // Spawned에서 자동 시작할지 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private readonly List<NetworkObject> _spawnedObjects = new(); // 이번 라운드에 스폰한 오브젝트 목록

    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new(); // A존 1단계 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new(); // B존 1단계 진행도 대상 퍼즐 목록
    private readonly List<GameObject> _zoneAStage2Screens = new();             // A존 2단계 스크린 루트 목록
    private readonly List<GameObject> _zoneBStage2Screens = new();             // B존 2단계 스크린 루트 목록

    private bool _hasSpawnedRound; // 이번 라운드 스폰 완료 여부

    public override void Spawned()
    {
        Log($"Spawned 호출 | IsServer={Runner.IsServer} | HasStateAuthority={HasStateAuthority}");

        if (!autoSpawnOnSpawned)
            return; // 자동 시작 옵션 꺼져 있으면 종료

        if (!Runner.IsServer)
        {
            Log("클라이언트 인스턴스이므로 자동 스폰하지 않음");
            return; // 서버만 실제 스폰 수행
        }

        TrySpawnRoundPuzzles(); // 자동 스폰 실행
    }

    [ContextMenu("Spawn Round Puzzles")]
    public void TrySpawnRoundPuzzles()
    {
        if (_hasSpawnedRound)
        {
            LogWarning("이미 이번 라운드 퍼즐 스폰이 완료되어 중복 실행을 막음");
            return; // 중복 스폰 방지
        }

        if (Runner == null)
        {
            LogWarning("Runner가 없어 퍼즐 랜덤 배치를 시작할 수 없습니다.");
            return; // Runner 없으면 네트워크 스폰 불가
        }

        if (!Runner.IsServer)
        {
            LogWarning("PuzzleSpawnManager는 서버에서만 실행해야 합니다.");
            return; // 서버만 스폰 가능
        }

        if (puzzleDefinitionDatabase == null ||
            puzzleProgressManager == null ||
            roundSeedManager == null ||
            aZoneSlots == null ||
            bZoneSlots == null)
        {
            LogWarning("필수 참조가 비어 있어 랜덤 배치를 시작할 수 없습니다.");
            return; // 필수 참조 누락 방지
        }

        ClearSpawnedObjects(); // 이전 라운드 잔여물 정리

        roundSeedManager.EnsureRoundSeed(); // 시드 생성 보장

        int roundSeed = roundSeedManager.CurrentSeed; // 현재 라운드 시드 가져오기
        if (roundSeed == 0)
        {
            LogWarning("유효한 라운드 시드가 없어 퍼즐 배치를 진행할 수 없습니다.");
            return; // 0 시드는 방어적으로 차단
        }

        RoundGenerationResult result = RoundGenerator.GeneratePuzzlePlans(
            roundSeed,
            puzzleDefinitionDatabase,
            aZoneSlots,
            bZoneSlots,
            stage1SelectCount,
            stage2SelectCount); // 이번 라운드 배치 계획 계산

        Log($"RoundGenerationResult 생성 완료 | PuzzlePlans={result.PuzzlePlans.Count}");

        for (int i = 0; i < result.PuzzlePlans.Count; i++)
        {
            RoundGenerationResult.PuzzleSpawnPlan plan = result.PuzzlePlans[i]; // 퍼즐 1개 배치 계획
            if (plan == null || plan.Definition == null)
                continue; // 잘못된 계획은 스킵

            ZonePuzzleSlotSet puzzleZoneSet = GetZoneSlotSet(plan.Zone);      // 퍼즐 본체가 들어갈 존 슬롯셋
            ZonePuzzleSlotSet hintZoneSet = GetHintZoneSlotSet(plan.Zone);    // 힌트가 들어갈 반대편 존 슬롯셋

            if (puzzleZoneSet == null || hintZoneSet == null)
                continue;

            List<PuzzlePlacementSlot> puzzleSlots = GetPuzzleSlotsByStage(puzzleZoneSet, plan.Stage); // 해당 단계 퍼즐 슬롯 목록
            List<HintPlacementSlot> hintSlots = GetHintSlotsByStage(hintZoneSet, plan.Stage);         // 해당 단계 힌트 슬롯 목록

            if (puzzleSlots == null || hintSlots == null)
                continue;

            if (plan.PuzzleSlotIndex < 0 || plan.PuzzleSlotIndex >= puzzleSlots.Count)
                continue; // 퍼즐 슬롯 인덱스 방어

            PuzzlePlacementSlot puzzleSlot = puzzleSlots[plan.PuzzleSlotIndex]; // 실제 퍼즐 슬롯 가져오기
            if (puzzleSlot == null)
                continue;

            NetworkObject spawnedPuzzle = SpawnNetworkPrefabAt(
                plan.Definition.PuzzlePrefab,
                puzzleSlot.transform,
                plan.Definition.PuzzlePositionOffset,
                plan.Definition.PuzzleRotationOffset); // 퍼즐 본체 스폰

            if (spawnedPuzzle != null)
            {
                ApplyAnswerSeedToSpawnedObject(spawnedPuzzle, plan.AnswerSeed);    // 정답 시드 적용
                RegisterSpawnedPuzzle(spawnedPuzzle.gameObject, plan.Zone, plan.Stage); // 진행도/스크린 등록

                Log($"{plan.Zone} 퍼즐 배치 : {plan.Definition.PuzzleId} -> {puzzleSlot.SlotId} | Stage={plan.Stage} | AnswerSeed={plan.AnswerSeed}");
            }

            // 연결된 힌트 여러 개를 각각 스폰
            for (int hintIndex = 0; hintIndex < plan.HintPlans.Count; hintIndex++)
            {
                RoundGenerationResult.HintSpawnPlan hintPlan = plan.HintPlans[hintIndex]; // 힌트 1개 배치 계획
                if (hintPlan == null || hintPlan.HintPrefab == null)
                    continue;

                if (hintPlan.HintSlotIndex < 0 || hintPlan.HintSlotIndex >= hintSlots.Count)
                    continue; // 힌트 슬롯 인덱스 방어

                HintPlacementSlot hintSlot = hintSlots[hintPlan.HintSlotIndex]; // 실제 힌트 슬롯 가져오기
                if (hintSlot == null)
                    continue;

                NetworkObject spawnedHint = SpawnNetworkPrefabAt(
                    hintPlan.HintPrefab,
                    hintSlot.transform,
                    hintPlan.HintPositionOffset,
                    hintPlan.HintRotationOffset); // 힌트 프리팹 스폰

                if (spawnedHint != null)
                {
                    ApplyAnswerSeedToSpawnedObject(spawnedHint, plan.AnswerSeed); // 퍼즐과 같은 정답 시드 적용
                    TryLinkLightPatternPair(spawnedPuzzle, spawnedHint);          // 라이트패턴 퍼즐 전용 연결 시도

                    Log($"{GetHintZone(plan.Zone)} 힌트 배치 : {plan.Definition.PuzzleId}/{hintPlan.HintId} -> {hintSlot.SlotId} | AnswerSeed={plan.AnswerSeed}");
                }
            }
        }

        puzzleProgressManager.InitializeRound(
            _zoneAStage1Puzzles,
            _zoneBStage1Puzzles,
            _zoneAStage2Screens,
            _zoneBStage2Screens); // 이번 라운드 진행도 정보 등록

        _hasSpawnedRound = true; // 스폰 완료 표시

        Log($"퍼즐 랜덤 배치 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count} | ZoneB Stage1={_zoneBStage1Puzzles.Count} | ZoneA Stage2Screen={_zoneAStage2Screens.Count} | ZoneB Stage2Screen={_zoneBStage2Screens.Count}");
    }

    private NetworkObject SpawnNetworkPrefabAt(
        NetworkObject prefab,
        Transform slotTransform,
        Vector3 localPositionOffset,
        Vector3 localRotationOffset)
    {
        if (prefab == null || slotTransform == null)
            return null; // 필수 참조 방어

        Vector3 worldPosition = slotTransform.TransformPoint(localPositionOffset); // 슬롯 기준 월드 위치 계산
        Quaternion worldRotation = slotTransform.rotation * Quaternion.Euler(localRotationOffset); // 슬롯 기준 월드 회전 계산

        NetworkObject spawned = Runner.Spawn(prefab, worldPosition, worldRotation, null); // 네트워크 스폰
        if (spawned != null)
            _spawnedObjects.Add(spawned); // 나중에 despawn할 목록에 등록

        return spawned;
    }

    private void ApplyAnswerSeedToSpawnedObject(NetworkObject spawnedObject, int answerSeed)
    {
        if (spawnedObject == null)
            return;

        PuzzleSeedSync seedSync = spawnedObject.GetComponent<PuzzleSeedSync>(); // 시드 전달용 컴포넌트 찾기
        if (seedSync == null)
        {
            LogWarning($"PuzzleSeedSync 누락 : {spawnedObject.name}");
            return;
        }

        seedSync.ServerSetAnswerSeed(answerSeed); // 서버 권한으로 정답 시드 적용
    }

    private void RegisterSpawnedPuzzle(GameObject spawnedPuzzle, Zone zone, PuzzleStage stage)
    {
        if (spawnedPuzzle == null)
            return;

        PuzzleSpawnEntry entry = spawnedPuzzle.GetComponent<PuzzleSpawnEntry>(); // 진행도/스크린 등록 정보 찾기
        if (entry == null)
        {
            LogWarning($"PuzzleSpawnEntry 누락 : {spawnedPuzzle.name}");
            return;
        }

        if (stage == PuzzleStage.Stage1 && entry.ProgressTarget != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage1Puzzles.Add(entry.ProgressTarget); // A존 1단계 진행도 목록 등록
            else
                _zoneBStage1Puzzles.Add(entry.ProgressTarget); // B존 1단계 진행도 목록 등록

            return;
        }

        if (stage == PuzzleStage.Stage2 && entry.Stage2ScreenRoot != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage2Screens.Add(entry.Stage2ScreenRoot); // A존 2단계 스크린 등록
            else
                _zoneBStage2Screens.Add(entry.Stage2ScreenRoot); // B존 2단계 스크린 등록
        }
    }

    private void TryLinkLightPatternPair(NetworkObject spawnedPuzzle, NetworkObject spawnedHint)
    {
        if (spawnedPuzzle == null || spawnedHint == null)
            return;

        LightPatternPuzzle puzzle = spawnedPuzzle.GetComponent<LightPatternPuzzle>(); // 라이트패턴 퍼즐 찾기
        LightPatternHint hint = spawnedHint.GetComponent<LightPatternHint>();         // 라이트패턴 힌트 찾기

        if (puzzle == null || hint == null)
            return; // 둘 다 있어야만 연결

        hint.SetObservedPuzzle(puzzle); // 힌트가 관찰할 퍼즐 연결

        Log($"LightPattern 퍼즐-힌트 연결 완료 | Puzzle={spawnedPuzzle.name} | Hint={spawnedHint.name}");
    }

    private ZonePuzzleSlotSet GetZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? aZoneSlots : bZoneSlots; // 퍼즐 본체 존 슬롯셋 반환
    }

    private ZonePuzzleSlotSet GetHintZoneSlotSet(Zone zone)
    {
        return zone == Zone.ZoneA ? bZoneSlots : aZoneSlots; // 힌트는 반대편 존 슬롯셋 반환
    }

    private Zone GetHintZone(Zone zone)
    {
        return zone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA; // 힌트가 실제 배치될 반대편 존 반환
    }

    private List<PuzzlePlacementSlot> GetPuzzleSlotsByStage(ZonePuzzleSlotSet zoneSlotSet, PuzzleStage stage)
    {
        if (zoneSlotSet == null)
            return null;

        return stage switch
        {
            PuzzleStage.Stage1 => zoneSlotSet.Stage1PuzzleSlots, // 1단계 퍼즐 슬롯 목록 반환
            PuzzleStage.Stage2 => zoneSlotSet.Stage2PuzzleSlots, // 2단계 퍼즐 슬롯 목록 반환
            PuzzleStage.Stage3 => zoneSlotSet.Stage3PuzzleSlots, // 3단계 퍼즐 슬롯 목록 반환
            _ => null
        };
    }

    private List<HintPlacementSlot> GetHintSlotsByStage(ZonePuzzleSlotSet zoneSlotSet, PuzzleStage stage)
    {
        if (zoneSlotSet == null)
            return null;

        return stage switch
        {
            PuzzleStage.Stage1 => zoneSlotSet.Stage1HintSlots, // 1단계 힌트 슬롯 목록 반환
            PuzzleStage.Stage2 => zoneSlotSet.Stage2HintSlots, // 2단계 힌트 슬롯 목록 반환
            PuzzleStage.Stage3 => zoneSlotSet.Stage3HintSlots, // 3단계 힌트 슬롯 목록 반환
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
                Runner.Despawn(obj); // 기존 스폰 오브젝트 제거
        }

        _spawnedObjects.Clear();      // 스폰 목록 초기화
        _zoneAStage1Puzzles.Clear();  // A존 진행도 목록 초기화
        _zoneBStage1Puzzles.Clear();  // B존 진행도 목록 초기화
        _zoneAStage2Screens.Clear();  // A존 스크린 목록 초기화
        _zoneBStage2Screens.Clear();  // B존 스크린 목록 초기화

        _hasSpawnedRound = false;     // 스폰 완료 플래그 초기화
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PuzzleSpawnManager] {message}", this); // 일반 디버그 로그
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PuzzleSpawnManager] {message}", this); // 경고 디버그 로그
    }
}