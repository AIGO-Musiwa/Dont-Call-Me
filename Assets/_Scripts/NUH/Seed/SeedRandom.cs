using System;
using System.Collections.Generic;

/// <summary>
/// 시드 기반 난수 유틸
/// UnityEngine.Random 대신 이 클래스를 통해 일관된 랜덤을 사용
/// </summary>
public class SeedRandom
{
    private readonly Random _random;        // 내부 시드 기반 RNG

    public SeedRandom(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// min 이상 max 미만 정수 랜덤 반환
    /// </summary>
    public int NextInt(int minInclusive, int maxInclusive)
    {
        return _random.Next(minInclusive, maxInclusive);
    }

    /// <summary>
    /// 0.0 이상, 1.0 미만 실수 랜덤 반환
    /// </summary>
    public float NextFloat()
    {
        return (float)_random.NextDouble();
    }

    /// <summary>
    /// 리스트를 Fisher-Yates 방식으로 섞음
    /// </summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// 후보 리스트에서 count 개를 중복 없이 선택
    /// </summary>
    public List<T> PickUnique<T>(IList<T> source, int count)
    {
        List<T> copied = new(source);
        Shuffle(copied);

        int pickCount = Math.Min(count, copied.Count);
        List<T> result = new(pickCount);

        for (int i = 0; i < pickCount; i++)
            result.Add(copied[i]);

        return result;
    }
}
