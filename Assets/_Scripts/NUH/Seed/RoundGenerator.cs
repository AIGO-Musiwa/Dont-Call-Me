using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 기준 시드를 받아 한 판의 퍼즐/정답/플레이어 배치 결과를 계산
/// 실제 스폰/적용은 하지 않고 결과만 생성
/// </summary>
public class RoundGenerator
{
    private const int PuzzleSeedSalt = 1001;        // 퍼즐 생성용 파생 시드 salt
    private const int PlayerSeedSalt = 2001;        // 플레이어 배정용 파생 시드 salt
    private const int AnswerSeedSalt = 3001;        // 퍼즐 정답용 파생 시드 salt

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
        copiedSlots.Sort();         // 입력 순서를 고정

        rng.Shuffle(copiedSlots);   // 같은 시드면 같은 셔플 결과

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
    /// 슬롯 배치와 정답 시드를 같이 계산
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
        int spawnCount = Mathf.Min(definitions.Count, puzzleSlotCount, hintSlotCount);

        List<int> puzzleSlotIndices = Enumerable.Range(0, puzzleSlotCount).ToList();
        List<int> hintSlotIndices = Enumerable.Range(0, hintSlotCount).ToList();

        rng.Shuffle(puzzleSlotIndices);
        rng.Shuffle(hintSlotIndices);

        for(int i = 0; i < spawnCount; i++)
        {
            PuzzleDefinition def = definitions[i];
            if (def == null)
                continue;

            int answerSeed = BuildAnswerSeed(result.RoundSeed, zone, stage, i);

            result.PuzzlePlans.Add(new RoundGenerationResult.PuzzleSpawnPlan
            {
                Definition = def,
                Zone = zone,
                Stage = stage,
                PuzzleSlotIndex = puzzleSlotIndices[i],
                HintSlotIndex = hintSlotIndices[i],
                AnswerSeed = answerSeed
            });
        }
    }

    /// <summary>
    /// 퍼즐별 정답 생성용 파생 시드 생성
    /// 같은 판/ 같은 퍼즐 위치면 같은 정답 시드를 보장
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
