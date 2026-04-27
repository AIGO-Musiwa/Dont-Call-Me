using UnityEngine;

/// <summary>
/// 구제구역 퍼즐 정답 생성 전용 유틸
/// </summary>
public static class RescueZoneAnswerGenerator
{
    /// <summary>
    /// 4자리 정답 데이터
    /// </summary>
    public struct RescueZoneAnswerData
    {
        public int digit0;
        public int digit1;
        public int digit2;
        public int digit3;
    }


    public static RescueZoneAnswerData Generate(int seed)
    {
        if (seed == 0)
            seed = 1;

        SeedRandom rng = new SeedRandom(seed);

        RescueZoneAnswerData data = new RescueZoneAnswerData
        {
            digit0 = rng.NextInt(0, 10),
            digit1 = rng.NextInt(0, 10),
            digit2 = rng.NextInt(0, 10),
            digit3 = rng.NextInt(0, 10)
        };

        return data;
    }
}
