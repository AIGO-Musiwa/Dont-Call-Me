using UnityEngine;

/// <summary>
/// 퍼즐 1종에 대한 정의 데이터
/// 퍼즐 본체와 반대편 힌트를 한 세트로 관리
/// </summary>
[CreateAssetMenu(fileName = "PuzzleDefinition", menuName = "Puzzle/Puzzle Definition")]
public class PuzzleDefinition : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string puzzleId;               // 퍼즐 고유 Id
    [SerializeField] private PuzzleStage puzzleStage;       // 퍼즐 단계
    [SerializeField] private GameObject puzzlePrefab;       // 퍼즐 본체 프리팹
    [SerializeField] private GameObject hintPrefab;         // 반대편 건물에 배치될 힌트 프리팹

    public string PuzzleId => puzzleId;
    public PuzzleStage Stage => puzzleStage;
    public GameObject PuzzlePrefab => puzzlePrefab;
    public GameObject HintPrefab => hintPrefab;
}
