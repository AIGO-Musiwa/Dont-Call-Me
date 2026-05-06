using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 기준 시드를 받아 한 판의 퍼즐/힌트/플레이어 배치 결과를 계산하는 정적 유틸.
/// 
/// 핵심 변경점
/// - 기존 슬롯 인덱스 셔플 방식 제거
/// - Room 기반 배치 방식으로 전환
/// - Stage1 : 1,2층 선호 + 몰림 페널티 + 3층 약한 보정
/// - Stage2 : 층 동일 선호 + 몰림 페널티
/// - 퍼즐 먼저 배치하고, 남은 방에 힌트를 배치
/// - 힌트는 1차로 방 중복 없이 배치하고, 실패 시 2차로 방 중복 허용 배치
/// - Stage3 퍼즐은 Zone별 Stage3Puzzle 슬롯 사용
/// - Stage3 힌트는 월드에 스폰하지 않음
/// </summary>
public static class RoundGenerator
{
    private const int PuzzleSeedSalt = 1001;    // 퍼즐 배치용 파생 시드 salt
    private const int PlayerSeedSalt = 2001;    // 플레이어 배정용 파생 시드 salt
    private const int AnswerSeedSalt = 3001;    // 퍼즐 정답 생성용 파생 시드 salt
    private const int FinalCodeSeedSalt = 9001; // FinalCode 전용 salt

    /// <summary>
    /// Zone별 Room 배치 컨텍스트.
    /// 배치 중 점유 상태와 층 분포 상태를 함께 관리한다.
    /// </summary>
    private sealed class ZonePlacementContext
    {
        public ZonePlacementCatalog Catalog;                      // 이 Zone의 Room 카탈로그
        public List<RoomPlacementGroup> Rooms = new();            // 배치 대상 Room 목록
        public FloorDistributionState Stage1Distribution = new(); // Stage1 층 분포 상태
        public FloorDistributionState Stage2Distribution = new(); // Stage2 층 분포 상태
    }

    /// <summary>
    /// 층별 현재 배치 수를 관리하는 내부 상태 클래스.
    /// </summary>
    private sealed class FloorDistributionState
    {
        private int _floor1Count; // 현재 1층 배치 수
        private int _floor2Count; // 현재 2층 배치 수
        private int _floor3Count; // 현재 3층 배치 수

        public int TotalPlaced => _floor1Count + _floor2Count + _floor3Count; // 총 배치 수

        /// <summary>
        /// 특정 층의 현재 배치 수 반환.
        /// </summary>
        public int GetCount(int floor)
        {
            return floor switch
            {
                1 => _floor1Count,
                2 => _floor2Count,
                3 => _floor3Count,
                _ => 0
            };
        }

        /// <summary>
        /// 특정 층 배치 수 1 증가.
        /// </summary>
        public void AddPlaced(int floor)
        {
            switch (floor)
            {
                case 1:
                    _floor1Count++;
                    break;
                case 2:
                    _floor2Count++;
                    break;
                case 3:
                    _floor3Count++;
                    break;
            }
        }

        /// <summary>
        /// 현재 가장 적게 배치된 층의 수를 반환.
        /// </summary>
        public int GetMinCount()
        {
            return Mathf.Min(_floor1Count, Mathf.Min(_floor2Count, _floor3Count));
        }

        /// <summary>
        /// 현재 가장 많이 배치된 층의 수를 반환.
        /// </summary>
        public int GetMaxCount()
        {
            return Mathf.Max(_floor1Count, Mathf.Max(_floor2Count, _floor3Count));
        }
    }

    /// <summary>
    /// 퍼즐/힌트 배치 결과 생성.
    /// </summary>
    public static RoundGenerationResult GeneratePuzzlePlans(
        int roundSeed,
        PuzzleDefinitionDatabase database,
        ZonePlacementCatalog zoneA,
        ZonePlacementCatalog zoneB,
        int stage1SelectCount,
        int stage2SelectCount)
    {
        RoundGenerationResult result = new RoundGenerationResult(); // 이번 라운드 결과 컨테이너 생성
        result.RoundSeed = roundSeed;                               // 원본 라운드 시드 기록

        SeedRandom rng = new SeedRandom(roundSeed + PuzzleSeedSalt); // 퍼즐 배치 전용 RNG 생성

        // 안전하게 이전 런타임 점유 상태 초기화
        zoneA?.ClearRuntimeOccupancy();
        zoneB?.ClearRuntimeOccupancy();

        // Zone별 배치 컨텍스트 생성
        ZonePlacementContext zoneAContext = BuildZonePlacementContext(zoneA);
        ZonePlacementContext zoneBContext = BuildZonePlacementContext(zoneB);

        // Stage별 후보 정의 목록 생성
        List<PuzzleDefinition> stage1Pool = GetStableDefinitions(database, PuzzleStage.Stage1); // Stage1 후보 풀
        List<PuzzleDefinition> stage2Pool = GetStableDefinitions(database, PuzzleStage.Stage2); // Stage2 후보 풀
        List<PuzzleDefinition> stage3Pool = GetStableDefinitions(database, PuzzleStage.Stage3); // Stage3 후보 풀

        // Zone별 퍼즐 종류 선택
        List<PuzzleDefinition> zoneAStage1Defs = rng.PickUnique(stage1Pool, stage1SelectCount); // ZoneA Stage1 퍼즐 종류 선택
        List<PuzzleDefinition> zoneBStage1Defs = rng.PickUnique(stage1Pool, stage1SelectCount); // ZoneB Stage1 퍼즐 종류 선택
        List<PuzzleDefinition> zoneAStage2Defs = rng.PickUnique(stage2Pool, stage2SelectCount); // ZoneA Stage2 퍼즐 종류 선택
        List<PuzzleDefinition> zoneBStage2Defs = rng.PickUnique(stage2Pool, stage2SelectCount); // ZoneB Stage2 퍼즐 종류 선택

        // 퍼즐 먼저 배치
        PlaceStage1Puzzles(result, rng, zoneAContext, zoneAStage1Defs); // ZoneA Stage1 퍼즐 배치
        PlaceStage1Puzzles(result, rng, zoneBContext, zoneBStage1Defs); // ZoneB Stage1 퍼즐 배치
        PlaceStage2Puzzles(result, rng, zoneAContext, zoneAStage2Defs); // ZoneA Stage2 퍼즐 배치
        PlaceStage2Puzzles(result, rng, zoneBContext, zoneBStage2Defs); // ZoneB Stage2 퍼즐 배치

        // 힌트는 퍼즐 배치가 끝난 후, 반대 Zone에 배치
        // 1차는 방 중복 금지, 2차는 방 중복 허용
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneA, PuzzleStage.Stage1, zoneBContext); // ZoneA Stage1 퍼즐의 힌트는 ZoneB에 배치
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneB, PuzzleStage.Stage1, zoneAContext); // ZoneB Stage1 퍼즐의 힌트는 ZoneA에 배치
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneA, PuzzleStage.Stage2, zoneBContext); // ZoneA Stage2 퍼즐의 힌트는 ZoneB에 배치
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneB, PuzzleStage.Stage2, zoneAContext); // ZoneB Stage2 퍼즐의 힌트는 ZoneA에 배치

        // Stage3 퍼즐은 Zone별 Stage3Puzzle 슬롯에 plan 추가
        AddStage3PuzzlePlan(result, zoneAContext, stage3Pool); // ZoneA Stage3 퍼즐 추가
        AddStage3PuzzlePlan(result, zoneBContext, stage3Pool); // ZoneB Stage3 퍼즐 추가

        // FinalCode 데이터 생성
        result.ZoneAFinalCodeData = FinalCodeAnswerGenerator.Generate(BuildFinalCodeSeed(roundSeed, Zone.ZoneA)); // ZoneA FinalCode 데이터 생성
        result.ZoneBFinalCodeData = FinalCodeAnswerGenerator.Generate(BuildFinalCodeSeed(roundSeed, Zone.ZoneB)); // ZoneB FinalCode 데이터 생성

        return result; // 완성된 배치 결과 반환
    }

    /// <summary>
    /// 플레이어 SlotIndex 기준 Zone/Role 배정 결과 생성.
    /// 기존 로직 유지.
    /// </summary>
    public static RoundGenerationResult GeneratePlayerAssignments(int roundSeed, IReadOnlyList<int> orderedSlotIndices)
    {
        RoundGenerationResult result = new RoundGenerationResult(); // 플레이어 배정 결과 컨테이너 생성
        result.RoundSeed = roundSeed;                               // 원본 라운드 시드 기록

        SeedRandom rng = new SeedRandom(roundSeed + PlayerSeedSalt); // 플레이어 배정용 RNG 생성

        List<int> copiedSlots = new List<int>(orderedSlotIndices); // 원본 슬롯 목록 복사
        copiedSlots.Sort();                                        // 슬롯 순서 고정
        rng.Shuffle(copiedSlots);                                  // 시드 기준 셔플

        for (int i = 0; i < copiedSlots.Count; i++)
        {
            int slotIndex = copiedSlots[i]; // 현재 슬롯 인덱스

            Zone zone = i < 2 ? Zone.ZoneA : Zone.ZoneB; // 앞 2명은 ZoneA, 뒤 2명은 ZoneB
            int slotInZone = i < 2 ? i : i - 2;          // Zone 내부 순번
            PlayerRole role = slotInZone == 0
                ? PlayerRole.WalkieTalkie
                : PlayerRole.Flashlight;                 // 각 Zone 첫 번째는 무전기, 두 번째는 손전등

            result.AddPlayerAssignment(new RoundGenerationResult.PlayerAssignmentPlan
            {
                SlotIndex = slotIndex,
                Zone = zone,
                Role = role
            });
        }

        return result;
    }

    /// <summary>
    /// Zone 카탈로그를 기반으로 배치 컨텍스트를 생성한다.
    /// </summary>
    private static ZonePlacementContext BuildZonePlacementContext(ZonePlacementCatalog catalog)
    {
        ZonePlacementContext context = new ZonePlacementContext
        {
            Catalog = catalog
        };

        if (catalog != null)
        {
            List<RoomPlacementGroup> rooms = catalog.GetAllRooms();
            if (rooms != null)
                context.Rooms.AddRange(rooms);
        }

        return context;
    }

    /// <summary>
    /// 단계별 퍼즐 정의 목록을 안정적인 순서로 반환한다.
    /// 같은 시드에서 inspector 순서 영향 최소화.
    /// </summary>
    private static List<PuzzleDefinition> GetStableDefinitions(PuzzleDefinitionDatabase database, PuzzleStage stage)
    {
        if (database == null)
            return new List<PuzzleDefinition>();

        return database
            .GetDefinitionsByStage(stage)
            .Where(d => d != null)
            .OrderBy(d => d.PuzzleId)
            .ToList();
    }

    /// <summary>
    /// Stage1 퍼즐들을 Room 기반으로 배치한다.
    /// </summary>
    private static void PlaceStage1Puzzles(
        RoundGenerationResult result,
        SeedRandom rng,
        ZonePlacementContext context,
        List<PuzzleDefinition> definitions)
    {
        if (context == null || definitions == null || context.Catalog == null)
            return;

        int localIndex = 0; // Zone 내부 Stage1 퍼즐 로컬 순번

        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleDefinition definition = definitions[i]; // 현재 배치할 Stage1 퍼즐 정의
            if (definition == null)
                continue;

            RoomPlacementGroup room = PickBestRoomForStage1(context, rng, true); // Stage1 퍼즐에 맞는 Room 선택
            if (room == null)
                continue;

            PlacementSlotMeta slot = room.GetRandomAvailablePuzzleSlot(rng); // 해당 Room에서 퍼즐 슬롯 선택
            if (slot == null)
                continue;

            room.MarkRoomOccupied(slot);                      // 방 + 슬롯 점유 확정
            context.Stage1Distribution.AddPlaced(room.Floor); // Stage1 층 분포 갱신

            int answerSeed = BuildAnswerSeed(result.RoundSeed, context.Catalog.Zone, PuzzleStage.Stage1, localIndex); // 퍼즐 정답 시드 생성
            localIndex++;

            result.AddPuzzlePlan(new RoundGenerationResult.PuzzleSpawnPlan
            {
                Definition = definition,
                Zone = context.Catalog.Zone,
                Stage = PuzzleStage.Stage1,
                TargetRoom = room,
                TargetSlot = slot,
                AnswerSeed = answerSeed
            });
        }
    }

    /// <summary>
    /// Stage2 퍼즐들을 Room 기반으로 배치한다.
    /// </summary>
    private static void PlaceStage2Puzzles(
        RoundGenerationResult result,
        SeedRandom rng,
        ZonePlacementContext context,
        List<PuzzleDefinition> definitions)
    {
        if (context == null || definitions == null || context.Catalog == null)
            return;

        int localIndex = 0; // Zone 내부 Stage2 퍼즐 로컬 순번

        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleDefinition definition = definitions[i]; // 현재 배치할 Stage2 퍼즐 정의
            if (definition == null)
                continue;

            RoomPlacementGroup room = PickBestRoomForStage2(context, rng, true); // Stage2 퍼즐에 맞는 Room 선택
            if (room == null)
                continue;

            PlacementSlotMeta slot = room.GetRandomAvailablePuzzleSlot(rng); // 해당 Room에서 퍼즐 슬롯 선택
            if (slot == null)
                continue;

            room.MarkRoomOccupied(slot);                      // 방 + 슬롯 점유 확정
            context.Stage2Distribution.AddPlaced(room.Floor); // Stage2 층 분포 갱신

            int answerSeed = BuildAnswerSeed(result.RoundSeed, context.Catalog.Zone, PuzzleStage.Stage2, localIndex); // 퍼즐 정답 시드 생성
            localIndex++;

            result.AddPuzzlePlan(new RoundGenerationResult.PuzzleSpawnPlan
            {
                Definition = definition,
                Zone = context.Catalog.Zone,
                Stage = PuzzleStage.Stage2,
                TargetRoom = room,
                TargetSlot = slot,
                AnswerSeed = answerSeed
            });
        }
    }

    /// <summary>
    /// 이미 배치된 퍼즐들의 힌트를 반대 Zone에 배치한다.
    /// 
    /// 1차:
    /// - 방 중복 금지
    /// - 기존 방당 1개 우선 규칙 유지
    /// 
    /// 2차:
    /// - 1차 실패 시 방 중복 허용
    /// - 단, 슬롯 중복은 계속 금지
    /// </summary>
    private static void PlaceHintsForPlacedPuzzles(
        RoundGenerationResult result,
        SeedRandom rng,
        Zone sourcePuzzleZone,
        PuzzleStage stage,
        ZonePlacementContext targetHintContext)
    {
        if (result == null || rng == null || targetHintContext == null)
            return;

        List<RoundGenerationResult.PuzzleSpawnPlan> plans = result.GetPlansByZone(sourcePuzzleZone); // source Zone 퍼즐 계획 목록

        for (int i = 0; i < plans.Count; i++)
        {
            RoundGenerationResult.PuzzleSpawnPlan puzzlePlan = plans[i];
            if (puzzlePlan == null)
                continue;

            if (puzzlePlan.Stage != stage)
                continue;

            PuzzleDefinition definition = puzzlePlan.Definition; // 현재 퍼즐 정의
            if (definition == null || definition.HintDefinitions == null)
                continue;

            for (int hintIndex = 0; hintIndex < definition.HintDefinitions.Count; hintIndex++)
            {
                PuzzleDefinition.HintDefinition hintDefinition = definition.HintDefinitions[hintIndex]; // 현재 힌트 정의
                if (hintDefinition == null || hintDefinition.HintPrefab == null)
                    continue;

                bool placed = TryPlaceHintPlan(
                    puzzlePlan,
                    hintDefinition,
                    targetHintContext,
                    rng,
                    stage,
                    false); // 1차: 방 중복 금지

                if (placed)
                    continue;

                placed = TryPlaceHintPlan(
                    puzzlePlan,
                    hintDefinition,
                    targetHintContext,
                    rng,
                    stage,
                    true); // 2차: 방 중복 허용

                if (!placed)
                {
                    Debug.LogWarning(
                        $"[RoundGenerator] 힌트 배치 실패 | " +
                        $"SourceZone={sourcePuzzleZone} | " +
                        $"TargetZone={(targetHintContext.Catalog != null ? targetHintContext.Catalog.Zone.ToString() : "None")} | " +
                        $"Stage={stage} | " +
                        $"PuzzleId={definition.PuzzleId} | " +
                        $"HintId={hintDefinition.HintId}");
                }
            }
        }
    }

    /// <summary>
    /// 힌트 1개를 실제로 배치하고 HintSpawnPlan을 추가한다.
    /// 
    /// ignoreRoomOccupancy가 false면 기존 규칙처럼 빈 방만 사용한다.
    /// ignoreRoomOccupancy가 true면 이미 사용된 방도 허용하되, 슬롯은 비어 있어야 한다.
    /// </summary>
    private static bool TryPlaceHintPlan(
        RoundGenerationResult.PuzzleSpawnPlan puzzlePlan,
        PuzzleDefinition.HintDefinition hintDefinition,
        ZonePlacementContext targetHintContext,
        SeedRandom rng,
        PuzzleStage stage,
        bool ignoreRoomOccupancy)
    {
        if (puzzlePlan == null || hintDefinition == null || targetHintContext == null || rng == null)
            return false;

        RoomPlacementGroup hintRoom = PickBestHintRoom(
            targetHintContext,
            rng,
            stage,
            ignoreRoomOccupancy); // 힌트를 놓을 Room 선택

        if (hintRoom == null)
            return false;

        PlacementSlotMeta hintSlot = hintRoom.GetRandomAvailableHintSlotPreferHintOnly(
            rng,
            ignoreRoomOccupancy); // 힌트 슬롯 선택

        if (hintSlot == null)
            return false;

        hintRoom.MarkRoomOccupied(hintSlot); // 방 + 슬롯 점유 확정

        // 힌트도 해당 Stage의 층 분포에 포함시켜서 몰림을 줄임
        if (stage == PuzzleStage.Stage1)
            targetHintContext.Stage1Distribution.AddPlaced(hintRoom.Floor);
        else if (stage == PuzzleStage.Stage2)
            targetHintContext.Stage2Distribution.AddPlaced(hintRoom.Floor);

        puzzlePlan.HintPlans.Add(new RoundGenerationResult.HintSpawnPlan
        {
            HintId = hintDefinition.HintId,
            HintPrefab = hintDefinition.HintPrefab,
            HintDefinition = hintDefinition,
            TargetRoom = hintRoom,
            TargetSlot = hintSlot,
            HintPositionOffset = hintDefinition.HintPositionOffset,
            HintRotationOffset = hintDefinition.HintRotationOffset
        });

        return true;
    }

    /// <summary>
    /// Stage1 규칙으로 가장 적절한 Room을 선택한다.
    /// 퍼즐/힌트 공용으로 사용 가능하게 usePuzzleSlots 플래그로 분기한다.
    /// </summary>
    private static RoomPlacementGroup PickBestRoomForStage1(
        ZonePlacementContext context,
        SeedRandom rng,
        bool usePuzzleSlots,
        bool ignoreRoomOccupancy = false)
    {
        if (context == null || rng == null)
            return null;

        List<RoomPlacementGroup> candidates = new List<RoomPlacementGroup>(); // 배치 가능한 Room 후보 목록

        for (int i = 0; i < context.Rooms.Count; i++)
        {
            RoomPlacementGroup room = context.Rooms[i];
            if (room == null)
                continue;

            if (!ignoreRoomOccupancy && room.IsOccupied())
                continue;

            if (usePuzzleSlots)
            {
                if (!room.HasAvailablePuzzleSlot(ignoreRoomOccupancy))
                    continue;
            }
            else
            {
                if (!room.HasAvailableHintSlot(ignoreRoomOccupancy))
                    continue;
            }

            candidates.Add(room);
        }

        return PickWeightedRoomByFloorScore(candidates, context.Stage1Distribution, true, rng); // Stage1 층 점수로 Room 선택
    }

    /// <summary>
    /// Stage2 규칙으로 가장 적절한 Room을 선택한다.
    /// </summary>
    private static RoomPlacementGroup PickBestRoomForStage2(
        ZonePlacementContext context,
        SeedRandom rng,
        bool usePuzzleSlots,
        bool ignoreRoomOccupancy = false)
    {
        if (context == null || rng == null)
            return null;

        List<RoomPlacementGroup> candidates = new List<RoomPlacementGroup>(); // 배치 가능한 Room 후보 목록

        for (int i = 0; i < context.Rooms.Count; i++)
        {
            RoomPlacementGroup room = context.Rooms[i];
            if (room == null)
                continue;

            if (!ignoreRoomOccupancy && room.IsOccupied())
                continue;

            if (usePuzzleSlots)
            {
                if (!room.HasAvailablePuzzleSlot(ignoreRoomOccupancy))
                    continue;
            }
            else
            {
                if (!room.HasAvailableHintSlot(ignoreRoomOccupancy))
                    continue;
            }

            candidates.Add(room);
        }

        return PickWeightedRoomByFloorScore(candidates, context.Stage2Distribution, false, rng); // Stage2 층 점수로 Room 선택
    }

    /// <summary>
    /// Stage 힌트를 위한 Room을 선택한다.
    /// 내부적으로 Stage1 / Stage2 규칙을 재사용한다.
    /// </summary>
    private static RoomPlacementGroup PickBestHintRoom(
        ZonePlacementContext context,
        SeedRandom rng,
        PuzzleStage stage,
        bool ignoreRoomOccupancy)
    {
        if (stage == PuzzleStage.Stage1)
            return PickBestRoomForStage1(context, rng, false, ignoreRoomOccupancy);

        if (stage == PuzzleStage.Stage2)
            return PickBestRoomForStage2(context, rng, false, ignoreRoomOccupancy);

        return null;
    }

    /// <summary>
    /// 후보 Room들 중 층 점수 기반 weighted random으로 1개 선택한다.
    /// </summary>
    private static RoomPlacementGroup PickWeightedRoomByFloorScore(
        List<RoomPlacementGroup> candidates,
        FloorDistributionState distribution,
        bool isStage1,
        SeedRandom rng)
    {
        if (candidates == null || candidates.Count == 0 || rng == null || distribution == null)
            return null;

        float totalWeight = 0f;                  // 전체 누적 가중치
        List<float> weights = new List<float>(); // 후보별 가중치 목록

        for (int i = 0; i < candidates.Count; i++)
        {
            RoomPlacementGroup room = candidates[i];
            if (room == null)
            {
                weights.Add(0f);
                continue;
            }

            float score = isStage1
                ? EvaluateStage1FloorScore(room.Floor, distribution) // Stage1 층 점수 계산
                : EvaluateStage2FloorScore(room.Floor, distribution); // Stage2 층 점수 계산

            score = Mathf.Max(0.01f, score); // 0 이하 방지
            weights.Add(score);
            totalWeight += score;
        }

        if (totalWeight <= 0f)
        {
            int randomIndex = rng.NextInt(0, candidates.Count); // 방어용 균등 랜덤
            return candidates[randomIndex];
        }

        float pick = rng.NextFloat() * totalWeight; // 0~totalWeight 사이 랜덤값
        float accum = 0f;                           // 누적 가중치

        for (int i = 0; i < candidates.Count; i++)
        {
            accum += weights[i];
            if (pick <= accum)
                return candidates[i];
        }

        return candidates[candidates.Count - 1]; // 부동소수 오차 방어용 fallback
    }

    /// <summary>
    /// Stage1 층 점수 계산.
    /// </summary>
    private static float EvaluateStage1FloorScore(int floor, FloorDistributionState distribution)
    {
        float baseScore = floor switch
        {
            1 => 1.0f,
            2 => 1.0f,
            3 => 0.55f,
            _ => 0.1f
        };

        int current = distribution.GetCount(floor); // 현재 이 층 배치 수
        int min = distribution.GetMinCount();       // 가장 적게 배치된 층의 개수

        float crowdPenalty = Mathf.Max(0, current - min) * 0.45f; // 많이 몰렸을수록 감점
        float emptyThirdFloorBonus = 0f;                          // 3층 비어 있을 때 보정값

        if (floor == 3 && distribution.TotalPlaced >= 2 && distribution.GetCount(3) == 0)
            emptyThirdFloorBonus = 0.35f; // 3층이 너무 오래 비면 약하게 띄워줌

        return Mathf.Max(0.05f, baseScore - crowdPenalty + emptyThirdFloorBonus);
    }

    /// <summary>
    /// Stage2 층 점수 계산.
    /// </summary>
    private static float EvaluateStage2FloorScore(int floor, FloorDistributionState distribution)
    {
        float baseScore = 1.0f; // Stage2는 모든 층 기본 선호 동일

        int current = distribution.GetCount(floor); // 현재 이 층 배치 수
        int min = distribution.GetMinCount();       // 가장 적게 배치된 층의 개수

        float crowdPenalty = Mathf.Max(0, current - min) * 0.50f; // 몰린 층 감점

        return Mathf.Max(0.05f, baseScore - crowdPenalty);
    }

    /// <summary>
    /// Zone의 Stage3Puzzle 슬롯에 Stage3 퍼즐 스폰 계획을 추가한다.
    /// Stage3 힌트는 월드 배치하지 않으므로 HintPlans는 비운다.
    /// </summary>
    private static void AddStage3PuzzlePlan(
        RoundGenerationResult result,
        ZonePlacementContext context,
        List<PuzzleDefinition> stage3Definitions)
    {
        if (result == null || context == null || stage3Definitions == null || stage3Definitions.Count == 0)
            return;

        if (context.Catalog == null)
            return;

        PlacementSlotMeta stage3Slot = context.Catalog.GetStage3PuzzleSlot(); // Zone별 Stage3 퍼즐 슬롯
        if (stage3Slot == null)
            return;

        if (!stage3Slot.CanPlaceStage3Puzzle())
        {
            Debug.LogWarning(
                $"[RoundGenerator] Stage3 퍼즐 슬롯을 사용할 수 없습니다. " +
                $"Zone={context.Catalog.Zone} | SlotId={stage3Slot.SlotId} | UsageType={stage3Slot.UsageType}");
            return;
        }

        PuzzleDefinition definition = stage3Definitions[0]; // 현재는 Stage3 정의 1종만 사용
        if (definition == null)
            return;

        stage3Slot.MarkOccupied(); // Stage3 슬롯 점유 처리

        result.AddPuzzlePlan(new RoundGenerationResult.PuzzleSpawnPlan
        {
            Definition = definition,
            Zone = context.Catalog.Zone,
            Stage = PuzzleStage.Stage3,
            TargetRoom = null, // Stage3는 Room 점유 구조에 묶지 않음
            TargetSlot = stage3Slot,
            AnswerSeed = BuildAnswerSeed(result.RoundSeed, context.Catalog.Zone, PuzzleStage.Stage3, 0)
        });
    }

    /// <summary>
    /// Zone별 FinalCode 힌트/정답 생성용 파생 시드 생성.
    /// </summary>
    private static int BuildFinalCodeSeed(int roundSeed, Zone zone)
    {
        int seed = roundSeed;
        seed = (seed * 397) ^ (int)zone;
        seed = (seed * 397) ^ FinalCodeSeedSalt;
        seed = (seed * 397) ^ AnswerSeedSalt;

        if (seed == 0)
            seed = 1;

        return seed;
    }

    /// <summary>
    /// 퍼즐별 정답 생성용 파생 시드 생성.
    /// 같은 판 / 같은 존 / 같은 단계 / 같은 로컬 인덱스면 같은 시드 보장.
    /// </summary>
    private static int BuildAnswerSeed(int roundSeed, Zone zone, PuzzleStage stage, int localIndex)
    {
        int seed = roundSeed;
        seed = (seed * 397) ^ (int)zone;
        seed = (seed * 397) ^ (int)stage;
        seed = (seed * 397) ^ localIndex;
        seed = (seed * 397) ^ AnswerSeedSalt;

        if (seed == 0)
            seed = 1;

        return seed;
    }
}