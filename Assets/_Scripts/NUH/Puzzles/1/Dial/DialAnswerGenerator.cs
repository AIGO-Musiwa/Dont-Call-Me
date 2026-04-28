using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다이얼 퍼즐 정답 데이터.
/// 시작 방향과 단계별 회전 횟수를 함께 가진다.
/// </summary>
public class DialAnswerData
{
    public RotationDirection FirstDirection;        // 첫 입력 방향
    public List<int> StepCounts = new();            // 단계별 회전 횟수
}

/// <summary>
/// 다이얼 퍼즐 정답 생성 전용 유틸.
/// 퍼즐 본체와 힌트가 같은 seed로 같은 시작 방향/숫자열을 만들기 위해 사용한다.
/// </summary>
public static class DialAnswerGenerator
{
    /// <summary>
    /// 시드 기반으로 다이얼 정답 데이터를 생성한다.
    /// - 첫 방향은 좌/우 랜덤
    /// - 이후 방향은 반대로 번갈아 진행
    /// - 36도 기준 10칸 다이얼이므로 최대 9회까지 허용
    /// </summary>
    public static DialAnswerData GenerateAnswerData(int seed, int totalSteps)
    {
        SeedRandom rng = new SeedRandom(seed);              // 시드 기반 난수기
        DialAnswerData result = new DialAnswerData();       // 최종 정답 데이터

        result.FirstDirection = rng.NextInt(0, 2) == 0
            ? RotationDirection.Left
            : RotationDirection.Right;                      // 첫 방향 랜덤 결정

        int currentSignedPosition = 0;                      // 현재 다이얼 누적 위치값

        for (int stepIndex = 0; stepIndex < totalSteps; stepIndex++)
        {
            RotationDirection direction = GetDirectionByStepIndex(result.FirstDirection, stepIndex);

            int minStepCount;
            int maxStepCount = 9;                            // 36도 기준 10칸이므로 최대 9회

            if (stepIndex == 0)
            {
                minStepCount = 1;
                maxStepCount = 5;                            // 첫 단계는 반 바퀴 이내 1~5회
            }
            else
            {
                minStepCount = Mathf.Abs(currentSignedPosition) + 1; // 0을 지나가도록 최소 횟수 계산
            }

            minStepCount = Mathf.Clamp(minStepCount, 1, maxStepCount);

            int stepCount = rng.NextInt(minStepCount, maxStepCount + 1);
            result.StepCounts.Add(stepCount);

            if (direction == RotationDirection.Left)
                currentSignedPosition -= stepCount;
            else
                currentSignedPosition += stepCount;
        }

        return result;
    }

    /// <summary>
    /// 기존 코드 호환용 숫자열 생성 함수.
    /// 가능하면 새 코드에서는 GenerateAnswerData()를 사용한다.
    /// </summary>
    public static List<int> GenerateStepCounts(int seed, int totalSteps)
    {
        return GenerateAnswerData(seed, totalSteps).StepCounts;
    }

    /// <summary>
    /// 첫 방향과 단계 인덱스를 기준으로 현재 단계 방향을 계산한다.
    /// </summary>
    public static RotationDirection GetDirectionByStepIndex(RotationDirection firstDirection, int stepIndex)
    {
        bool sameAsFirst = stepIndex % 2 == 0;

        if (sameAsFirst)
            return firstDirection;

        return firstDirection == RotationDirection.Left
            ? RotationDirection.Right
            : RotationDirection.Left;
    }
}