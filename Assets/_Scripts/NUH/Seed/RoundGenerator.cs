using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 기준 시드를 받아 한 판의 퍼즐/힌트/플레이어 배치 결과를 계산하는 정적 유틸.
/// 
/// 핵심 규칙
/// - Stage1 / Stage2 퍼즐과 힌트는 Room 기반으로 랜덤 배치한다.
/// - 힌트는 1차로 방 중복 없이 배치하고, 실패 시 2차로 방 중복 허용 배치한다.
/// - Stage3 퍼즐은 Zone별 Stage3Puzzle 슬롯에 고정 배치한다.
/// - FinalCodeData는 라운드당 1개만 생성한다.
/// - Stage2 화면 힌트와 Stage3 FinalCodePuzzle 정답은 같은 FinalCodeSeed를 공유한다.
/// </summary>
public static class RoundGenerator
{
    private const int PuzzleSeedSalt = 1001;    // 퍼즐 배치용 파생 시드 salt
    private const int PlayerSeedSalt = 2001;    // 플레이어 배정용 파생 시드 salt
    private const int AnswerSeedSalt = 3001;    // 퍼즐 정답 생성용 파생 시드 salt
    private const int FinalCodeSeedSalt = 9001; // FinalCode 전용 salt

    /// <summary>
    /// Zone별 Room 배치 컨텍스트.
    /// </summary>
    private sealed class ZonePlacementContext
    {
        public ZonePlacementCatalog Catalog;
        public List<RoomPlacementGroup> Rooms = new();
        public FloorDistributionState Stage1Distribution = new();
        public FloorDistributionState Stage2Distribution = new();
    }

    /// <summary>
    /// 층별 현재 배치 수를 관리하는 내부 상태 클래스.
    /// </summary>
    private sealed class FloorDistributionState
    {
        private int _floor1Count;
        private int _floor2Count;
        private int _floor3Count;

        public int TotalPlaced => _floor1Count + _floor2Count + _floor3Count;

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

        public int GetMinCount()
        {
            return Mathf.Min(_floor1Count, Mathf.Min(_floor2Count, _floor3Count));
        }

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
        RoundGenerationResult result = new RoundGenerationResult();
        result.RoundSeed = roundSeed;

        SeedRandom rng = new SeedRandom(roundSeed + PuzzleSeedSalt);

        // 이전 런타임 점유 상태 초기화
        zoneA?.ClearRuntimeOccupancy();
        zoneB?.ClearRuntimeOccupancy();

        ZonePlacementContext zoneAContext = BuildZonePlacementContext(zoneA);
        ZonePlacementContext zoneBContext = BuildZonePlacementContext(zoneB);

        List<PuzzleDefinition> stage1Pool = GetStableDefinitions(database, PuzzleStage.Stage1);
        List<PuzzleDefinition> stage2Pool = GetStableDefinitions(database, PuzzleStage.Stage2);
        List<PuzzleDefinition> stage3Pool = GetStableDefinitions(database, PuzzleStage.Stage3);

        // FinalCode 데이터는 라운드 전체에서 1번만 생성한다.
        result.FinalCodeSeed = BuildFinalCodeSeed(roundSeed);
        result.FinalCodeData = FinalCodeAnswerGenerator.Generate(result.FinalCodeSeed);

        // Zone별 퍼즐 종류 선택
        List<PuzzleDefinition> zoneAStage1Defs = rng.PickUnique(stage1Pool, stage1SelectCount);
        List<PuzzleDefinition> zoneBStage1Defs = rng.PickUnique(stage1Pool, stage1SelectCount);
        List<PuzzleDefinition> zoneAStage2Defs = rng.PickUnique(stage2Pool, stage2SelectCount);
        List<PuzzleDefinition> zoneBStage2Defs = rng.PickUnique(stage2Pool, stage2SelectCount);

        // Stage1 / Stage2 퍼즐 배치
        PlaceStage1Puzzles(result, rng, zoneAContext, zoneAStage1Defs);
        PlaceStage1Puzzles(result, rng, zoneBContext, zoneBStage1Defs);
        PlaceStage2Puzzles(result, rng, zoneAContext, zoneAStage2Defs);
        PlaceStage2Puzzles(result, rng, zoneBContext, zoneBStage2Defs);

        // 힌트는 퍼즐 배치 후 반대 Zone에 배치
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneA, PuzzleStage.Stage1, zoneBContext);
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneB, PuzzleStage.Stage1, zoneAContext);
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneA, PuzzleStage.Stage2, zoneBContext);
        PlaceHintsForPlacedPuzzles(result, rng, Zone.ZoneB, PuzzleStage.Stage2, zoneAContext);

        // Stage3 FinalCodePuzzle도 FinalCodeSeed를 사용해야 Stage2 힌트와 정답이 일치한다.
        AddStage3PuzzlePlan(result, zoneAContext, stage3Pool);
        AddStage3PuzzlePlan(result, zoneBContext, stage3Pool);

        return result;
    }

    /// <summary>
    /// 플레이어 SlotIndex 기준 Zone/Role 배정 결과 생성.
    /// 기존 로직 유지.
    /// </summary>
    public static RoundGenerationResult GeneratePlayerAssignments(int roundSeed, IReadOnlyList<int> orderedSlotIndices)
    {
        RoundGenerationResult result = new RoundGenerationResult();
        result.RoundSeed = roundSeed;

        SeedRandom rng = new SeedRandom(roundSeed + PlayerSeedSalt);

        List<int> copiedSlots = new List<int>(orderedSlotIndices);
        copiedSlots.Sort();
        rng.Shuffle(copiedSlots);

        for (int i = 0; i < copiedSlots.Count; i++)
        {
            int slotIndex = copiedSlots[i];

            Zone zone = i < 2 ? Zone.ZoneA : Zone.ZoneB;
            int slotInZone = i < 2 ? i : i - 2;

            PlayerRole role = slotInZone == 0
                ? PlayerRole.WalkieTalkie
                : PlayerRole.Flashlight;

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

        int localIndex = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleDefinition definition = definitions[i];
            if (definition == null)
                continue;

            RoomPlacementGroup room = PickBestRoomForStage1(context, rng, true);
            if (room == null)
                continue;

            PlacementSlotMeta slot = room.GetRandomAvailablePuzzleSlot(rng);
            if (slot == null)
                continue;

            room.MarkRoomOccupied(slot);
            context.Stage1Distribution.AddPlaced(room.Floor);

            int answerSeed = BuildAnswerSeed(result.RoundSeed, context.Catalog.Zone, PuzzleStage.Stage1, localIndex);
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

        int localIndex = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleDefinition definition = definitions[i];
            if (definition == null)
                continue;

            RoomPlacementGroup room = PickBestRoomForStage2(context, rng, true);
            if (room == null)
                continue;

            PlacementSlotMeta slot = room.GetRandomAvailablePuzzleSlot(rng);
            if (slot == null)
                continue;

            room.MarkRoomOccupied(slot);
            context.Stage2Distribution.AddPlaced(room.Floor);

            int answerSeed = BuildAnswerSeed(result.RoundSeed, context.Catalog.Zone, PuzzleStage.Stage2, localIndex);
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

        List<RoundGenerationResult.PuzzleSpawnPlan> plans = result.GetPlansByZone(sourcePuzzleZone);

        for (int i = 0; i < plans.Count; i++)
        {
            RoundGenerationResult.PuzzleSpawnPlan puzzlePlan = plans[i];
            if (puzzlePlan == null)
                continue;

            if (puzzlePlan.Stage != stage)
                continue;

            PuzzleDefinition definition = puzzlePlan.Definition;
            if (definition == null || definition.HintDefinitions == null)
                continue;

            for (int hintIndex = 0; hintIndex < definition.HintDefinitions.Count; hintIndex++)
            {
                PuzzleDefinition.HintDefinition hintDefinition = definition.HintDefinitions[hintIndex];
                if (hintDefinition == null || hintDefinition.HintPrefab == null)
                    continue;

                bool placed = TryPlaceHintPlan(
                    puzzlePlan,
                    hintDefinition,
                    targetHintContext,
                    rng,
                    stage,
                    false);

                if (placed)
                    continue;

                placed = TryPlaceHintPlan(
                    puzzlePlan,
                    hintDefinition,
                    targetHintContext,
                    rng,
                    stage,
                    true);

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
            ignoreRoomOccupancy);

        if (hintRoom == null)
            return false;

        PlacementSlotMeta hintSlot = hintRoom.GetRandomAvailableHintSlotPreferHintOnly(
            rng,
            ignoreRoomOccupancy);

        if (hintSlot == null)
            return false;

        hintRoom.MarkRoomOccupied(hintSlot);

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
    /// </summary>
    private static RoomPlacementGroup PickBestRoomForStage1(
        ZonePlacementContext context,
        SeedRandom rng,
        bool usePuzzleSlots,
        bool ignoreRoomOccupancy = false)
    {
        if (context == null || rng == null)
            return null;

        List<RoomPlacementGroup> candidates = new List<RoomPlacementGroup>();

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

        return PickWeightedRoomByFloorScore(candidates, context.Stage1Distribution, true, rng);
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

        List<RoomPlacementGroup> candidates = new List<RoomPlacementGroup>();

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

        return PickWeightedRoomByFloorScore(candidates, context.Stage2Distribution, false, rng);
    }

    /// <summary>
    /// Stage 힌트를 위한 Room을 선택한다.
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

        float totalWeight = 0f;
        List<float> weights = new List<float>();

        for (int i = 0; i < candidates.Count; i++)
        {
            RoomPlacementGroup room = candidates[i];
            if (room == null)
            {
                weights.Add(0f);
                continue;
            }

            float score = isStage1
                ? EvaluateStage1FloorScore(room.Floor, distribution)
                : EvaluateStage2FloorScore(room.Floor, distribution);

            score = Mathf.Max(0.01f, score);
            weights.Add(score);
            totalWeight += score;
        }

        if (totalWeight <= 0f)
        {
            int randomIndex = rng.NextInt(0, candidates.Count);
            return candidates[randomIndex];
        }

        float pick = rng.NextFloat() * totalWeight;
        float accum = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            accum += weights[i];
            if (pick <= accum)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
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

        int current = distribution.GetCount(floor);
        int min = distribution.GetMinCount();

        float crowdPenalty = Mathf.Max(0, current - min) * 0.45f;
        float emptyThirdFloorBonus = 0f;

        if (floor == 3 && distribution.TotalPlaced >= 2 && distribution.GetCount(3) == 0)
            emptyThirdFloorBonus = 0.35f;

        return Mathf.Max(0.05f, baseScore - crowdPenalty + emptyThirdFloorBonus);
    }

    /// <summary>
    /// Stage2 층 점수 계산.
    /// </summary>
    private static float EvaluateStage2FloorScore(int floor, FloorDistributionState distribution)
    {
        float baseScore = 1.0f;

        int current = distribution.GetCount(floor);
        int min = distribution.GetMinCount();

        float crowdPenalty = Mathf.Max(0, current - min) * 0.50f;

        return Mathf.Max(0.05f, baseScore - crowdPenalty);
    }

    /// <summary>
    /// Zone의 Stage3Puzzle 슬롯에 Stage3 퍼즐 스폰 계획을 추가한다.
    /// Stage3 FinalCodePuzzle은 반드시 FinalCodeSeed를 AnswerSeed로 사용한다.
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

        PlacementSlotMeta stage3Slot = context.Catalog.GetStage3PuzzleSlot();
        if (stage3Slot == null)
            return;

        if (!stage3Slot.CanPlaceStage3Puzzle())
        {
            Debug.LogWarning(
                $"[RoundGenerator] Stage3 퍼즐 슬롯을 사용할 수 없습니다. " +
                $"Zone={context.Catalog.Zone} | SlotId={stage3Slot.SlotId} | UsageType={stage3Slot.UsageType}");
            return;
        }

        PuzzleDefinition definition = stage3Definitions[0];
        if (definition == null)
            return;

        stage3Slot.MarkOccupied();

        result.AddPuzzlePlan(new RoundGenerationResult.PuzzleSpawnPlan
        {
            Definition = definition,
            Zone = context.Catalog.Zone,
            Stage = PuzzleStage.Stage3,
            TargetRoom = null,
            TargetSlot = stage3Slot,

            // 핵심 수정:
            // Stage3 FinalCodePuzzle은 Stage2 힌트를 만든 FinalCodeSeed를 그대로 받아야 한다.
            AnswerSeed = result.FinalCodeSeed
        });
    }

    /// <summary>
    /// 라운드 전체 FinalCode 힌트/정답 생성용 파생 시드 생성.
    /// Zone별로 나누지 않고 라운드당 1개만 만든다.
    /// </summary>
    private static int BuildFinalCodeSeed(int roundSeed)
    {
        int seed = roundSeed;
        seed = (seed * 397) ^ FinalCodeSeedSalt;
        seed = (seed * 397) ^ AnswerSeedSalt;

        if (seed == 0)
            seed = 1;

        return seed;
    }

    /// <summary>
    /// 일반 퍼즐별 정답 생성용 파생 시드 생성.
    /// FinalCodePuzzle에는 사용하지 않는다.
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