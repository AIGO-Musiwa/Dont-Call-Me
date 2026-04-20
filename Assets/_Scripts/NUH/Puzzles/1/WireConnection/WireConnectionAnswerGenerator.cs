using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전선 연결 퍼즐 정답 생성 전용 유틸
/// - 좌측 색 6개
/// - 우측 색 6개
/// - 좌측 -> 우측 정답 매핑 1:1
/// 힌트 표시 순서 랜덤
/// </summary>
public static class WireConnectionAnswerGenerator
{
    public class Result 
    {
        public List<WireSocketColor> LeftColors = new();        // 좌측 6개 색
        public List<WireSocketColor > RightColors = new();      // 우측 6개 색
        public List<int> CorrectRightIndexByLeft = new();       // 왼쪽 인덱스 -> 오른쪽 인덱스 연결용
        public List<int> HintOrderLeftIndices = new();          // 힌트 표시 순서용 left인덱스
    }

    public static Result Generate(int seed, int socketCount)
    {
        SeedRandom rng = new SeedRandom(seed);
        Result result = new Result();

        List<WireSocketColor> allColors = BuildAllColors();

        // 좌측 색 6개 고유 선택
        List<WireSocketColor> leftPool = new List<WireSocketColor>(allColors);
        rng.Shuffle(leftPool);
        for (int i = 0; i < socketCount; i++)
        {
            result.LeftColors.Add(leftPool[i]);
        }

        // 우측 색 6개 고유 선택
        List<WireSocketColor> rightPool = new List<WireSocketColor>(allColors);
        rng.Shuffle(rightPool);
        for(int i  = 0; i < socketCount; i++)
        {
            result.RightColors.Add(rightPool[i]);
        }

        // Left -> Right 정답 매핑 1:1 생성
        List<int> rightIndices = new List<int>();
        for (int i = 0; i < socketCount; i++)
        {
            rightIndices.Add(i);
        }

        rng.Shuffle(rightIndices);
        result.CorrectRightIndexByLeft.AddRange(rightIndices);

        //힌트 순서 랜덤
        List<int> hintOrder = new List<int>();
        for (int i = 0; i < socketCount; i++)
        {
            hintOrder.Add(i);
        }

        rng.Shuffle(hintOrder);
        result.HintOrderLeftIndices.AddRange(hintOrder);

        return result;
    }

    private static List<WireSocketColor> BuildAllColors()
    {
        return new List<WireSocketColor>
        {
            WireSocketColor.Red,
            WireSocketColor.Orange,
            WireSocketColor.Yellow,
            WireSocketColor.Green,
            WireSocketColor.Blue,
            WireSocketColor.Navy,
            WireSocketColor.Purple,
            WireSocketColor.White,
            WireSocketColor.Black
        };
    }

}

