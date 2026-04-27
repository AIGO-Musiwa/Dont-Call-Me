using TMPro;
using UnityEngine;

/// <summary>
/// 구제구역 내부 힌트 숫자 표시 담당
/// </summary>
public class RescueZoneHintDisplay : MonoBehaviour
{
    [Header("힌트 숫자 TMP")]
    [SerializeField] private TMP_Text hintDigit0Text; // 1 _ _ _ 의 숫자 TMP
    [SerializeField] private TMP_Text hintDigit1Text; // _ 2 _ _ 의 숫자 TMP
    [SerializeField] private TMP_Text hintDigit2Text; // _ _ 6 _ 의 숫자 TMP
    [SerializeField] private TMP_Text hintDigit3Text; // _ _ _ 0 의 숫자 TMP

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false; // 디버그 로그 출력 여부

    /// <summary>
    /// 힌트 숫자 4개를 화면에 반영
    /// </summary>
    public void SetHintDigits(int d0, int d1, int d2, int d3)
    {
        if (hintDigit0Text != null) hintDigit0Text.text = d0.ToString();
        if (hintDigit1Text != null) hintDigit1Text.text = d1.ToString();
        if (hintDigit2Text != null) hintDigit2Text.text = d2.ToString();
        if (hintDigit3Text != null) hintDigit3Text.text = d3.ToString();

        Log($"힌트 적용 완료 | {d0} / {d1} / {d2} / {d3}");
    }

    /// <summary>
    /// 힌트 숫자 초기화
    /// </summary>
    public void ClearHintDigits()
    {
        if (hintDigit0Text != null) hintDigit0Text.text = string.Empty;
        if (hintDigit1Text != null) hintDigit1Text.text = string.Empty;
        if (hintDigit2Text != null) hintDigit2Text.text = string.Empty;
        if (hintDigit3Text != null) hintDigit3Text.text = string.Empty;

        Log("힌트 초기화 완료");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RescueZoneHintDisplay] {message}", this);
    }
}