using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 라운드의 퍼즐 / 힌트 / 플레이어 배치 결과를 담는 데이터 컨테이너.
/// 
/// 역할
/// - RoundGenerator가 만든 배치 계획을 PuzzleSpawnManager에 전달한다.
/// - 퍼즐 본체 스폰 계획과 힌트 스폰 계획을 보관한다.
/// - FinalCode 정답/힌트 데이터와 FinalCodeSeed를 라운드 단위로 1개만 보관한다.
/// </summary>
public class RoundGenerationResult
{
    /// <summary>
    /// 이번 라운드의 원본 기준 시드.
    /// </summary>
    public int RoundSeed;

    /// <summary>
    /// FinalCode 전용 파생 시드.
    /// Stage2 화면 힌트와 Stage3 FinalCodePuzzle 정답이 반드시 이 seed를 공유해야 한다.
    /// </summary>
    public int FinalCodeSeed;

    /// <summary>
    /// 라운드 전체 FinalCode 정답/힌트 데이터.
    /// ZoneA 힌트 3개와 ZoneB 힌트 3개를 모두 포함한다.
    /// </summary>
    public FinalCodeAnswerGenerator.FinalCodeAnswerData FinalCodeData;

    /// <summary>
    /// 퍼즐 스폰 계획 목록.
    /// </summary>
    public List<PuzzleSpawnPlan> PuzzlePlans = new();

    /// <summary>
    /// 플레이어 배정 계획 목록.
    /// </summary>
    public List<PlayerAssignmentPlan> PlayerAssignments = new();

    /// <summary>
    /// 퍼즐 스폰 계획 1개를 추가한다.
    /// </summary>
    public void AddPuzzlePlan(PuzzleSpawnPlan plan)
    {
        if (plan == null)
            return;

        PuzzlePlans.Add(plan);
    }

    /// <summary>
    /// 플레이어 배정 계획 1개를 추가한다.
    /// </summary>
    public void AddPlayerAssignment(PlayerAssignmentPlan plan)
    {
        if (plan == null)
            return;

        PlayerAssignments.Add(plan);
    }

    /// <summary>
    /// 특정 Zone에 속한 퍼즐 스폰 계획 목록을 반환한다.
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
    /// 퍼즐 본체 1개의 스폰 계획.
    /// </summary>
    public class PuzzleSpawnPlan
    {
        public PuzzleDefinition Definition;                  // 스폰할 퍼즐 정의
        public Zone Zone;                                    // 이 퍼즐이 속한 Zone
        public PuzzleStage Stage;                            // 퍼즐 단계
        public RoomPlacementGroup TargetRoom;                // 배치 대상 Room, Stage3는 null 가능
        public PlacementSlotMeta TargetSlot;                 // 실제 스폰 슬롯
        public int AnswerSeed;                               // 이 퍼즐에 적용할 정답 seed
        public List<HintSpawnPlan> HintPlans = new();        // 이 퍼즐에 연결된 힌트 스폰 계획 목록
    }

    /// <summary>
    /// 힌트 오브젝트 1개의 스폰 계획.
    /// </summary>
    public class HintSpawnPlan
    {
        public string HintId;                                // 힌트 ID
        public NetworkObject HintPrefab;                     // 스폰할 힌트 프리팹
        public PuzzleDefinition.HintDefinition HintDefinition; // 원본 힌트 정의
        public RoomPlacementGroup TargetRoom;                // 힌트 배치 Room
        public PlacementSlotMeta TargetSlot;                 // 힌트 배치 Slot
        public Vector3 HintPositionOffset;                   // 슬롯 기준 위치 오프셋
        public Vector3 HintRotationOffset;                   // 슬롯 기준 회전 오프셋
    }

    /// <summary>
    /// 플레이어 SlotIndex 기준 Zone / Role 배정 계획.
    /// </summary>
    public class PlayerAssignmentPlan
    {
        public int SlotIndex;
        public Zone Zone;
        public PlayerRole Role;
    }
}