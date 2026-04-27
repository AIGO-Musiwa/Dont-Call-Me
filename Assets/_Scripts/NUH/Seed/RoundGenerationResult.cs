using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 한 판의 랜덤 생성 결과를 담는 데이터 컨테이너.
/// 
/// 역할
/// - 퍼즐 배치 계획 보관
/// - 힌트 배치 계획 보관
/// - 플레이어 배정 결과 보관
/// - Zone별 FinalCode 정답/힌트 데이터 보관
/// 
/// 변경점
/// - 기존 슬롯 인덱스 기반 구조를 제거
/// - 실제 선택된 Room / Slot 참조를 직접 보관하는 구조로 변경
/// </summary>
public class RoundGenerationResult
{
    /// <summary>
    /// 개별 힌트 배치 결과
    /// </summary>
    public class HintSpawnPlan
    {
        public string HintId;                                    // 힌트 식별용 ID
        public NetworkObject HintPrefab;                         // 실제 스폰할 힌트 프리팹
        public PuzzleDefinition.HintDefinition HintDefinition;   // 원본 힌트 정의 데이터
        public RoomPlacementGroup TargetRoom;                    // 이 힌트를 배치할 대상 방
        public PlacementSlotMeta TargetSlot;                     // 이 힌트를 배치할 대상 슬롯
        public Vector3 HintPositionOffset;                       // 힌트 로컬 위치 보정값
        public Vector3 HintRotationOffset;                       // 힌트 로컬 회전 보정값
    }

    /// <summary>
    /// 개별 퍼즐 배치 결과
    /// </summary>
    public class PuzzleSpawnPlan
    {
        public PuzzleDefinition Definition;                      // 어떤 퍼즐 정의인지
        public Zone Zone;                                        // 어느 Zone에 퍼즐 본체가 배치되는지
        public PuzzleStage Stage;                                // 몇 단계 퍼즐인지
        public RoomPlacementGroup TargetRoom;                    // 이 퍼즐을 배치할 대상 방
        public PlacementSlotMeta TargetSlot;                     // 이 퍼즐을 배치할 대상 슬롯
        public int AnswerSeed;                                   // 퍼즐 정답 생성용 시드
        public readonly List<HintSpawnPlan> HintPlans = new();   // 연결된 힌트 배치 계획들
    }

    /// <summary>
    /// 개별 플레이어 배정 결과
    /// SlotIndex 기준으로 적용한다.
    /// </summary>
    public class PlayerAssignmentPlan
    {
        public int SlotIndex;    // 실제 플레이어 슬롯 인덱스
        public Zone Zone;        // 배정된 존
        public PlayerRole Role;  // 배정된 역할
    }

    public int RoundSeed; // 이번 라운드 원본 시드

    public readonly List<PuzzleSpawnPlan> PuzzlePlans = new();               // 퍼즐 스폰 계획 목록
    public readonly List<PlayerAssignmentPlan> PlayerAssignments = new();    // 플레이어 배정 결과 목록

    public FinalCodeAnswerGenerator.FinalCodeAnswerData ZoneAFinalCodeData;  // ZoneA FinalCode 정답/힌트 데이터
    public FinalCodeAnswerGenerator.FinalCodeAnswerData ZoneBFinalCodeData;  // ZoneB FinalCode 정답/힌트 데이터

    /// <summary>
    /// 퍼즐 스폰 계획 1개를 결과에 추가한다.
    /// </summary>
    public void AddPuzzlePlan(PuzzleSpawnPlan plan)
    {
        if (plan == null)
            return;

        PuzzlePlans.Add(plan);
    }

    /// <summary>
    /// 플레이어 배정 결과 1개를 결과에 추가한다.
    /// </summary>
    public void AddPlayerAssignment(PlayerAssignmentPlan plan)
    {
        if (plan == null)
            return;

        PlayerAssignments.Add(plan);
    }

    /// <summary>
    /// 특정 Zone에 속한 퍼즐 배치 계획만 반환한다.
    /// </summary>
    public List<PuzzleSpawnPlan> GetPlansByZone(Zone zone)
    {
        List<PuzzleSpawnPlan> result = new();

        for (int i = 0; i < PuzzlePlans.Count; i++)
        {
            PuzzleSpawnPlan plan = PuzzlePlans[i];
            if (plan == null)
                continue;

            if (plan.Zone != zone)
                continue;

            result.Add(plan);
        }

        return result;
    }

    /// <summary>
    /// 특정 Stage에 속한 퍼즐 배치 계획만 반환한다.
    /// </summary>
    public List<PuzzleSpawnPlan> GetPlansByStage(PuzzleStage stage)
    {
        List<PuzzleSpawnPlan> result = new();

        for (int i = 0; i < PuzzlePlans.Count; i++)
        {
            PuzzleSpawnPlan plan = PuzzlePlans[i];
            if (plan == null)
                continue;

            if (plan.Stage != stage)
                continue;

            result.Add(plan);
        }

        return result;
    }

    /// <summary>
    /// 특정 Zone의 Stage3 퍼즐 스폰 계획을 반환한다.
    /// 없으면 null 반환.
    /// </summary>
    public PuzzleSpawnPlan FindStage3Plan(Zone zone)
    {
        for (int i = 0; i < PuzzlePlans.Count; i++)
        {
            PuzzleSpawnPlan plan = PuzzlePlans[i];
            if (plan == null)
                continue;

            if (plan.Zone != zone)
                continue;

            if (plan.Stage != PuzzleStage.Stage3)
                continue;

            return plan;
        }

        return null;
    }
}