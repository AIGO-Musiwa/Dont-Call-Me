using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 판의 랜덤 생성 결과를 담는 데이터 컨테이너
/// 퍼즐 배치, 힌트 배치, 퍼즐 정답 시드, 플레이어 배정 결과를 보관
/// </summary>
public class RoundGenerationResult
{
    /// <summary>
    /// 개별 힌트 배치 결과
    /// </summary>
    public class HintSpawnPlan
    {
        public string HintId;                   // 어떤 힌트인지
        public NetworkObject HintPrefab;        // 스폰할 힌트 프리팹
        public int HintSlotIndex;               // 배치할 힌트 슬롯 인덱스
        public Vector3 HintPositionOffset;      // 힌트 로컬 위치 오프셋
        public Vector3 HintRotationOffset;      // 힌트 로컬 회전 오프셋
    }

    /// <summary>
    /// 개별 퍼즐 배치 결과
    /// </summary>
    public class PuzzleSpawnPlan
    {
        public PuzzleDefinition Definition;     // 어떤 퍼즐 정의인지
        public Zone Zone;                       // 어느 존에 퍼즐 본체가 배치되는지
        public PuzzleStage Stage;               // 몇 단계 퍼즐인지
        public int PuzzleSlotIndex;             // 퍼즐 슬롯 인덱스
        public int AnswerSeed;                  // 이 퍼즐의 정답 생성용 시드
        public readonly List<HintSpawnPlan> HintPlans = new(); // 이 퍼즐에 연결된 힌트 배치 계획들
    }

    /// <summary>
    /// 개별 플레이어 배치 결과
    /// SlotIndex 기준으로 적용한다
    /// </summary>
    public class PlayerAssignmentPlan
    {
        public int SlotIndex;
        public Zone Zone;
        public PlayerRole Role;
    }

    public int RoundSeed; // 이번 라운드 원본 시드

    public readonly List<PuzzleSpawnPlan> PuzzlePlans = new(); // 퍼즐 스폰 계획 목록
    public readonly List<PlayerAssignmentPlan> PlayerAssignments = new(); // 플레이어 배정 결과 목록

    public FinalCodeAnswerGenerator.FinalCodeAnswerData ZoneAFinalCodeData; // A존 최종 코드 퍼즐 정답/힌트 데이터
    public FinalCodeAnswerGenerator.FinalCodeAnswerData ZoneBFinalCodeData; // B존 최종 코드 퍼즐 정답/힌트 데이터
}