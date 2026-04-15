using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 판의 랜덤 생성 결과를 담는 데이터 컨테이너
/// 퍼즐 배치, 힌트 배치, 퍼즐 정답 시드, 플레이어 배정 결과를 보관
/// </summary>
public class RoundGenerationResult
{
    /// <summary>
    /// 개별 퍼즐 배치 결과
    /// </summary>
    public class PuzzleSpawnPlan
    {
        public PuzzleDefinition Definition;     // 어떤 퍼즐 정의인지
        public Zone Zone;                       // 어느 존에 퍼즐 본체가 배치되는지
        public PuzzleStage Stage;               // 몇 단계 퍼즐인지
        public int PuzzleSlotIndex;             // 퍼즐 슬롯 인덱스
        public int HintSlotIndex;               // 반대편 힌트 슬롯 인덱스
        public int AnswerSeed;                  // 이 퍼즐의 정답 생성용 시드
    }

    /// <summary>
    /// 개별 플레이어 배치 결과
    /// SlotIndex 기준으로 적용한다
    /// </summary>
    public class PlayerAssignmentPlan
    {
        public int SlotIndex;                   // 로비 슬롯 인덱스
        public Zone Zone;                       // 배정될 존
        public PlayerRole Role;                 // 배정될 역할
    }

    public int RoundSeed;                       // 이번 판 기준 시드

    public readonly List<PuzzleSpawnPlan> PuzzlePlans = new();              // 퍼즐/힌트 배치 결과
    public readonly List<PlayerAssignmentPlan> PlayerAssignments = new();   // 플레이어 Zone/Role 결과
}
