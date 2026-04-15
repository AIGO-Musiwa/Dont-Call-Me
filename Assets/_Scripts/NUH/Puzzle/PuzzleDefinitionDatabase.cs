using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퍼즐 정의들을 모아두는 데이터베이스
/// 단계별 후보 풀을 제공
/// </summary>
[CreateAssetMenu(fileName = "PuzzleDefinitionDatabase", menuName = "Puzzle/Puzzle Definition Database")]
public class PuzzleDefinitionDatabase : ScriptableObject
{
    [SerializeField] private List<PuzzleDefinition> def = new();        //전체 퍼즐 정의 목록

    public List<PuzzleDefinition> GetDefinitionsByStage(PuzzleStage stage)
    {
        List<PuzzleDefinition> result = new();

        for(int i = 0; i < def.Count; i++)
        {
            PuzzleDefinition definition = def[i];

            if (definition == null)
                continue;

            if (definition.Stage != stage)
                continue;

            result.Add(definition);
        }

        return result;
    }
}
