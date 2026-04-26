using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 퍼즐 1종에 대한 정의 데이터
/// 퍼즐 본체 1개 + 반대편 힌트 여러 개를 한 세트로 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "PuzzleDefinition", menuName = "Puzzle/Puzzle Definition")]
public class PuzzleDefinition : ScriptableObject
{
    [System.Serializable]
    public class HintDefinition
    {
        [Header("기본 정보")]
        [SerializeField] private string hintId;              // 힌트 고유 식별용 ID
        [SerializeField] private NetworkObject hintPrefab;   // 실제 스폰할 힌트 프리팹

        [Header("힌트 배치 보정")]
        [SerializeField] private Vector3 hintPositionOffset; // 힌트 슬롯 기준 로컬 위치 보정값
        [SerializeField] private Vector3 hintRotationOffset; // 힌트 슬롯 기준 로컬 회전 보정값

        public string HintId => hintId;                         // 힌트 ID 외부 읽기용
        public NetworkObject HintPrefab => hintPrefab;          // 힌트 프리팹 외부 읽기용
        public Vector3 HintPositionOffset => hintPositionOffset; // 힌트 위치 오프셋 외부 읽기용
        public Vector3 HintRotationOffset => hintRotationOffset; // 힌트 회전 오프셋 외부 읽기용
    }

    [Header("기본 정보")]
    [SerializeField] private string puzzleId;                     // 퍼즐 고유 ID
    [SerializeField] private PuzzleStage puzzleStage;             // 이 퍼즐이 속한 단계
    [SerializeField] private NetworkObject puzzlePrefab;          // 퍼즐 본체 프리팹
    [SerializeField] private List<HintDefinition> hintDefinitions = new(); // 연결된 힌트 정의 목록

    [Header("퍼즐 배치 보정")]
    [SerializeField] private Vector3 puzzlePositionOffset;        // 퍼즐 슬롯 기준 로컬 위치 보정값
    [SerializeField] private Vector3 puzzleRotationOffset;        // 퍼즐 슬롯 기준 로컬 회전 보정값

    public string PuzzleId => puzzleId;                           // 퍼즐 ID 외부 읽기용
    public PuzzleStage Stage => puzzleStage;                      // 퍼즐 단계 외부 읽기용
    public NetworkObject PuzzlePrefab => puzzlePrefab;            // 퍼즐 본체 프리팹 외부 읽기용
    public IReadOnlyList<HintDefinition> HintDefinitions => hintDefinitions; // 힌트 목록 읽기 전용 반환

    public Vector3 PuzzlePositionOffset => puzzlePositionOffset;  // 퍼즐 위치 보정값 외부 읽기용
    public Vector3 PuzzleRotationOffset => puzzleRotationOffset;  // 퍼즐 회전 보정값 외부 읽기용

    /// <summary>
    /// 현재 퍼즐 정의에 연결된 힌트 개수 반환
    /// </summary>
    public int HintCount => hintDefinitions != null ? hintDefinitions.Count : 0; // 힌트 총 개수 반환
}