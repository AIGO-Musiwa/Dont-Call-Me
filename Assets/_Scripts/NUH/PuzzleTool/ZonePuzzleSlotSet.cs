using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 존별 퍼즐/힌트 배치 슬롯 묶음
/// 단계별로 퍼즐 슬롯과 힌트 슬롯을 나눠서 보관한다.
/// </summary>
public class ZonePuzzleSlotSet : MonoBehaviour
{
    [Header("건물 이름")]
    [SerializeField] private Zone zone; // 이 슬롯셋이 속한 존 정보

    [Header("1단계 퍼즐 슬롯")]
    [SerializeField] private List<PuzzlePlacementSlot> stage1PuzzleSlots = new(); // 1단계 퍼즐 본체 슬롯 목록

    [Header("1단계 힌트 슬롯")]
    [SerializeField] private List<HintPlacementSlot> stage1HintSlots = new(); // 1단계 힌트 슬롯 목록

    [Header("2단계 퍼즐 슬롯")]
    [SerializeField] private List<PuzzlePlacementSlot> stage2PuzzleSlots = new(); // 2단계 퍼즐 본체 슬롯 목록

    [Header("2단계 힌트 슬롯")]
    [SerializeField] private List<HintPlacementSlot> stage2HintSlots = new(); // 2단계 힌트 슬롯 목록

    [Header("3단계 퍼즐 슬롯")]
    [SerializeField] private List<PuzzlePlacementSlot> stage3PuzzleSlots = new(); // 3단계 퍼즐 본체 슬롯 목록

    [Header("3단계 힌트 슬롯")]
    [SerializeField] private List<HintPlacementSlot> stage3HintSlots = new(); // 3단계 힌트 슬롯 목록

    public Zone Zone => zone;                                 // 현재 슬롯셋 존 정보 외부 읽기용
    public List<PuzzlePlacementSlot> Stage1PuzzleSlots => stage1PuzzleSlots; // 1단계 퍼즐 슬롯 목록 반환
    public List<HintPlacementSlot> Stage1HintSlots => stage1HintSlots;       // 1단계 힌트 슬롯 목록 반환
    public List<PuzzlePlacementSlot> Stage2PuzzleSlots => stage2PuzzleSlots; // 2단계 퍼즐 슬롯 목록 반환
    public List<HintPlacementSlot> Stage2HintSlots => stage2HintSlots;       // 2단계 힌트 슬롯 목록 반환
    public List<PuzzlePlacementSlot> Stage3PuzzleSlots => stage3PuzzleSlots; // 3단계 퍼즐 슬롯 목록 반환
    public List<HintPlacementSlot> Stage3HintSlots => stage3HintSlots;       // 3단계 힌트 슬롯 목록 반환
}