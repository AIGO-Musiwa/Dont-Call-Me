using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다이얼 퍼즐 정답 숫자열 생성 전용 유틸
/// 퍼즐 본체와 힌트가 같은 seed로 완전히 같은 숫자열을 만들기 위해 분리
/// </summary>
public static class DialAnswerGenerator
{
    /// <summary>
    /// 시드 기반으로 다이얼 정답 숫자 6개를 생성
    /// 방향은 생성 x 
    /// 방향은 항상 좌->우->좌->우->...
    /// </summary>
    public static List<int> GenerateStepCounts(int seed, int totalSteps)
    {
        SeedRandom rng = new SeedRandom(seed);          // 시드 기반 난수기
        List<int> result = new List<int>(totalSteps);   // 생성된 숫자열

        int currentSignedPosition = 0;                  // 현재 다이얼 누적 위치값 (0, +1, -2 같은 값)

        for (int stepIndex = 0; stepIndex < totalSteps; stepIndex++)
        {
            bool isLeftStep = stepIndex % 2 == 0;       // 0,2,4번째는 좌 / 1,3,5번째는 우로 회전해야함
            int minStepCount;
            int maxStepCount = 7;   // 2단계 이후 최대값

            if(stepIndex == 0)
            {
                // 1단계는 좌측 고정 1~4
                minStepCount = 1;
                maxStepCount = 4;
            }
            else
            {
                // 다음 단계부터는 반드시 0을 지나가야 하므로 최소값 계산
                minStepCount = Mathf.Abs(currentSignedPosition) + 1;
            }

            // 최소값이 최대값보다 커지면 최대값으로 보정
            minStepCount = Mathf.Clamp(minStepCount, 1, maxStepCount);

            // SeedRandom.NextInd 는 max exclusive라 +1 필요
            int stepCount = rng.NextInt(minStepCount, maxStepCount + 1);
            result.Add(stepCount);

            // 생성 즉시 signed position 갱신
            if (isLeftStep)
                currentSignedPosition -= stepCount;
            else
                currentSignedPosition += stepCount;
        }

        return result;
    }
}
