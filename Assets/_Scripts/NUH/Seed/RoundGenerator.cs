using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 기준 시드를 받아 한 판의 퍼즐/정답/플레이어 배치 결과를 계산
/// 실제 스폰/적용은 하지 않고 결과만 생성
/// </summary>
public static class RoundGenerator
{
    private const int PuzzleSeedSalt = 1001; // 퍼즐 배치용 파생 시드 salt
    private const int PlayerSeedSalt = 2001; // 플레이어 배정용 파생 시드 salt
    private const int AnswerSeedSalt = 3001; // 퍼즐 정답 생성용 파생 시드 salt

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
        RoundGenerationResult result = new(); // 이번 라운드 결과 컨테이너 생성
        result.RoundSeed = roundSeed;         // 원본 라운드 시드 기록

        SeedRandom rng = new(roundSeed + PuzzleSeedSalt); // 퍼즐 배치 전용 랜덤 생성기

        List<PuzzleDefinition> stage1Pool = GetStableDefinitions(database, PuzzleStage.Stage1); // 1단계 퍼즐 후보 풀
        List<PuzzleDefinition> stage2Pool = GetStableDefinitions(database, PuzzleStage.Stage2); // 2단계 퍼즐 후보 풀
        List<PuzzleDefinition> stage3Pool = GetStableDefinitions(database, PuzzleStage.Stage3); // 3단계 퍼즐 후보 풀

        List<PuzzleDefinition> aStage1 = rng.PickUnique(stage1Pool, stage1SelectCount); // A존 1단계 퍼즐 선택
        List<PuzzleDefinition> bStage1 = rng.PickUnique(stage1Pool, stage1SelectCount); // B존 1단계 퍼즐 선택
        List<PuzzleDefinition> aStage2 = rng.PickUnique(stage2Pool, stage2SelectCount); // A존 2단계 퍼즐 선택
        List<PuzzleDefinition> bStage2 = rng.PickUnique(stage2Pool, stage2SelectCount); // B존 2단계 퍼즐 선택

        AddZonePlans(result, rng, zoneA.Zone, PuzzleStage.Stage1, aStage1, zoneA.Stage1PuzzleSlots.Count, zoneB.Stage1HintSlots.Count); // A존 1단계 배치 계획 생성
        AddZonePlans(result, rng, zoneB.Zone, PuzzleStage.Stage1, bStage1, zoneB.Stage1PuzzleSlots.Count, zoneA.Stage1HintSlots.Count); // B존 1단계 배치 계획 생성
        AddZonePlans(result, rng, zoneA.Zone, PuzzleStage.Stage2, aStage2, zoneA.Stage2PuzzleSlots.Count, zoneB.Stage2HintSlots.Count); // A존 2단계 배치 계획 생성
        AddZonePlans(result, rng, zoneB.Zone, PuzzleStage.Stage2, bStage2, zoneB.Stage2PuzzleSlots.Count, zoneA.Stage2HintSlots.Count); // B존 2단계 배치 계획 생성

        AddStage3FinalCodePlan(result, zoneA.Zone, stage3Pool, zoneA.Stage3PuzzleSlots.Count); // A존 최종 퍼즐 배치 계획 생성
        AddStage3FinalCodePlan(result, zoneB.Zone, stage3Pool, zoneB.Stage3PuzzleSlots.Count); // B존 최종 퍼즐 배치 계획 생성

        result.ZoneAFinalCodeData = FinalCodeAnswerGenerator.Generate(BuildFinalCodeSeed(roundSeed, Zone.ZoneA)); // A존 최종 코드 데이터 생성
        result.ZoneBFinalCodeData = FinalCodeAnswerGenerator.Generate(BuildFinalCodeSeed(roundSeed, Zone.ZoneB)); // B존 최종 코드 데이터 생성

        return result; // 완성된 퍼즐/힌트 배치 결과 반환
    }

    /// <summary>
    /// 플레이어 SlotIndex 기준 Zone/Role 배정 결과 생성
    /// </summary>
    public static RoundGenerationResult GeneratePlayerAssignments(int roundSeed, IReadOnlyList<int> orderedSlotIndices)
    {
        RoundGenerationResult result = new(); // 플레이어 배정 결과 컨테이너 생성
        result.RoundSeed = roundSeed;         // 원본 라운드 시드 기록

        SeedRandom rng = new(roundSeed + PlayerSeedSalt); // 플레이어 배정 전용 랜덤 생성기

        List<int> copiedSlots = new(orderedSlotIndices); // 원본 슬롯 목록 복사
        copiedSlots.Sort();                              // 슬롯 순서 고정

        rng.Shuffle(copiedSlots); // 시드 기준으로 슬롯 순서 섞기

        for (int i = 0; i < copiedSlots.Count; i++)
        {
            int slotIndex = copiedSlots[i]; // 현재 플레이어 슬롯 인덱스

            Zone zone = i < 2 ? Zone.ZoneA : Zone.ZoneB;                    // 앞 2명은 ZoneA, 뒤 2명은 ZoneB
            int slotInZone = i < 2 ? i : i - 2;                             // 존 내부에서 몇 번째 플레이어인지
            PlayerRole role = slotInZone == 0 ? PlayerRole.WalkieTalkie : PlayerRole.Flashlight; // 존 내부 첫 번째는 무전기, 두 번째는 손전등

            result.PlayerAssignments.Add(new RoundGenerationResult.PlayerAssignmentPlan
            {
                SlotIndex = slotIndex, // 실제 플레이어 슬롯 인덱스
                Zone = zone,           // 배정된 존
                Role = role            // 배정된 역할
            });
        }

        return result; // 플레이어 배정 결과 반환
    }

    /// <summary>
    /// 단계별 퍼즐 정의 목록을 안정적인 순서로 반환
    /// 같은 시드에서 inspector 순서 영향 최소화
    /// </summary>
    private static List<PuzzleDefinition> GetStableDefinitions(PuzzleDefinitionDatabase database, PuzzleStage stage)
    {
        return database
            .GetDefinitionsByStage(stage) // 해당 단계 퍼즐 목록 가져오기
            .Where(d => d != null)        // null 정의 제거
            .OrderBy(d => d.PuzzleId)     // PuzzleId 기준 정렬로 순서 고정
            .ToList();                    // 리스트로 반환
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
        List<int> puzzleSlotIndices = Enumerable.Range(0, puzzleSlotCount).ToList(); // 사용 가능한 퍼즐 슬롯 인덱스 목록
        List<int> hintSlotIndices = Enumerable.Range(0, hintSlotCount).ToList();     // 사용 가능한 힌트 슬롯 인덱스 목록

        rng.Shuffle(puzzleSlotIndices); // 퍼즐 슬롯 순서를 시드 기준으로 섞기
        rng.Shuffle(hintSlotIndices);   // 힌트 슬롯 순서를 시드 기준으로 섞기

        int puzzleCursor = 0; // 현재 사용할 퍼즐 슬롯 위치
        int hintCursor = 0;   // 현재 사용할 힌트 슬롯 위치
        int localIndex = 0;   // 해당 존/단계 내부 퍼즐 로컬 순번

        for (int i = 0; i < definitions.Count; i++)
        {
            PuzzleDefinition def = definitions[i]; // 현재 퍼즐 정의
            if (def == null)
                continue; // null 정의는 건너뛰기

            if (puzzleCursor >= puzzleSlotIndices.Count)
                break; // 퍼즐 슬롯이 더 없으면 종료

            int requiredHintCount = def.HintCount; // 이 퍼즐이 요구하는 힌트 개수

            // 힌트 슬롯이 부족하면 이 퍼즐은 스킵
            if (hintCursor + requiredHintCount > hintSlotIndices.Count)
                continue;

            int answerSeed = BuildAnswerSeed(result.RoundSeed, zone, stage, localIndex); // 이 퍼즐 전용 정답 시드 생성

            RoundGenerationResult.PuzzleSpawnPlan puzzlePlan = new RoundGenerationResult.PuzzleSpawnPlan
            {
                Definition = def,                                  // 어떤 퍼즐 정의인지
                Zone = zone,                                       // 어느 존에 배치되는지
                Stage = stage,                                     // 몇 단계 퍼즐인지
                PuzzleSlotIndex = puzzleSlotIndices[puzzleCursor], // 사용할 퍼즐 슬롯 인덱스
                AnswerSeed = answerSeed                            // 퍼즐 정답 시드
            };

            puzzleCursor++; // 다음 퍼즐 슬롯으로 이동
            localIndex++;   // 다음 퍼즐 로컬 인덱스로 이동

            for (int hintIndex = 0; hintIndex < def.HintDefinitions.Count; hintIndex++)
            {
                PuzzleDefinition.HintDefinition hintDef = def.HintDefinitions[hintIndex]; // 현재 힌트 정의
                if (hintDef == null || hintDef.HintPrefab == null)
                    continue; // null 힌트나 프리팹 없는 힌트는 건너뛰기

                puzzlePlan.HintPlans.Add(new RoundGenerationResult.HintSpawnPlan
                {
                    HintId = hintDef.HintId,                         // 힌트 식별용 ID
                    HintPrefab = hintDef.HintPrefab,                 // 스폰할 힌트 프리팹
                    HintSlotIndex = hintSlotIndices[hintCursor],     // 배치할 힌트 슬롯 인덱스
                    HintPositionOffset = hintDef.HintPositionOffset, // 힌트 위치 보정값
                    HintRotationOffset = hintDef.HintRotationOffset  // 힌트 회전 보정값
                });

                hintCursor++; // 다음 힌트 슬롯으로 이동
            }

            result.PuzzlePlans.Add(puzzlePlan); // 계산된 퍼즐 계획 결과에 추가
        }
    }

    /// <summary>
    /// 존별 최종 3단계 퍼즐 1개의 스폰 계획을 추가한다.
    /// Stage3 힌트는 월드에 스폰하지 않으므로 HintPlans는 비워둔다.
    /// </summary>
    private static void AddStage3FinalCodePlan(
        RoundGenerationResult result,
        Zone zone,
        List<PuzzleDefinition> stage3Definitions,
        int stage3PuzzleSlotCount)
    {
        if (stage3Definitions == null || stage3Definitions.Count == 0)
            return; // 3단계 퍼즐 정의가 없으면 종료

        if (stage3PuzzleSlotCount <= 0)
            return; // 3단계 퍼즐 슬롯이 없으면 종료

        PuzzleDefinition stage3Definition = stage3Definitions[0]; // 현재는 존당 1개의 최종 퍼즐만 사용
        if (stage3Definition == null)
            return;

        int answerSeed = BuildAnswerSeed(result.RoundSeed, zone, PuzzleStage.Stage3, 0); // 최종 퍼즐 전용 정답 시드 생성

        RoundGenerationResult.PuzzleSpawnPlan puzzlePlan = new RoundGenerationResult.PuzzleSpawnPlan
        {
            Definition = stage3Definition, // 최종 퍼즐 정의
            Zone = zone,                   // 배치 존
            Stage = PuzzleStage.Stage3,    // 3단계 퍼즐
            PuzzleSlotIndex = 0,           // 존별 Stage3 슬롯은 1개만 사용
            AnswerSeed = answerSeed        // 최종 퍼즐 시드
        };

        result.PuzzlePlans.Add(puzzlePlan); // Stage3 퍼즐 본체 계획 추가
    }

    /// <summary>
    /// 존별 FinalCode 힌트/정답 생성용 파생 시드 생성
    /// </summary>
    private static int BuildFinalCodeSeed(int roundSeed, Zone zone)
    {
        int seed = roundSeed;              // 기본 라운드 시드 시작
        seed = (seed * 397) ^ (int)zone;   // 존 정보 섞기
        seed = (seed * 397) ^ 9001;        // FinalCode 전용 구분 salt
        seed = (seed * 397) ^ AnswerSeedSalt; // 정답 생성 salt 섞기

        if (seed == 0)
            seed = 1; // 0 방지

        return seed;
    }

    /// <summary>
    /// 퍼즐별 정답 생성용 파생 시드 생성
    /// 같은 판 / 같은 존 / 같은 단계 / 같은 로컬 인덱스면 같은 정답 시드를 보장
    /// </summary>
    private static int BuildAnswerSeed(int roundSeed, Zone zone, PuzzleStage stage, int localIndex)
    {
        int seed = roundSeed;                 // 기본 라운드 시드 시작
        seed = (seed * 397) ^ (int)zone;      // 존 정보 섞기
        seed = (seed * 397) ^ (int)stage;     // 단계 정보 섞기
        seed = (seed * 397) ^ localIndex;     // 로컬 인덱스 섞기
        seed = (seed * 397) ^ AnswerSeedSalt; // 정답 시드 전용 salt 섞기

        if (seed == 0)
            seed = 1; // 0 시드는 방어적으로 1로 대체

        return seed; // 최종 파생 정답 시드 반환
    }
}