using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 힌트 순서표 표시 담당
/// 같은 seed로 퍼즐 본체와 동일한 RNG 순서를 따라가서
/// "정답 순서"를 재구성해 6칸에 문양 스프라이트를 표시한다.
/// </summary>
public class SymbolLeverHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int leverCount = 6;          // 순서표 칸 수
    [SerializeField] private int totalSymbolCount = 10;   // 전체 문양 종류 수

    [Header("표시 슬롯")]
    [SerializeField] private List<SymbolSpriteDisplay> hintSlots = new(); // 순서표 6칸

    [Header("문양 스프라이트 풀")]
    [SerializeField] private List<Sprite> symbolSprites = new(); // 문양 ID와 대응되는 스프라이트 목록

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _selectedSymbolIds = new();
    private readonly List<int> _answerSequence = new();

    public void ApplyAnswerSeed(int seed)
    {
        _selectedSymbolIds.Clear();
        _answerSequence.Clear();

        SeedRandom rng = new SeedRandom(seed);

        // 1. 전체 문양 풀 생성
        List<int> allSymbolIds = BuildAllSymbolIds();

        // 2. 이번 판에 사용할 6개 문양 선택
        List<int> selected = rng.PickUnique(allSymbolIds, leverCount);
        _selectedSymbolIds.AddRange(selected);

        // 3. 퍼즐 본체와 동일하게 "레버 배치용 셔플"을 먼저 한 번 소비
        //    힌트에서는 직접 쓰지 않지만 RNG 순서를 맞추기 위해 반드시 필요
        List<int> leverPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(leverPlacement);

        // 4. 퍼즐 본체와 동일하게 다시 셔플해서 "정답 순서" 생성
        List<int> answerPlacement = new List<int>(_selectedSymbolIds);
        rng.Shuffle(answerPlacement);
        _answerSequence.AddRange(answerPlacement);

        ApplyHintVisuals();
        Log($"힌트 시드 적용 완료 | seed={seed}");
        LogSequenceDebug();
    }

    private void ApplyHintVisuals()
    {
        int count = Mathf.Min(hintSlots.Count, _answerSequence.Count);

        for (int i = 0; i < hintSlots.Count; i++)
        {
            if (hintSlots[i] == null)
                continue;

            if (i >= count)
            {
                hintSlots[i].Clear();
                continue;
            }

            int symbolId = _answerSequence[i];
            Sprite sprite = GetSymbolSprite(symbolId);
            hintSlots[i].SetSprite(sprite);
        }
    }

    private List<int> BuildAllSymbolIds()
    {
        List<int> ids = new List<int>(totalSymbolCount);

        for (int i = 0; i < totalSymbolCount; i++)
            ids.Add(i);

        return ids;
    }

    private Sprite GetSymbolSprite(int symbolId)
    {
        if (symbolId < 0 || symbolId >= symbolSprites.Count)
            return null;

        return symbolSprites[symbolId];
    }

    private void LogSequenceDebug()
    {
        if (!enableDebugLog)
            return;

        string answerSymbols = string.Join(", ", _answerSequence);
        Debug.Log($"[SymbolLeverHint] 힌트 정답 순서 문양 IDs = [{answerSymbols}]", this);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[SymbolLeverHint] {message}", this);
    }
}
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// 문양 레버 퍼즐 힌트
///// 같은 정답 시드를 받아 퍼즐과 동일한 정답 상태를 재생성
///// </summary>
//public class SymbolLeverHint : MonoBehaviour, IPuzzleSeedReceiver
//{
//    [Header("힌트 표시 데이터")]
//    [SerializeField] private int leverCount = 6;                         // 순서칸 수
//    [SerializeField] private int totalSymbolCount = 10;                 // 전체 문양 종류 수


//    [Header("표시 슬롯")]
//    [SerializeField] private List<SymbolSpriteDisplay> hintSlots = new();       //순서표 6칸

//    [Header("문양 스프라이트 풀")]
//    [SerializeField] private List<Sprite> symbolSprites = new();        //문양 ID와 대응되는 스프라이트 목록

//    [Header("디버그")]
//    [SerializeField] private bool enableDebugLog = true;                // 디버그 로그 출력 여부

//    private readonly List<int> _selectedSymbolIds = new();
//    private readonly List<int> _answerSequence = new();

//    /// <summary>
//    /// 퍼즐과 동일한 정답 시드를 받아 같은 힌트 상태를 만듦
//    /// false = 위 / true = 아래
//    /// </summary>
//    public void ApplyAnswerSeed(int seed)
//    {
//        _selectedSymbolIds.Clear();
//        _answerSequence.Clear();

//        SeedRandom rng = new SeedRandom(seed);

//        List<int> allSymbolIds = BuildAllSymbolIds();
//        List<int> selected = rng.PickUnique(allSymbolIds, leverCount);

//        _selectedSymbolIds.AddRange(selected);

//        List<int> shuffledAnswer = new List<int>(_selectedSymbolIds);
//        rng.Shuffle(shuffledAnswer);
//        _answerSequence.AddRange(shuffledAnswer);

//        ApplyHintVisuals();
//        Log($"힌트 시드 적용 완료 | seed = {seed}");
//    }

//    private void ApplyHintVisuals()
//    {
//        int count = Mathf.Min(hintSlots.Count, _answerSequence.Count);

//        for (int i = 0; i < hintSlots.Count; i++)
//        {
//            if (hintSlots[i] == null)
//                continue;

//            if (i > count)
//            {
//                hintSlots[i].Clear();
//                continue;
//            }

//            int symbolId = _answerSequence[i];
//            Sprite sprite = GetSymbolSprite(symbolId);
//            hintSlots[i].SetSprite(sprite);
//        }
//    }

//    private List<int> BuildAllSymbolIds()
//    {
//        List<int> ids = new List<int>(totalSymbolCount);

//        for (int i = 0; i < totalSymbolCount; i++)
//            ids.Add(i);

//        return ids;
//    }

//    private Sprite GetSymbolSprite(int symbolId)
//    {
//        if (symbolId < 0 || symbolId >= symbolSprites.Count)
//            return null;

//        return symbolSprites[symbolId];
//    }


//    private void Log(string m)
//    {
//        if (!enableDebugLog)
//            return;

//        Debug.Log($"[SymbolLeverHint] {m}", this);
//    }
//}
