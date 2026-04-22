using System.Collections.Generic;
using UnityEngine;

public class ZonePuzzleSlotSet : MonoBehaviour
{
    [Header("건물 이름")]
    [SerializeField] private Zone zone;       // 디버그용 건물 이름

    [Header("1단계 퍼즐 슬롯")]
    [SerializeField] private List<PuzzlePlacementSlot> stage1PuzzleSlots = new();

    [Header("1단계 힌트 슬롯")]
    [SerializeField] private List<HintPlacementSlot> stage1HintSlots = new();

    [Header("2단계 퍼즐 슬롯")]
    [SerializeField] private List<PuzzlePlacementSlot> stage2PuzzleSlots = new();

    [Header("2단계 힌트 슬롯")]
    [SerializeField] private List<HintPlacementSlot> stage2HintSlots = new();

    [Header("3단계 퍼즐 슬롯")]
    [SerializeField] private List<PuzzlePlacementSlot> stage3PuzzleSlots = new();

    [Header("3단계 힌트 슬롯")]
    [SerializeField] private List<HintPlacementSlot> stage3HintSlots = new();

    public Zone Zone => zone;
    public List<PuzzlePlacementSlot> Stage1PuzzleSlots => stage1PuzzleSlots;
    public List<HintPlacementSlot> Stage1HintSlots => stage1HintSlots;
    public List<PuzzlePlacementSlot> Stage2PuzzleSlots => stage2PuzzleSlots;
    public List<HintPlacementSlot> Stage2HintSlots => stage2HintSlots;
    public List<PuzzlePlacementSlot> Stage3PuzzleSlots => stage3PuzzleSlots;
    public List<HintPlacementSlot> Stage3HintSlots => stage3HintSlots;
}
