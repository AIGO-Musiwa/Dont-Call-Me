using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-1 숫자 입력 퍼즐 정답 데이터 생성 유틸
/// 같은 seed를 받은 퍼즐 본체 / 모니터 힌트 / 반대편 월드 힌트 세트가
/// 완전히 같은 결과를 재구성할 수 있도록 사용
/// </summary>
public static class NumericCodeAnswerGenerator
{
    /// <summary>
    /// 퍼즐 한 판에 필요한 모든 정답 데이터를 담는 컨테이너
    /// 퍼즐 본체 / 모니터 / 월드 힌트 세트가 공용으로 사용
    /// </summary>
    [Serializable]
    public class NumericCodeAnswerData 
    {
        public readonly List<int> FinalDigits = new();                              // 최종 4자리 정답
        public readonly List<NumericHintType> HintOrder = new();                    // 상단 힌트 sprite 순서
        public readonly List<NumericBookColor> BookPlacement = new();               // 20권 책 각각의 색 배치
        public readonly Dictionary<NumericBookColor, int> BookColorCounts = new();  // 색별 책 개수

        public NumericBookColor TargetBookColor;    // 어떤 색의 책을 몇개 세야 하는지
        public int ClockHour;                       // 시계 숫자 
        public int OpenedDrawerCount;               // 열린 서랍 수
        public int MarkedFrameCount;                // X 액자 수 

        /// <summary>
        /// 최종 4자리 숫자를 문자열로 반환
        /// 디버그 로그 확인용
        /// </summary>
        public string GetFinalCodeString()
        {
            return string.Join(string.Empty, FinalDigits);
        }
    }

    private const int TotalDigidCount = 4;      // 최종 입력 자릿수
    private const int TotalBookCount = 20;      // 총 책 수
    private const int TotalFrameCount = 9;      // 총 액자 수
    private const int MinSingleDigit = 1;       // 한 자리 숫자 최소값
    private const int MaxSingleDigit = 9;       // 한 자리 숫자 최대값

    /// <summary>
    /// seed 로 이번 퍼즐 전체 정답 데이터 생성
    /// </summary>
    public static NumericCodeAnswerData Generate(int seed)
    {
        SeedRandom rng = new SeedRandom(seed);
        NumericCodeAnswerData data = new NumericCodeAnswerData();

        // 1. 모니터 상단에 배치될 힌트 순서를 결정
        BuildHintOrder(rng, data);

        // 2. 각 힌트 오브젝트가 가질 실제 숫자값 결정
        data.ClockHour = BuildClockHour(rng);
        data.OpenedDrawerCount = BuildOpenedDrawerCount(rng);
        data.MarkedFrameCount = BuildMarkedFrameCount(rng);

        // 3. 책 관련 데이터 결정
        data.TargetBookColor = BuildTargetBookColor(rng);
        BuildBookData(rng, data);

        // 4. 힌트 순서에 맞춰 최종 4자리 정답 구성
        ComposeFinalDigits(data);

        return data;
    }

    /// <summary>
    /// 힌트 4종의 순서를 seed 기반으로 섞어 결정
    /// </summary>
    private static void BuildHintOrder(SeedRandom rng, NumericCodeAnswerData data)
    {
        List<NumericHintType> order = new list<NumericHintType>
        {
            NumericHintType.Clock,
            NumericHintType.Drawer,
            NumericHintType.Book,
            NumericHintType.Frame
        };

        rng.Shuffle(order);
        data.HintOrder.AddRange(order);
    }

    /// <summary>
    /// 시계 숫자 생성
    /// 1~9 정각만 사용
    /// </summary>
    private static int BuildClockHour(SeedRandom rng)
    {
        return rng.NextInt(MinSingleDigit, MaxSingleDigit + 1);
    }

    /// <summary>
    /// 열린 서랍 수 생성
    /// </summary>
    private static int BuildOpenedDrawerCount(SeedRandom rng)
    {
        return rng.NextInt(MinSingleDigit, MaxSingleDigit + 1);
    }

    /// <summary>
    /// X 표시 액자 수 생성
    /// 액자 총 수는  9개이므로 1~9 범위 안에서 생성
    /// </summary>
    private static int BuildMarkedFrameCount(SeedRandom rng)
    {
        return rng.NextInt(MinSingleDigit, TotalFrameCount + 1);
    }

    /// <summary>
    /// 세야 할 책 색을 하나 고르기
    /// </summary>
    private static NumericBookColor BuildTargetBookColor(SeedRandom rng)
    {
        int colorIndex = rng.NextInt(0, Enum.GetValues(typeof(NumericBookColor)).Length);
        return (NumericBookColor)colorIndex;
    }

    /// <summary>
    /// 책 20권의 색 분포와 실제 배치 순서를 만들기
    /// 규칙:
    /// - 총 20권
    /// - 4색
    /// - 각 색 최소 1권 이상
    /// - 각 색 최대 9권 이하
    /// - TargetBookColor의 개수가 정답 숫자가 됨
    /// </summary>
    private static void BuildBookData(SeedRandom rng, NumericCodeAnswerData data)
    {
        data.BookColorCounts.Clear();
        data.BookPlacement.Clear();

        NumericBookColor[] allColors =
        {
            NumericBookColor.Red,
            NumericBookColor.Green,
            NumericBookColor.Blue,
            NumericBookColor.Yellow
        };


        // 타겟 색 개수를 먼저 한 자리 수로 정하기
        int targetCount = rng.NextInt(MinSingleDigit, MaxSingleDigit + 1);

        // 나머지 3색에 최소 1권씩 배정한 뒤, 남은 수 분배
        Dictionary<NumericBookColor, int> counts = new Dictionary<NumericBookColor, int>();

        for (int i = 0; i < allColors.Length; i++)
            counts[allColors[i]] = 1;

        // 타겟 색은 원하는 숫자로 덮어쓴다
        counts[data.TargetBookColor] = targetCount;

        // 현재까지 배정된 총합 계산
        int currentTotal = 0;
        for (int i = 0; i < allColors.Length; i++)
            currentTotal += counts[allColors[i]];

        // 남은 책 수 계산
        int remain = TotalBookCount - currentTotal;

        // 타겟이 아닌 3색 목록
        List<NumericBookColor> otherColors = new List<NumericBookColor>();
        for (int i = 0; i < allColors.Length; i++)
        {
            if (allColors[i] == data.TargetBookColor)
                continue;

            otherColors.Add(allColors[i]);
        }

        // 남은 책을 3색에 나눠 담기
        // 각 색 최대 9권 넘지 않게 하기
        while (remain > 0)
        {
            List<NumericBookColor> candidates = new List<NumericBookColor>();

            for (int i = 0; i < otherColors.Count; i++)
            {
                NumericBookColor color = otherColors[i];
                if (counts[color] < MaxSingleDigit)
                    candidates.Add(color);
            }

            // 보험용
            if (candidates.Count == 0)
                break;

            int pickIndex = rng.NextInt(0, candidates.Count);
            NumericBookColor picked = candidates[pickIndex];
            counts[picked] += 1;
            remain -= 1;
        }

        // counts 결과를 최종 데이터에 복사
        for (int i = 0; i < allColors.Length; i++)
        {
            NumericBookColor color = allColors[i];
            data.BookColorCounts[color] = counts[color];
        }

        // 책 20권의 실제 색 배치를 만들기
        // 매터리얼만 갈아끼우므로 20칸짜리 색 리스트 만들어서 섞기
        List<NumericBookColor> placement = new List<NumericBookColor>(TotalBookCount);

        for (int i = 0; i < allColors.Length; i++)
        {
            NumericBookColor color = allColors[i];
            int count = counts[color];

            for (int j = 0; j < count; j++)
                placement.Add(color);
        }
        rng.Shuffle(placement);
        data.BookPlacement.AddRange(placement);
    }

    /// <summary>
    /// 힌트 순서에 맞춰 최종 4자리 숫자 조합하기
    /// </summary>
    private static void ComposeFinalDigits(NumericCodeAnswerData data)
    {
        data.FinalDigits.Clear();

        for(int i = 0; i < data.HintOrder.Count; i++)
        {
            NumericHintType hintType = data.HintOrder[i];
            int digit = hintType switch
            {
                NumericHintType.Clock => data.ClockHour,
                NumericHintType.Drawer => data.OpenedDrawerCount,
                NumericHintType.Book => data.BookColorCounts[data.TargetBookColor],
                NumericHintType.Frame => data.MarkedFrameCount,
                _ => 0
            };

            data.FinalDigits.Add(digit);
        }
    }
}
