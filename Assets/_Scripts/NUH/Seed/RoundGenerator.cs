using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 기준 시드를 받아 한 판의 퍼즐/정답/플레이어 배치 결과를 계산
/// 실제 스폰/적용은 하지 않고 결과만 생성
/// </summary>
public static class RoundGenerator
{
    private const int PuzzleSeedSalt = 1001;
    private const int PlayerSeedSalt = 2001;
    private const int AnswerSeedSalt = 3001;

    /// <summary>
    /// 퍼즐/힌트 배치 결과 생성
    /// </summary>
    public static RoundGenerationResult GeneratePuzzlePlans(
        int roundSeed,
        PuzzleDefinitionDatabase database,
        ZonePuzzleSlotSet zoneA,
        ZonePuzzleSlotSet zoneB,
        int stage1SelectCount,
        int stage2SelectCount)
    {
        RoundGenerationResult result = new();
        result.RoundSeed = roundSeed;

        SeedRandom rng = new(roundSeed + PuzzleSeedSalt);

        List<PuzzleDefinition> stage1Pool = GetStableDefinitions(database, PuzzleStage.Stage1);
        List<PuzzleDefinition> stage2Pool = GetStableDefinitions(database, PuzzleStage.Stage2);

        List<PuzzleDefinition> aStage1 = rng.PickUnique(stage1Pool, stage1SelectCount);
        List<PuzzleDefinition> bStage1 = rng.PickUnique(stage1Pool, stage1SelectCount);
        List<PuzzleDefinition> aStage2 = rng.PickUnique(stage2Pool, stage2SelectCount);
        List<PuzzleDefinition> bStage2 = rng.PickUnique(stage2Pool, stage2SelectCount);

        AddZonePlans(result, rng, zoneA.Zone, PuzzleStage.Stage1, aStage1, zoneA.Stage1PuzzleSlots.Count, zoneB.Stage1HintSlots.Count);
        AddZonePlans(result, rng, zoneB.Zone, PuzzleStage.Stage1, bStage1, zoneB.Stage1PuzzleSlots.Count, zoneA.Stage1HintSlots.Count);
        AddZonePlans(result, rng, zoneA.Zone, PuzzleStage.Stage2, aStage2, zoneA.Stage2PuzzleSlots.Count, zoneB.Stage2HintSlots.Count);
        AddZonePlans(result, rng, zoneB.Zone, PuzzleStage.Stage2, bStage2, zoneB.Stage2PuzzleSlots.Count, zoneA.Stage2HintSlots.Count);

        return result;
    }

    /// <summary>
    /// 플레이어 SlotIndex 기준 Zone/Role 배정 결과 생성
    /// </summary>
    public static RoundGenerationResult GeneratePlayerAssignments(int roundSeed, IReadOnlyList<int> orderedSlotIndices)
    {
        RoundGenerationResult result = new();
        result.RoundSeed = roundSeed;

        SeedRandom rng = new(roundSeed + PlayerSeedSalt);

        List<int> copiedSlots = new(orderedSlotIndices);
        copiedSlots.Sort();

        rng.Shuffle(copiedSlots);

        for (int i = 0; i < copiedSlots.Count; i++)
        {
            int slotIndex = copiedSlots[i];

            Zone zone = i < 2 ? Zone.ZoneA : Zone.ZoneB;
            int slotInZone = i < 2 ? i : i - 2;
            PlayerRole role = slotInZone == 0 ? PlayerRole.WalkieTalkie : PlayerRole.Flashlight;

            result.PlayerAssignments.Add(new RoundGenerationResult.PlayerAssignmentPlan
            {
                SlotIndex = slotIndex,
                Zone = zone,
                Role = role
            });
        }

        return result;
    }

    /// <summary>
    /// 단계별 퍼즐 정의 목록을 안정적인 순서로 반환
    /// 같은 시드에서 inspector 순서 영향 최소화
    /// </summary>
    private static List<PuzzleDefinition> GetStableDefinitions(PuzzleDefinitionDatabase database, PuzzleStage stage)
    {
        return database
            .GetDefinitionsByStage(stage)
            .Where(d => d != null)
            .OrderBy(d => d.PuzzleId)
            .ToList();
    }

    /// <summary>
    /// 특정 존/단계의 퍼즐 계획을 결과에 추가
    /// 퍼즐 1개에 연결된 힌트 여러 개까지 같이 계산한다.
    /// </summary>
    private static void AddZonePlans(
        RoundGenerationResult result,
        SeedRandom rng,
        Zone zone,
        PuzzleStage stage,
        List<PuzzleDefinition> definitions,
        int puzzleSlotCount,
        int hintSlotCount)
    {
        List<int> puzzleSlotIndices = Enumerable.Range(0, puzzleSlotCount).ToList();
        List<int> hintSlotIndices = Enumerable.Range(0, hintSlotCount).ToList();

        rng.Shuffle(puzzleSlotIndices);
        rng.Shuffle(hintSlotIndices);

        int puzzleCursor = 0;
        int hintCursor = 0;
        int localIndex = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleDefinition def = definitions[i];
            if (def == null)
                continue;

            if (puzzleCursor >= puzzleSlotIndices.Count)
                break;

            int requiredHintCount = def.HintCount;

            // 힌트 슬롯이 부족하면 이 퍼즐은 스킵
            if (hintCursor + requiredHintCount > hintSlotIndices.Count)
                continue;

            int answerSeed = BuildAnswerSeed(result.RoundSeed, zone, stage, localIndex);

            RoundGenerationResult.PuzzleSpawnPlan puzzlePlan = new RoundGenerationResult.PuzzleSpawnPlan
            {
                Definition = def,
                Zone = zone,
                Stage = stage,
                PuzzleSlotIndex = puzzleSlotIndices[puzzleCursor],
                AnswerSeed = answerSeed
            };

            puzzleCursor++;
            localIndex++;

            for (int hintIndex = 0; hintIndex < def.HintDefinitions.Count; hintIndex++)
            {
                PuzzleDefinition.HintDefinition hintDef = def.HintDefinitions[hintIndex];
                if (hintDef == null || hintDef.HintPrefab == null)
                    continue;

                puzzlePlan.HintPlans.Add(new RoundGenerationResult.HintSpawnPlan
                {
                    HintId = hintDef.HintId,
                    HintPrefab = hintDef.HintPrefab,
                    HintSlotIndex = hintSlotIndices[hintCursor],
                    HintPositionOffset = hintDef.HintPositionOffset,
                    HintRotationOffset = hintDef.HintRotationOffset
                });

                hintCursor++;
            }

            result.PuzzlePlans.Add(puzzlePlan);
        }
    }

    /// <summary>
    /// 퍼즐별 정답 생성용 파생 시드 생성
    /// 같은 판 / 같은 존 / 같은 단계 / 같은 로컬 인덱스면 같은 정답 시드를 보장
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