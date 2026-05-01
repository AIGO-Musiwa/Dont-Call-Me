using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 다이얼 퍼즐 힌트 표시 담당.
/// 같은 seed로 시작 방향과 숫자열을 재생성해서 힌트 오브젝트에 표시한다.
/// </summary>
public class DialHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int totalSteps = 6;                          // 힌트 개수

    [Header("시작 방향 Sprite")]
    [SerializeField] private SpriteRenderer firstDirectionSpriteRenderer; // 시작 방향 표시용 SpriteRenderer

    [Header("TMP 슬롯")]
    [SerializeField] private List<TextMeshPro> stepTexts = new();         // 숫자 표시용 TMP 6칸

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                  // 디버그 로그 여부

    private readonly List<int> _answerStepCounts = new();                 // 생성된 숫자열
    private RotationDirection _firstDirection;                            // 생성된 첫 방향

    /// <summary>
    /// 같은 seed로 시작 방향과 숫자열을 재구성하고 힌트에 반영한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        DialAnswerData data = DialAnswerGenerator.GenerateAnswerData(seed, totalSteps);

        _firstDirection = data.FirstDirection;

        _answerStepCounts.Clear();
        _answerStepCounts.AddRange(data.StepCounts);

        ApplyHintVisual();

        Log($"다이얼 힌트 시드 적용 완료 | seed = {seed}");
        LogStepsDebug();
    }

    /// <summary>
    /// 시작 방향 Sprite와 숫자 TMP를 표시한다.
    /// </summary>
    private void ApplyHintVisual()
    {
        ApplyFirstDirectionSpriteVisual();
        ApplyStepNumberVisual();
    }

    /// <summary>
    /// 시작 방향 Sprite의 X축 flip 상태를 반영한다.
    /// flipX == false : 좌측부터 돌리기
    /// flipX == true  : 우측부터 돌리기
    /// </summary>
    private void ApplyFirstDirectionSpriteVisual()
    {
        if (firstDirectionSpriteRenderer == null)
            return;

        firstDirectionSpriteRenderer.flipX = _firstDirection == RotationDirection.Right;
    }

    /// <summary>
    /// TMP 슬롯에 단계별 회전 횟수를 순서대로 표시한다.
    /// </summary>
    private void ApplyStepNumberVisual()
    {
        int count = Mathf.Min(stepTexts.Count, _answerStepCounts.Count);

        for (int i = 0; i < stepTexts.Count; i++)
        {
            if (stepTexts[i] == null)
                continue;

            if (i >= count)
            {
                stepTexts[i].text = string.Empty;
                continue;
            }

            stepTexts[i].text = _answerStepCounts[i].ToString();
        }
    }

    /// <summary>
    /// 정답 숫자열과 첫 방향을 로그로 출력한다.
    /// </summary>
    private void LogStepsDebug()
    {
        if (!enableDebugLog)
            return;

        string stepString = string.Join(", ", _answerStepCounts);
        Debug.Log($"[DialHint] 시작 방향 = {_firstDirection} | 힌트 숫자열 = [{stepString}]", this);
    }

    /// <summary>
    /// 디버그 로그를 출력한다.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[DialHint] {message}", this);
    }
}