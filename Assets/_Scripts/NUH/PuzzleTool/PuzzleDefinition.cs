using Fusion;
using UnityEngine;

/// <summary>
/// 퍼즐 1종에 대한 정의 데이터
/// 퍼즐 본체와 반대편 힌트를 한 세트로 관리
/// Fusion 네트워크 스폰용 프리팹 참조를 가진다
/// </summary>
[CreateAssetMenu(fileName = "PuzzleDefinition", menuName = "Puzzle/Puzzle Definition")]
public class PuzzleDefinition : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string puzzleId;                 // 퍼즐 고유 ID
    [SerializeField] private PuzzleStage puzzleStage;         // 퍼즐 단계
    [SerializeField] private NetworkObject puzzlePrefab;      // 퍼즐 본체 네트워크 프리팹
    [SerializeField] private NetworkObject hintPrefab;        // 반대편 건물에 배치될 힌트 네트워크 프리팹

    [Header("퍼즐 배치 보정")]
    [SerializeField] private Vector3 puzzlePositionOffset;    // 슬롯 기준 퍼즐 로컬 위치 오프셋
    [SerializeField] private Vector3 puzzleRotationOffset;    // 슬롯 기준 퍼즐 로컬 회전 오프셋

    [Header("힌트 배치 보정")]
    [SerializeField] private Vector3 hintPositionOffset;      // 슬롯 기준 힌트 로컬 위치 오프셋
    [SerializeField] private Vector3 hintRotationOffset;      // 슬롯 기준 힌트 로컬 회전 오프셋

    public string PuzzleId => puzzleId;
    public PuzzleStage Stage => puzzleStage;
    public NetworkObject PuzzlePrefab => puzzlePrefab;
    public NetworkObject HintPrefab => hintPrefab;

    public Vector3 PuzzlePositionOffset => puzzlePositionOffset;
    public Vector3 PuzzleRotationOffset => puzzleRotationOffset;
    public Vector3 HintPositionOffset => hintPositionOffset;
    public Vector3 HintRotationOffset => hintRotationOffset;
}