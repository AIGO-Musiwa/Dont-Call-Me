using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 정답 / 힌트 세트를 생성하는 유틸.
/// 
/// 규칙
/// - 최종 정답은 6자리
/// - 최종 정답 숫자는 1~9 랜덤, 중복 허용
/// - A 힌트 3개, B 힌트 3개 생성
/// - 각 힌트는 6칸 중 정확히 3칸만 공개
/// - 총 9개 A/B 조합 중 오직 1개 조합만 6자리를 완성 가능
/// - 진짜 A/B 힌트만 최종 정답 숫자를 사용
/// - 가짜 힌트의 공개 숫자는 정답 숫자를 재사용하지 않고, 시드 기반 랜덤 숫자로 생성
/// </summary>
public static class FinalCodeAnswerGenerator
{
    public const int FinalCodeLength = 6;          // 최종 정답 길이
    public const int RevealedCountPerHint = 3;     // 힌트 1개당 공개 칸 수
    public const int HintCountPerZone = 3;         // Zone당 힌트 개수

    private const int MaxGenerateAttempts = 2000;  // 생성 최대 재시도 횟수
    private const int MaxMaskAttempts = 200;       // 가짜 마스크 생성 최대 재시도 횟수

    /// <summary>
    /// 최종 생성 결과 전체 데이터.
    /// </summary>
    [Serializable]
    public class FinalCodeAnswerData
    {
        public int[] FinalDigits = new int[FinalCodeLength];   // 최종 정답 6자리
        public List<FinalCodeHintData> ZoneAHints = new();     // A Zone 힌트 3개
        public List<FinalCodeHintData> ZoneBHints = new();     // B Zone 힌트 3개
        public int TrueAHintIndex;                             // 진짜 A 힌트 인덱스
        public int TrueBHintIndex;                             // 진짜 B 힌트 인덱스

        /// <summary>
        /// 최종 정답을 문자열로 반환한다.
        /// </summary>
        public string GetFinalCodeString()
        {
            if (FinalDigits == null || FinalDigits.Length != FinalCodeLength)
                return string.Empty;

            string result = string.Empty;

            for (int i = 0; i < FinalDigits.Length; i++)
                result += FinalDigits[i].ToString();

            return result;
        }
    }

    /// <summary>
    /// seed를 받아 최종 정답과 힌트 세트를 생성한다.
    /// </summary>
    public static FinalCodeAnswerData Generate(int seed)
    {
        SeedRandom rng = new SeedRandom(seed);

        for (int attempt = 0; attempt < MaxGenerateAttempts; attempt++)
        {
            FinalCodeAnswerData result = TryGenerateSingleSet(rng);
            if (result != null)
                return result;
        }

        Debug.LogError("[FinalCodeAnswerGenerator] 유효한 힌트 세트를 생성하지 못했습니다. Fallback 데이터를 사용합니다.");
        return CreateFallbackData(seed);
    }

    /// <summary>
    /// 힌트 세트 1회를 생성 시도한다.
    /// 조건에 맞지 않으면 null을 반환한다.
    /// </summary>
    private static FinalCodeAnswerData TryGenerateSingleSet(SeedRandom rng)
    {
        FinalCodeAnswerData data = new FinalCodeAnswerData();

        // 1. 최종 정답 6자리 생성
        for (int i = 0; i < FinalCodeLength; i++)
            data.FinalDigits[i] = rng.NextInt(1, 10); // 1~9, 중복 허용

        // 2. 진짜 힌트 쌍의 공개 마스크 생성
        bool[] trueAMask = BuildRandomRevealMask(rng);
        bool[] trueBMask = BuildComplementMask(trueAMask);

        // 3. 진짜 힌트 생성
        // 진짜 힌트만 최종 정답 숫자를 사용한다.
        FinalCodeHintData trueAHint = BuildAnswerHint(data.FinalDigits, trueAMask);
        FinalCodeHintData trueBHint = BuildAnswerHint(data.FinalDigits, trueBMask);

        data.ZoneAHints.Add(trueAHint);
        data.ZoneBHints.Add(trueBHint);

        data.TrueAHintIndex = 0;
        data.TrueBHintIndex = 0;

        List<bool[]> zoneAMasks = new List<bool[]> { CopyMask(trueAMask) };
        List<bool[]> zoneBMasks = new List<bool[]> { CopyMask(trueBMask) };

        // 4. 가짜 A 힌트 2개 생성
        // 가짜 A는 trueBMask와 상보가 되면 안 된다.
        for (int i = 1; i < HintCountPerZone; i++)
        {
            bool[] fakeAMask = BuildFakeMask(
                rng,
                sameZoneMasks: zoneAMasks,
                oppositeZoneMasks: zoneBMasks);

            if (fakeAMask == null)
                return null;

            zoneAMasks.Add(CopyMask(fakeAMask));

            // 가짜 힌트 숫자는 정답에서 가져오지 않고 시드 기반 랜덤 숫자로 만든다.
            data.ZoneAHints.Add(BuildRandomDigitHint(fakeAMask, rng));
        }

        // 5. 가짜 B 힌트 2개 생성
        // 가짜 B는 현재 존재하는 모든 A 마스크와 상보가 되면 안 된다.
        for (int i = 1; i < HintCountPerZone; i++)
        {
            bool[] fakeBMask = BuildFakeMask(
                rng,
                sameZoneMasks: zoneBMasks,
                oppositeZoneMasks: zoneAMasks);

            if (fakeBMask == null)
                return null;

            zoneBMasks.Add(CopyMask(fakeBMask));

            // 가짜 힌트 숫자는 정답에서 가져오지 않고 시드 기반 랜덤 숫자로 만든다.
            data.ZoneBHints.Add(BuildRandomDigitHint(fakeBMask, rng));
        }

        // 6. Zone 내부 순서 셔플
        ShuffleHints(rng, data.ZoneAHints, ref data.TrueAHintIndex);
        ShuffleHints(rng, data.ZoneBHints, ref data.TrueBHintIndex);

        // 7. 전체 9조합 중 정확히 1개만 완성 가능한지 검증
        if (!HasExactlyOneValidPair(data))
            return null;

        return data;
    }

    /// <summary>
    /// 공개 칸이 정확히 3개인 랜덤 공개 마스크를 만든다.
    /// </summary>
    private static bool[] BuildRandomRevealMask(SeedRandom rng)
    {
        bool[] revealed = new bool[FinalCodeLength];
        List<int> indices = new List<int>(FinalCodeLength);

        for (int i = 0; i < FinalCodeLength; i++)
            indices.Add(i);

        rng.Shuffle(indices);

        for (int i = 0; i < RevealedCountPerHint; i++)
            revealed[indices[i]] = true;

        return revealed;
    }

    /// <summary>
    /// 가짜 힌트용 공개 마스크를 만든다.
    /// 
    /// 조건
    /// - 공개 칸은 정확히 3개
    /// - 같은 Zone 안에서 기존 마스크와 중복되면 안 됨
    /// - 반대 Zone의 어떤 마스크와도 정확한 상보 관계가 되면 안 됨
    /// </summary>
    private static bool[] BuildFakeMask(
        SeedRandom rng,
        List<bool[]> sameZoneMasks,
        List<bool[]> oppositeZoneMasks)
    {
        for (int attempt = 0; attempt < MaxMaskAttempts; attempt++)
        {
            bool[] candidate = BuildRandomRevealMask(rng);

            if (ContainsSameMask(sameZoneMasks, candidate))
                continue;

            if (IsComplementOfAny(oppositeZoneMasks, candidate))
                continue;

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// 공개 마스크의 정확한 상보 마스크를 만든다.
    /// </summary>
    private static bool[] BuildComplementMask(bool[] sourceMask)
    {
        bool[] complement = new bool[FinalCodeLength];

        for (int i = 0; i < FinalCodeLength; i++)
            complement[i] = !sourceMask[i];

        return complement;
    }

    /// <summary>
    /// 진짜 힌트 생성.
    /// 공개된 칸에는 최종 정답 숫자를 넣는다.
    /// </summary>
    private static FinalCodeHintData BuildAnswerHint(int[] finalDigits, bool[] revealMask)
    {
        FinalCodeHintData hint = new FinalCodeHintData();

        for (int i = 0; i < FinalCodeLength; i++)
            hint.SetCell(i, finalDigits[i], revealMask[i]);

        return hint;
    }

    /// <summary>
    /// 가짜 힌트 생성.
    /// 공개된 칸에는 최종 정답이 아니라 시드 기반 랜덤 숫자를 넣는다.
    /// </summary>
    private static FinalCodeHintData BuildRandomDigitHint(bool[] revealMask, SeedRandom rng)
    {
        FinalCodeHintData hint = new FinalCodeHintData();

        for (int i = 0; i < FinalCodeLength; i++)
        {
            int randomDigit = rng.NextInt(1, 10); // 1~9 랜덤, 중복 허용
            hint.SetCell(i, randomDigit, revealMask[i]);
        }

        return hint;
    }

    /// <summary>
    /// 같은 마스크가 이미 존재하는지 확인한다.
    /// </summary>
    private static bool ContainsSameMask(List<bool[]> masks, bool[] target)
    {
        if (masks == null || target == null)
            return false;

        for (int i = 0; i < masks.Count; i++)
        {
            if (AreMasksSame(masks[i], target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// target이 masks 중 하나와 상보 관계인지 확인한다.
    /// </summary>
    private static bool IsComplementOfAny(List<bool[]> masks, bool[] target)
    {
        if (masks == null || target == null)
            return false;

        for (int i = 0; i < masks.Count; i++)
        {
            if (AreMasksExactComplement(masks[i], target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 두 공개 마스크가 완전히 같은지 검사한다.
    /// </summary>
    private static bool AreMasksSame(bool[] a, bool[] b)
    {
        if (a == null || b == null || a.Length != FinalCodeLength || b.Length != FinalCodeLength)
            return false;

        for (int i = 0; i < FinalCodeLength; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// 두 공개 마스크가 정확한 상보 관계인지 검사한다.
    /// </summary>
    private static bool AreMasksExactComplement(bool[] a, bool[] b)
    {
        if (a == null || b == null || a.Length != FinalCodeLength || b.Length != FinalCodeLength)
            return false;

        for (int i = 0; i < FinalCodeLength; i++)
        {
            if (a[i] == b[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// 마스크 복사본을 만든다.
    /// </summary>
    private static bool[] CopyMask(bool[] source)
    {
        bool[] copy = new bool[FinalCodeLength];

        if (source == null)
            return copy;

        for (int i = 0; i < FinalCodeLength && i < source.Length; i++)
            copy[i] = source[i];

        return copy;
    }

    /// <summary>
    /// 힌트 리스트를 셔플하고, 진짜 힌트 인덱스도 함께 갱신한다.
    /// </summary>
    private static void ShuffleHints(SeedRandom rng, List<FinalCodeHintData> hints, ref int trueIndex)
    {
        for (int i = hints.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.NextInt(0, i + 1);

            if (swapIndex == i)
                continue;

            FinalCodeHintData temp = hints[i];
            hints[i] = hints[swapIndex];
            hints[swapIndex] = temp;

            if (trueIndex == i)
                trueIndex = swapIndex;
            else if (trueIndex == swapIndex)
                trueIndex = i;
        }
    }

    /// <summary>
    /// 전체 9개 조합 중 정확히 1개 조합만 완성 가능한지 검사한다.
    /// 그리고 그 조합이 진짜 힌트 쌍인지도 확인한다.
    /// </summary>
    private static bool HasExactlyOneValidPair(FinalCodeAnswerData data)
    {
        if (data == null || data.ZoneAHints == null || data.ZoneBHints == null)
            return false;

        int validPairCount = 0;
        int validAIndex = -1;
        int validBIndex = -1;

        for (int a = 0; a < data.ZoneAHints.Count; a++)
        {
            for (int b = 0; b < data.ZoneBHints.Count; b++)
            {
                if (!CanCombineIntoFullCode(data.ZoneAHints[a], data.ZoneBHints[b]))
                    continue;

                validPairCount++;
                validAIndex = a;
                validBIndex = b;

                if (validPairCount > 1)
                    return false;
            }
        }

        if (validPairCount != 1)
            return false;

        return validAIndex == data.TrueAHintIndex && validBIndex == data.TrueBHintIndex;
    }

    /// <summary>
    /// 두 힌트를 합쳐 6자리를 완성할 수 있는지 검사한다.
    /// 
    /// 규칙
    /// - 각 칸마다 정확히 한쪽만 공개돼 있어야 한다.
    /// - 둘 다 공개거나 둘 다 비공개면 실패다.
    /// </summary>
    public static bool CanCombineIntoFullCode(FinalCodeHintData aHint, FinalCodeHintData bHint)
    {
        if (aHint == null || bHint == null)
            return false;

        for (int i = 0; i < FinalCodeLength; i++)
        {
            bool aRevealed = aHint.IsRevealed(i);
            bool bRevealed = bHint.IsRevealed(i);

            if (aRevealed == bRevealed)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 두 힌트를 합쳐 최종 6자리 숫자를 복원한다.
    /// 
    /// 주의
    /// - 반드시 CanCombineIntoFullCode()가 true인 조합에만 호출해야 한다.
    /// </summary>
    public static int[] BuildCombinedCode(FinalCodeHintData aHint, FinalCodeHintData bHint)
    {
        int[] result = new int[FinalCodeLength];

        for (int i = 0; i < FinalCodeLength; i++)
        {
            if (aHint.IsRevealed(i))
                result[i] = aHint.GetDigit(i);
            else
                result[i] = bHint.GetDigit(i);
        }

        return result;
    }

    /// <summary>
    /// 비상용 fallback 데이터를 생성한다.
    /// 정상 생성 실패 시에만 사용된다.
    /// </summary>
    private static FinalCodeAnswerData CreateFallbackData(int seed)
    {
        SeedRandom rng = new SeedRandom(seed + 9999);

        FinalCodeAnswerData data = new FinalCodeAnswerData();

        for (int i = 0; i < FinalCodeLength; i++)
            data.FinalDigits[i] = rng.NextInt(1, 10);

        bool[] trueAMask = new bool[FinalCodeLength] { true, false, true, false, true, false };
        bool[] trueBMask = BuildComplementMask(trueAMask);

        bool[] fakeAMask1 = new bool[FinalCodeLength] { true, true, true, false, false, false };
        bool[] fakeAMask2 = new bool[FinalCodeLength] { false, false, true, true, true, false };

        bool[] fakeBMask1 = new bool[FinalCodeLength] { true, false, false, true, true, false };
        bool[] fakeBMask2 = new bool[FinalCodeLength] { false, true, true, false, false, true };

        data.ZoneAHints.Add(BuildAnswerHint(data.FinalDigits, trueAMask));
        data.ZoneAHints.Add(BuildRandomDigitHint(fakeAMask1, rng));
        data.ZoneAHints.Add(BuildRandomDigitHint(fakeAMask2, rng));

        data.ZoneBHints.Add(BuildAnswerHint(data.FinalDigits, trueBMask));
        data.ZoneBHints.Add(BuildRandomDigitHint(fakeBMask1, rng));
        data.ZoneBHints.Add(BuildRandomDigitHint(fakeBMask2, rng));

        data.TrueAHintIndex = 0;
        data.TrueBHintIndex = 0;

        if (!HasExactlyOneValidPair(data))
            Debug.LogError("[FinalCodeAnswerGenerator] Fallback 데이터 검증 실패");

        return data;
    }
}