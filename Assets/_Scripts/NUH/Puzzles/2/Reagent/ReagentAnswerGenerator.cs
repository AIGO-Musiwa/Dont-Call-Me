using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 정답 데이터 생성 유틸.
/// 
/// 역할
/// - 6종 시약 중 중복 없는 3개 정답 순서를 생성한다.
/// - 프로그레스 10칸 중 행동 힌트 3개를 생성한다.
/// - 같은 seed를 받은 퍼즐 본체와 힌트가 동일한 데이터를 재구성할 수 있게 한다.
/// </summary>
public static class ReagentAnswerGenerator
{
    /// <summary>
    /// 시약 제조 퍼즐 한 판의 정답 데이터를 담는 컨테이너.
    /// </summary>
    [Serializable]
    public class ReagentAnswerData
    {
        public int ProgressStepCount; // 프로그레스 총 칸 수
        public int TotalReagentTypeCount; // 전체 시약 종류 수
        public readonly List<ReagentType> RecipeSequence = new(); // 정답 시약 3개 순서
        public readonly List<ReagentActionStep> ActionSteps = new(); // 정답 행동 힌트 3개
    }

    /// <summary>
    /// 특정 프로그레스 칸에서 수행해야 하는 행동 1개를 담는 데이터.
    /// </summary>
    [Serializable]
    public class ReagentActionStep
    {
        public int ProgressIndex; // 몇 번째 칸에서 입력해야 하는지
        public ReagentActionType ActionType; // 해당 칸의 정답 행동 타입
    }

    private const int DefaultProgressStepCount = 10; // 기본 프로그레스 칸 수
    private const int DefaultRecipeSlotCount = 3; // 기본 시약 선택 칸 수
    private const int DefaultActionStepCount = 3; // 기본 행동 힌트 개수
    private const int DefaultTotalReagentTypeCount = 6; // 전체 시약 종류 수

    /// <summary>
    /// seed를 받아 시약 제조 퍼즐 정답 데이터를 생성한다.
    /// </summary>
    public static ReagentAnswerData Generate(int seed)
    {
        SeedRandom rng = new SeedRandom(seed); // 시드 기반 랜덤 생성기
        ReagentAnswerData data = new ReagentAnswerData(); // 결과 데이터 생성

        data.ProgressStepCount = DefaultProgressStepCount; // 프로그레스 칸 수 설정
        data.TotalReagentTypeCount = DefaultTotalReagentTypeCount; // 전체 시약 종류 수 설정

        BuildRecipeSequence(data, rng); // 시약 순서 정답 생성
        BuildActionSteps(data, rng); // 행동 힌트 정답 생성
        SortActionStepsByProgressIndex(data.ActionSteps); // 행동 힌트를 숫자 오름차순으로 정렬

        return data; // 완성된 정답 데이터 반환
    }

    /// <summary>
    /// 6종 시약 중 중복 없이 3개를 뽑아 정답 순서를 만든다.
    /// </summary>
    private static void BuildRecipeSequence(ReagentAnswerData data, SeedRandom rng)
    {
        List<ReagentType> candidates = new List<ReagentType>
        {
            ReagentType.ReagentA, // 시약 후보 A
            ReagentType.ReagentB, // 시약 후보 B
            ReagentType.ReagentC, // 시약 후보 C
            ReagentType.ReagentD, // 시약 후보 D
            ReagentType.ReagentE, // 시약 후보 E
            ReagentType.ReagentF  // 시약 후보 F
        };

        rng.Shuffle(candidates); // 후보 순서를 시드 기준으로 섞음

        data.RecipeSequence.Clear(); // 기존 시약 순서 초기화

        for (int i = 0; i < DefaultRecipeSlotCount; i++)
            data.RecipeSequence.Add(candidates[i]); // 앞 3개를 정답 순서로 채움
    }

    /// <summary>
    /// 프로그레스 10칸 중 행동 힌트 3개를 생성한다.
    /// 각 칸은 중복 없이 뽑힌다.
    /// </summary>
    private static void BuildActionSteps(ReagentAnswerData data, SeedRandom rng)
    {
        List<int> candidateIndices = new List<int>(); // 행동 힌트에 사용할 칸 후보 목록

        for (int i = 1; i <= data.ProgressStepCount; i++)
            candidateIndices.Add(i); // 1칸부터 10칸까지 전부 후보로 등록

        rng.Shuffle(candidateIndices); // 칸 후보 순서를 시드 기준으로 섞음

        data.ActionSteps.Clear(); // 기존 행동 힌트 초기화

        for (int i = 0; i < DefaultActionStepCount; i++)
        {
            ReagentActionStep step = new ReagentActionStep(); // 행동 힌트 1개 생성
            step.ProgressIndex = candidateIndices[i]; // 중복 없는 진행 칸 배정

            int randomValue = rng.NextInt(0, 2); // 0 또는 1 반환
            step.ActionType = randomValue == 0
                ? ReagentActionType.Heat
                : ReagentActionType.Cool; // 가열/냉각 중 하나 배정

            data.ActionSteps.Add(step); // 행동 힌트 목록에 추가
        }
    }

    /// <summary>
    /// 행동 힌트들을 진행 칸 숫자 기준 오름차순으로 정렬한다.
    /// 힌트 화면에서 좌->우로 바로 표시하기 위해 사용한다.
    /// </summary>
    private static void SortActionStepsByProgressIndex(List<ReagentActionStep> steps)
    {
        if (steps == null)
            return; // 리스트가 없으면 종료

        steps.Sort((a, b) => a.ProgressIndex.CompareTo(b.ProgressIndex)); // 숫자 오름차순 정렬
    }
}