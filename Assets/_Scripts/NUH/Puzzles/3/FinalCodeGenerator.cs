using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 정답 / 힌트 세트를 생성하는 유틸.
/// 
/// 규칙
/// - 최종 정답은 6자리
/// - 각 자리는 0~9 완전 랜덤
/// - 숫자 중복 허용
/// - A 힌트 3개, B 힌트 3개 생성
/// - 총 9개 조합 중 오직 1개 조합만 완성 가능해야 한다
/// </summary>
public static class FinalCodeAnswerGenerator
{
    public const int FinalCodeLength = 6;          // 최종 정답 길이
    public const int RevealedCountPerHint = 3;     // 힌트 1개당 공개 칸 수
    public const int HintCountPerZone = 3;         // Zone당 힌트 개수
    private const int MaxGenerateAttempts = 2000;  // 생성 최대 재시도 횟수

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

            string result = string.Empty; // 최종 문자열 누적

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
        SeedRandom rng = new SeedRandom(seed); // 시드 기반 랜덤 생성기

        for (int attempt = 0; attempt < MaxGenerateAttempts; attempt++)
        {
            FinalCodeAnswerData result = TryGenerateSingleSet(rng); // 한 번 생성 시도
            if (result != null)
                return result;
        }

        Debug.LogError("[FinalCodeAnswerGenerator] 유효한 힌트 세트를 생성하지 못했습니다.");
        return CreateFallbackData(seed); // 방어용 fallback 반환
    }

    /// <summary>
    /// 힌트 세트 1회를 생성 시도한다.
    /// 조건에 맞지 않으면 null을 반환한다.
    /// </summary>
    private static FinalCodeAnswerData TryGenerateSingleSet(SeedRandom rng)
    {
        FinalCodeAnswerData data = new FinalCodeAnswerData(); // 결과 컨테이너 생성

        // 1. 최종 정답 6자리 생성
        for (int i = 0; i < FinalCodeLength; i++)
            data.FinalDigits[i] = rng.NextInt(1, 10); // 1~9, 중복 허용

        // 2. 진짜 힌트 쌍의 공개 마스크 생성
        bool[] trueAMask = BuildRandomRevealMask(rng);     // A 진짜 힌트 공개 패턴
        bool[] trueBMask = BuildComplementMask(trueAMask); // B 진짜 힌트는 정확한 상보 패턴

        // 3. 진짜 힌트 생성
        FinalCodeHintData trueAHint = BuildHint(data.FinalDigits, trueAMask); // 진짜 A 힌트
        FinalCodeHintData trueBHint = BuildHint(data.FinalDigits, trueBMask); // 진짜 B 힌트

        data.ZoneAHints.Add(trueAHint); // A 첫 슬롯에 진짜 힌트 임시 배치
        data.ZoneBHints.Add(trueBHint); // B 첫 슬롯에 진짜 힌트 임시 배치
        data.TrueAHintIndex = 0;        // 진짜 A 인덱스 초기값
        data.TrueBHintIndex = 0;        // 진짜 B 인덱스 초기값

        // 4. 가짜 A 힌트 2개 생성
        for (int i = 1; i < HintCountPerZone; i++)
            data.ZoneAHints.Add(BuildFakeHint(data.FinalDigits, trueBMask, rng));

        // 5. 가짜 B 힌트 2개 생성
        for (int i = 1; i < HintCountPerZone; i++)
            data.ZoneBHints.Add(BuildFakeHint(data.FinalDigits, trueAMask, rng));

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
        bool[] revealed = new bool[FinalCodeLength];    // 공개 여부 배열
        List<int> indices = new List<int>(FinalCodeLength); // 인덱스 후보

        for (int i = 0; i < FinalCodeLength; i++)
            indices.Add(i);

        rng.Shuffle(indices); // 공개 위치 섞기

        for (int i = 0; i < RevealedCountPerHint; i++)
            revealed[indices[i]] = true; // 3칸만 공개

        return revealed;
    }

    /// <summary>
    /// 공개 마스크의 정확한 상보 마스크를 만든다.
    /// </summary>
    private static bool[] BuildComplementMask(bool[] sourceMask)
    {
        bool[] complement = new bool[FinalCodeLength]; // 상보 결과

        for (int i = 0; i < FinalCodeLength; i++)
            complement[i] = !sourceMask[i];

        return complement;
    }

    /// <summary>
    /// 최종 정답과 공개 마스크를 기반으로 힌트 1개를 만든다.
    /// </summary>
    private static FinalCodeHintData BuildHint(int[] finalDigits, bool[] revealMask)
    {
        FinalCodeHintData hint = new FinalCodeHintData(); // 새 힌트 생성

        for (int i = 0; i < FinalCodeLength; i++)
            hint.SetCell(i, finalDigits[i], revealMask[i]);

        return hint;
    }

    /// <summary>
    /// 진짜 상대 힌트와 절대 완성되지 않도록 가짜 힌트를 만든다.
    /// 
    /// 조건
    /// - 공개 칸 수는 3개 유지
    /// - 진짜 상대 힌트의 공개 마스크와 정확한 상보가 되면 안 된다
    /// </summary>
    private static FinalCodeHintData BuildFakeHint(int[] finalDigits, bool[] trueCounterpartMask, SeedRandom rng)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            bool[] fakeMask = BuildRandomRevealMask(rng); // 후보 마스크 생성

            if (AreMasksExactComplement(fakeMask, trueCounterpartMask))
                continue; // 진짜 상대 힌트와 완전 상보면 안 됨

            return BuildHint(finalDigits, fakeMask);
        }

        // fallback: 일부러 완성 불가능한 구조 생성
        bool[] fallbackMask = BuildComplementMask(trueCounterpartMask); // 일단 상보 생성
        fallbackMask[0] = !fallbackMask[0]; // 한 칸 뒤집기
        fallbackMask[1] = !fallbackMask[1]; // 공개 수 보정용 한 칸 더 뒤집기
        return BuildHint(finalDigits, NormalizeRevealCount(fallbackMask, rng));
    }

    /// <summary>
    /// 공개 칸 수를 정확히 3개로 맞춘다.
    /// </summary>
    private static bool[] NormalizeRevealCount(bool[] sourceMask, SeedRandom rng)
    {
        bool[] result = new bool[FinalCodeLength]; // 결과 마스크
        List<int> revealedIndices = new List<int>(); // 공개 칸 목록
        List<int> hiddenIndices = new List<int>();   // 비공개 칸 목록

        for (int i = 0; i < FinalCodeLength; i++)
        {
            result[i] = sourceMask[i];

            if (result[i])
                revealedIndices.Add(i);
            else
                hiddenIndices.Add(i);
        }

        while (revealedIndices.Count > RevealedCountPerHint)
        {
            int removeIndex = rng.NextInt(0, revealedIndices.Count); // 제거 대상
            int cellIndex = revealedIndices[removeIndex];            // 실제 칸 인덱스
            result[cellIndex] = false;                               // 비공개 처리
            revealedIndices.RemoveAt(removeIndex);                   // 공개 목록 제거
            hiddenIndices.Add(cellIndex);                            // 비공개 목록 추가
        }

        while (revealedIndices.Count < RevealedCountPerHint && hiddenIndices.Count > 0)
        {
            int addIndex = rng.NextInt(0, hiddenIndices.Count); // 추가 대상
            int cellIndex = hiddenIndices[addIndex];            // 실제 칸 인덱스
            result[cellIndex] = true;                           // 공개 처리
            hiddenIndices.RemoveAt(addIndex);                   // 비공개 목록 제거
            revealedIndices.Add(cellIndex);                     // 공개 목록 추가
        }

        return result;
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
                return false; // 둘 다 공개 또는 둘 다 비공개면 상보 아님
        }

        return true;
    }

    /// <summary>
    /// 힌트 리스트를 셔플하고, 진짜 힌트 인덱스도 함께 갱신한다.
    /// </summary>
    private static void ShuffleHints(SeedRandom rng, List<FinalCodeHintData> hints, ref int trueIndex)
    {
        for (int i = hints.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.NextInt(0, i + 1); // 교환 대상

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
        int validPairCount = 0; // 유효 조합 수
        int validAIndex = -1;   // 유효한 A 인덱스
        int validBIndex = -1;   // 유효한 B 인덱스

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
                    return false; // 2개 이상이면 실패
            }
        }

        if (validPairCount != 1)
            return false; // 0개여도 실패

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
            bool aRevealed = aHint.IsRevealed(i); // A 공개 여부
            bool bRevealed = bHint.IsRevealed(i); // B 공개 여부

            if (aRevealed == bRevealed)
                return false; // 둘 다 공개거나 둘 다 비공개면 실패
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
        int[] result = new int[FinalCodeLength]; // 조합 결과

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
        SeedRandom rng = new SeedRandom(seed + 9999); // fallback 전용 시드

        FinalCodeAnswerData data = new FinalCodeAnswerData(); // 결과 컨테이너

        for (int i = 0; i < FinalCodeLength; i++)
            data.FinalDigits[i] = rng.NextInt(1, 10);

        bool[] aMask = new bool[FinalCodeLength] { true, false, true, false, true, false };  // 진짜 A 패턴
        bool[] bMask = new bool[FinalCodeLength] { false, true, false, true, false, true };  // 진짜 B 패턴

        data.ZoneAHints.Add(BuildHint(data.FinalDigits, aMask)); // 진짜 A
        data.ZoneAHints.Add(BuildHint(data.FinalDigits, new bool[FinalCodeLength] { true, true, false, false, true, false }));   // 가짜 A
        data.ZoneAHints.Add(BuildHint(data.FinalDigits, new bool[FinalCodeLength] { false, true, true, false, false, true }));   // 가짜 A

        data.ZoneBHints.Add(BuildHint(data.FinalDigits, bMask)); // 진짜 B
        data.ZoneBHints.Add(BuildHint(data.FinalDigits, new bool[FinalCodeLength] { true, false, false, true, false, true }));   // 가짜 B
        data.ZoneBHints.Add(BuildHint(data.FinalDigits, new bool[FinalCodeLength] { false, false, true, true, true, false }));   // 가짜 B

        data.TrueAHintIndex = 0;
        data.TrueBHintIndex = 0;

        return data;
    }
}