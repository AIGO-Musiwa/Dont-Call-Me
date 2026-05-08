using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fusion 기준 퍼즐/힌트 랜덤 배치 매니저.
/// 인게임 씬 진입 후 서버가 자동으로 퍼즐/힌트를 스폰한다.
/// 
/// 변경점
/// - 기존 Stage별 슬롯셋/슬롯 인덱스 사용 제거
/// - RoundGenerationResult가 제공하는 TargetSlot 기준으로 바로 스폰
/// - 배치 규칙은 RoundGenerator가 전부 담당
/// - 이 스크립트는 계획 소비 + 스폰 후 연결 작업만 담당
/// - 스폰 전에 카탈로그 자동 준비/검증 단계 추가
/// - 스폰된 퍼즐에 자기 Zone을 공통 주입
/// </summary>
public class PuzzleSpawnManager : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private PuzzleDefinitionDatabase puzzleDefinitionDatabase; // 단계별 퍼즐 정의 DB
    [SerializeField] private PuzzleProgressManager puzzleProgressManager;       // 진행도 등록 대상
    [SerializeField] private RoundSeedManager roundSeedManager;                 // 이번 판 시드 관리

    [Header("Zone 배치 카탈로그")]
    [SerializeField] private ZonePlacementCatalog aZoneCatalog; // ZoneA Room/Stage3 고정 위치 카탈로그
    [SerializeField] private ZonePlacementCatalog bZoneCatalog; // ZoneB Room/Stage3 고정 위치 카탈로그

    [Header("각 구역 퍼즐 개수")]
    [SerializeField] private int stage1SelectCount = 3; // Zone당 Stage1 퍼즐 선택 개수
    [SerializeField] private int stage2SelectCount = 3; // Zone당 Stage2 퍼즐 선택 개수

    [Header("자동 시작")]
    [SerializeField] private bool autoSpawnOnSpawned = true; // Spawned에서 자동 시작 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private readonly List<NetworkObject> _spawnedObjects = new(); // 이번 라운드에 스폰한 오브젝트 목록

    private readonly List<PuzzleInteractableBase> _zoneAStage1Puzzles = new(); // ZoneA Stage1 진행도 대상 퍼즐 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage1Puzzles = new(); // ZoneB Stage1 진행도 대상 퍼즐 목록

    private readonly List<PuzzleInteractableBase> _zoneAStage2Puzzles = new(); // ZoneA Stage2 퍼즐 본체 목록
    private readonly List<PuzzleInteractableBase> _zoneBStage2Puzzles = new(); // ZoneB Stage2 퍼즐 본체 목록

    private readonly List<GameObject> _zoneAStage2Screens = new(); // ZoneA Stage2 스크린 루트 목록
    private readonly List<GameObject> _zoneBStage2Screens = new(); // ZoneB Stage2 스크린 루트 목록

    private PuzzleInteractableBase _zoneAStage3Puzzle; // ZoneA Stage3 퍼즐 본체
    private PuzzleInteractableBase _zoneBStage3Puzzle; // ZoneB Stage3 퍼즐 본체

    private bool _hasSpawnedRound; // 이번 라운드 스폰 완료 여부

    public override void Spawned()
    {
        Log($"Spawned 호출 | IsServer={Runner.IsServer} | HasStateAuthority={HasStateAuthority}");

        if (!autoSpawnOnSpawned)
            return; // 자동 시작 꺼져 있으면 종료

        if (!Runner.IsServer)
        {
            Log("클라이언트 인스턴스이므로 자동 스폰하지 않음");
            return; // 서버만 실제 스폰 수행
        }

        TrySpawnRoundPuzzles(); // 자동 스폰 시작
    }

    /// <summary>
    /// 현재 라운드 퍼즐/힌트 스폰을 시작한다.
    /// </summary>
    [ContextMenu("Spawn Round Puzzles")]
    public void TrySpawnRoundPuzzles()
    {
        if (_hasSpawnedRound)
        {
            LogWarning("이미 이번 라운드 퍼즐 스폰이 완료되어 중복 실행을 막음");
            return;
        }

        if (!CanStartSpawn())
            return; // 스폰 가능 상태가 아니면 중단

        ClearSpawnedObjects(); // 이전 라운드 정보 정리

        roundSeedManager.EnsureRoundSeed(); // 이번 판 시드 보장

        int roundSeed = roundSeedManager.CurrentSeed; // 현재 라운드 시드
        if (roundSeed == 0)
        {
            LogWarning("유효한 라운드 시드가 없어 퍼즐 배치를 진행할 수 없습니다.");
            return;
        }

        // Room 기반 배치 계획 생성
        RoundGenerationResult result = RoundGenerator.GeneratePuzzlePlans(
            roundSeed,
            puzzleDefinitionDatabase,
            aZoneCatalog,
            bZoneCatalog,
            stage1SelectCount,
            stage2SelectCount);

        Log($"RoundGenerationResult 생성 완료 | PuzzlePlans={result.PuzzlePlans.Count}");

        // 계획대로 실제 퍼즐/힌트 스폰
        for (int i = 0; i < result.PuzzlePlans.Count; i++)
        {
            RoundGenerationResult.PuzzleSpawnPlan plan = result.PuzzlePlans[i]; // 퍼즐 1개 배치 계획
            if (plan == null || plan.Definition == null)
                continue;

            NetworkObject spawnedPuzzle = SpawnNetworkPrefabAtSlot(
                plan.Definition.PuzzlePrefab,
                plan.TargetSlot,
                plan.Definition.PuzzlePositionOffset,
                plan.Definition.PuzzleRotationOffset); // 퍼즐 본체 스폰

            if (spawnedPuzzle != null)
            {
                ApplySpawnZoneToSpawnedPuzzle(spawnedPuzzle, plan.Zone);          // 스폰된 Zone 공통 주입
                ApplyAnswerSeedToSpawnedObject(spawnedPuzzle, plan.AnswerSeed);  // 퍼즐 정답 시드 적용
                RegisterSpawnedPuzzle(spawnedPuzzle.gameObject, plan.Zone, plan.Stage); // 진행도/스크린/Stage3 등록

                string slotId = plan.TargetSlot != null ? plan.TargetSlot.SlotId : "null";
                string roomId = plan.TargetRoom != null ? plan.TargetRoom.RoomId : "FixedSlot";

                Log($"{plan.Zone} 퍼즐 배치 : {plan.Definition.PuzzleId} | Stage={plan.Stage} | Room={roomId} | Slot={slotId} | AnswerSeed={plan.AnswerSeed}");
            }

            // Stage3 힌트는 월드에 스폰하지 않음
            if (plan.Stage == PuzzleStage.Stage3)
                continue;

            // 연결된 힌트 여러 개를 각각 스폰
            for (int hintIndex = 0; hintIndex < plan.HintPlans.Count; hintIndex++)
            {
                RoundGenerationResult.HintSpawnPlan hintPlan = plan.HintPlans[hintIndex]; // 힌트 1개 계획
                if (hintPlan == null || hintPlan.HintPrefab == null)
                    continue;

                NetworkObject spawnedHint = SpawnNetworkPrefabAtSlot(
                    hintPlan.HintPrefab,
                    hintPlan.TargetSlot,
                    hintPlan.HintPositionOffset,
                    hintPlan.HintRotationOffset); // 힌트 프리팹 스폰

                if (spawnedHint != null)
                {
                    ApplyAnswerSeedToSpawnedObject(spawnedHint, plan.AnswerSeed); // 퍼즐과 같은 정답 시드 적용
                    TryLinkLightPatternPair(spawnedPuzzle, spawnedHint);          // 특수 퍼즐-힌트 연결 시도

                    string hintSlotId = hintPlan.TargetSlot != null ? hintPlan.TargetSlot.SlotId : "null";
                    string hintRoomId = hintPlan.TargetRoom != null ? hintPlan.TargetRoom.RoomId : "null";

                    Log($"{GetHintZone(plan.Zone)} 힌트 배치 : {plan.Definition.PuzzleId}/{hintPlan.HintId} | Room={hintRoomId} | Slot={hintSlotId} | AnswerSeed={plan.AnswerSeed}");
                }
            }
        }

        // 진행도 등록
        puzzleProgressManager.InitializeRound(
            _zoneAStage1Puzzles,
            _zoneBStage1Puzzles,
            _zoneAStage2Screens,
            _zoneBStage2Screens);

        puzzleProgressManager.RegisterStage2Puzzles(
            _zoneAStage2Puzzles,
            _zoneBStage2Puzzles);

        puzzleProgressManager.RegisterStage3Puzzles(
            _zoneAStage3Puzzle,
            _zoneBStage3Puzzle);

        // FinalCode 힌트를 Zone별 Stage2 퍼즐에 배정
        AssignStage3HintsToStage2Puzzles(result);

        _hasSpawnedRound = true;

        Log($"퍼즐 랜덤 배치 완료 | ZoneA Stage1={_zoneAStage1Puzzles.Count} | ZoneB Stage1={_zoneBStage1Puzzles.Count} | ZoneA Stage2={_zoneAStage2Puzzles.Count} | ZoneB Stage2={_zoneBStage2Puzzles.Count}");
    }

    /// <summary>
    /// 스폰 전 카탈로그 준비와 검증까지 통과했는지 확인한다.
    /// </summary>
    private bool CanStartSpawn()
    {
        if (Runner == null)
        {
            LogWarning("Runner가 없어 퍼즐 랜덤 배치를 시작할 수 없습니다.");
            return false;
        }

        if (!Runner.IsServer)
        {
            LogWarning("PuzzleSpawnManager는 서버에서만 실행해야 합니다.");
            return false;
        }

        if (puzzleDefinitionDatabase == null ||
            puzzleProgressManager == null ||
            roundSeedManager == null ||
            aZoneCatalog == null ||
            bZoneCatalog == null)
        {
            LogWarning("필수 참조가 비어 있어 랜덤 배치를 시작할 수 없습니다.");
            return false;
        }

        PrepareCatalogsBeforeSpawn(); // 스폰 전 카탈로그 자동 준비

        if (!ValidateCatalogsBeforeSpawn())
            return false; // 준비 후 검증 실패 시 중단

        return true;
    }

    /// <summary>
    /// 스폰 전에 각 Zone 카탈로그의 Room/Slot 목록을 준비한다.
    /// </summary>
    private void PrepareCatalogsBeforeSpawn()
    {
        aZoneCatalog.PrepareCatalog(); // ZoneA 방/슬롯 자동 수집 및 점유 상태 초기화
        bZoneCatalog.PrepareCatalog(); // ZoneB 방/슬롯 자동 수집 및 점유 상태 초기화

        Log("Zone 카탈로그 준비 완료");
    }

    /// <summary>
    /// 스폰 전에 각 Zone 카탈로그가 정상적인지 검사한다.
    /// </summary>
    private bool ValidateCatalogsBeforeSpawn()
    {
        bool aValid = aZoneCatalog.ValidateCatalog(); // ZoneA 유효성 검사
        bool bValid = bZoneCatalog.ValidateCatalog(); // ZoneB 유효성 검사

        if (!aValid || !bValid)
        {
            LogWarning($"Zone 카탈로그 검증 실패 | ZoneA={aValid} | ZoneB={bValid}");
            return false;
        }

        Log("Zone 카탈로그 검증 통과");
        return true;
    }

    /// <summary>
    /// PlacementSlotMeta.anchor 위치를 기준으로 프리팹을 스폰한다.
    /// </summary>
    private NetworkObject SpawnNetworkPrefabAtSlot(
        NetworkObject prefab,
        PlacementSlotMeta slot,
        Vector3 localPositionOffset,
        Vector3 localRotationOffset)
    {
        if (prefab == null || slot == null)
            return null;

        Transform anchor = slot.Anchor != null ? slot.Anchor : slot.transform; // 슬롯 anchor 우선 사용
        if (anchor == null)
            return null;

        Vector3 worldPosition = anchor.TransformPoint(localPositionOffset); // 슬롯 기준 월드 위치 계산
        Quaternion worldRotation = anchor.rotation * Quaternion.Euler(localRotationOffset); // 슬롯 기준 월드 회전 계산

        NetworkObject spawned = Runner.Spawn(prefab, worldPosition, worldRotation, null); // 네트워크 스폰
        if (spawned != null)
            _spawnedObjects.Add(spawned); // 추후 despawn 대상 목록 등록

        return spawned;
    }

    /// <summary>
    /// 스폰된 퍼즐/힌트에 정답 시드를 적용한다.
    /// </summary>
    private void ApplyAnswerSeedToSpawnedObject(NetworkObject spawnedObject, int answerSeed)
    {
        if (spawnedObject == null)
            return;

        PuzzleSeedSync seedSync = spawnedObject.GetComponent<PuzzleSeedSync>(); // 시드 동기화 컴포넌트 찾기
        if (seedSync == null)
        {
            LogWarning($"PuzzleSeedSync 누락 : {spawnedObject.name}");
            return;
        }

        seedSync.ServerSetAnswerSeed(answerSeed); // 서버 권한으로 정답 시드 적용
    }

    /// <summary>
    /// 스폰된 퍼즐에 자기 Zone 값을 공통 주입한다.
    /// PuzzleSpawnEntry.ProgressTarget을 우선 사용하고,
    /// 없으면 자식 포함 PuzzleInteractableBase를 탐색한다.
    /// </summary>
    private void ApplySpawnZoneToSpawnedPuzzle(NetworkObject spawnedPuzzle, Zone zone)
    {
        if (spawnedPuzzle == null)
            return;

        PuzzleInteractableBase targetPuzzle = null;

        PuzzleSpawnEntry entry = spawnedPuzzle.GetComponent<PuzzleSpawnEntry>();
        if (entry != null && entry.ProgressTarget != null)
            targetPuzzle = entry.ProgressTarget;

        if (targetPuzzle == null)
            targetPuzzle = spawnedPuzzle.GetComponentInChildren<PuzzleInteractableBase>();

        if (targetPuzzle == null)
        {
            LogWarning($"SpawnZone 주입 실패 : PuzzleInteractableBase를 찾을 수 없음 | object={spawnedPuzzle.name}");
            return;
        }

        targetPuzzle.SetSpawnZone(zone);

        Log($"SpawnZone 주입 완료 | puzzle={targetPuzzle.name} | zone={zone}");
    }

    /// <summary>
    /// 스폰된 퍼즐을 Stage/Zone 기준으로 진행도 등록용 목록에 분류한다.
    /// </summary>
    private void RegisterSpawnedPuzzle(GameObject spawnedPuzzle, Zone zone, PuzzleStage stage)
    {
        if (spawnedPuzzle == null)
            return;

        PuzzleSpawnEntry entry = spawnedPuzzle.GetComponent<PuzzleSpawnEntry>(); // 진행도/스크린 등록 정보
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

        if (stage == PuzzleStage.Stage2)
        {
            if (entry.ProgressTarget != null)
            {
                if (zone == Zone.ZoneA)
                    _zoneAStage2Puzzles.Add(entry.ProgressTarget);
                else
                    _zoneBStage2Puzzles.Add(entry.ProgressTarget);
            }

            if (entry.Stage2ScreenRoot != null)
            {
                if (zone == Zone.ZoneA)
                    _zoneAStage2Screens.Add(entry.Stage2ScreenRoot);
                else
                    _zoneBStage2Screens.Add(entry.Stage2ScreenRoot);
            }

            return;
        }

        if (stage == PuzzleStage.Stage3 && entry.ProgressTarget != null)
        {
            if (zone == Zone.ZoneA)
                _zoneAStage3Puzzle = entry.ProgressTarget;
            else
                _zoneBStage3Puzzle = entry.ProgressTarget;
        }
    }

    /// <summary>
    /// 특수 퍼즐(예: LightPattern)과 힌트를 직접 연결해야 할 때 사용한다.
    /// </summary>
    private void TryLinkLightPatternPair(NetworkObject spawnedPuzzle, NetworkObject spawnedHint)
    {
        if (spawnedPuzzle == null || spawnedHint == null)
            return;

        LightPatternPuzzle puzzle = spawnedPuzzle.GetComponent<LightPatternPuzzle>(); // 퍼즐 본체 찾기
        LightPatternHint hint = spawnedHint.GetComponent<LightPatternHint>();         // 힌트 본체 찾기

        if (puzzle == null || hint == null)
            return;

        hint.SetObservedPuzzle(puzzle); // 힌트가 관찰할 퍼즐 연결

        Log($"LightPattern 퍼즐-힌트 연결 완료 | Puzzle={spawnedPuzzle.name} | Hint={spawnedHint.name}");
    }

    /// <summary>
    /// 라운드 전체 FinalCode 데이터에서 Zone별 Stage3 힌트 3개를 Zone별 Stage2 퍼즐 3개에 배정한다.
    /// </summary>
    private void AssignStage3HintsToStage2Puzzles(RoundGenerationResult result)
    {
        if (result == null)
            return;

        if (result.FinalCodeData == null)
        {
            LogWarning("FinalCodeData가 없어 Stage3 힌트를 배정할 수 없습니다.");
            return;
        }

        Log(
            $"FinalCode 생성 완료 | Code={result.FinalCodeData.GetFinalCodeString()} | " +
            $"TrueA={result.FinalCodeData.TrueAHintIndex} | " +
            $"TrueB={result.FinalCodeData.TrueBHintIndex}");

        AssignZoneStage3Hints(
            _zoneAStage2Puzzles,
            result.FinalCodeData.ZoneAHints,
            Zone.ZoneA,
            result.FinalCodeData.TrueAHintIndex);

        AssignZoneStage3Hints(
            _zoneBStage2Puzzles,
            result.FinalCodeData.ZoneBHints,
            Zone.ZoneB,
            result.FinalCodeData.TrueBHintIndex);
    }

    /// <summary>
    /// 특정 Zone의 Stage2 퍼즐들에 FinalCode 힌트를 순서대로 배정한다.
    /// </summary>
    private void AssignZoneStage3Hints(
        List<PuzzleInteractableBase> stage2Puzzles,
        List<FinalCodeHintData> hints,
        Zone zone,
        int trueHintIndex)
    {
        if (stage2Puzzles == null || hints == null)
            return;

        int count = Mathf.Min(stage2Puzzles.Count, hints.Count); // 실제 배정 가능한 수

        if (stage2Puzzles.Count != hints.Count)
        {
            LogWarning(
                $"{zone} Stage3 힌트 개수와 Stage2 퍼즐 개수가 다릅니다. " +
                $"Puzzles={stage2Puzzles.Count} | Hints={hints.Count} | Assigned={count}");
        }

        for (int i = 0; i < count; i++)
        {
            PuzzleInteractableBase puzzle = stage2Puzzles[i]; // 대상 퍼즐
            FinalCodeHintData hint = hints[i];                // 배정할 힌트

            if (puzzle == null || hint == null)
                continue;

            string trueFlag = i == trueHintIndex ? "TRUE" : "FAKE";

            if (puzzle is MazePuzzle mazePuzzle)
            {
                mazePuzzle.SetStage3HintData(hint);
                Log($"{zone} Stage3 힌트 배정 | Index={i} | {trueFlag} | MazePuzzle -> {hint.GetDebugString()}");
                continue;
            }

            if (puzzle is NumericCodePuzzle numericCodePuzzle)
            {
                numericCodePuzzle.SetStage3HintData(hint);
                Log($"{zone} Stage3 힌트 배정 | Index={i} | {trueFlag} | NumericCodePuzzle -> {hint.GetDebugString()}");
                continue;
            }

            if (puzzle is ReagentCraftPuzzle reagentCraftPuzzle)
            {
                reagentCraftPuzzle.SetStage3HintData(hint);
                Log($"{zone} Stage3 힌트 배정 | Index={i} | {trueFlag} | ReagentCraftPuzzle -> {hint.GetDebugString()}");
                continue;
            }

            LogWarning($"{zone} Stage2 퍼즐 힌트 배정 실패 | 지원하지 않는 퍼즐 타입 : {puzzle.name}");
        }
    }

    /// <summary>
    /// 퍼즐 원본 Zone 기준으로 힌트가 실제 배치되는 반대 Zone을 반환한다.
    /// </summary>
    private Zone GetHintZone(Zone puzzleZone)
    {
        return puzzleZone == Zone.ZoneA ? Zone.ZoneB : Zone.ZoneA;
    }

    /// <summary>
    /// 이전 라운드 스폰 정보와 캐시를 전부 초기화한다.
    /// </summary>
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

        _spawnedObjects.Clear();     // 스폰 목록 초기화
        _zoneAStage1Puzzles.Clear(); // ZoneA Stage1 진행도 목록 초기화
        _zoneBStage1Puzzles.Clear(); // ZoneB Stage1 진행도 목록 초기화
        _zoneAStage2Puzzles.Clear(); // ZoneA Stage2 퍼즐 목록 초기화
        _zoneBStage2Puzzles.Clear(); // ZoneB Stage2 퍼즐 목록 초기화
        _zoneAStage2Screens.Clear(); // ZoneA Stage2 화면 목록 초기화
        _zoneBStage2Screens.Clear(); // ZoneB Stage2 화면 목록 초기화
        _zoneAStage3Puzzle = null;   // ZoneA Stage3 퍼즐 초기화
        _zoneBStage3Puzzle = null;   // ZoneB Stage3 퍼즐 초기화

        // Room 런타임 점유 상태도 함께 초기화
        aZoneCatalog?.ClearRuntimeOccupancy();
        bZoneCatalog?.ClearRuntimeOccupancy();

        _hasSpawnedRound = false;    // 스폰 완료 플래그 초기화
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
    /// 경고 디버그 로그 출력.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PuzzleSpawnManager] {message}", this);
    }
}