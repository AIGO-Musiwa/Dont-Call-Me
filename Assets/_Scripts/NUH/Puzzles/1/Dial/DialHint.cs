using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 다이얼 퍼즐 힌트 표시 담당
/// 같은 seed로 숫자 6개를 재생성해서 TMP 칸에 표시
/// </summary>
public class DialHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("설정")]
    [SerializeField] private int totalSteps = 6;                    // 힌트 개수

    [Header("TMP 슬롯")]
    [SerializeField] private List<TextMeshPro> stepTexts = new();   // 숫자 표시용 TMP 6칸

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    private readonly List<int> _answerStepCounts = new();            // 생성된 숫자열

    
    /// <summary>
    /// 같은 seed로 숫자열을 재구성하고 힌트에 반영
    /// </summary>
    /// <param name="seed"></param>
    public void ApplyAnswerSeed(int seed)
    {
        _answerStepCounts.Clear();
        _answerStepCounts.AddRange(DialAnswerGenerator.GenerateStepCounts(seed, totalSteps));

        ApplyHintVisual();
        Log($"다이얼 힌트 시드 적용 완료 | seed = {seed}");
        LogStepsDebug();
    }


    /// <summary>
    /// TMP 슬롯에 숫자를 순서대로 표시
    /// </summary>
    private void ApplyHintVisual()
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

    private void LogStepsDebug()
    {
        if (!enableDebugLog)
            return;

        string stepString = string.Join(", ", _answerStepCounts);
        Debug.Log($"[DialHint] 힌트 숫자열 = [{stepString}]", this);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[DialHint] {message}", this);
    }
}
