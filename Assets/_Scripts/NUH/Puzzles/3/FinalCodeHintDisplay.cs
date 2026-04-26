using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 3단계 최종 힌트 1개를 화면에 표시하는 공용 디스플레이.
/// 
/// 역할
/// - 6칸 숫자/빈칸 데이터를 받아 모니터에 반영한다.
/// - 각 2단계 퍼즐의 Stage3HintRoot 내부에서 공통으로 사용한다.
/// </summary>
public class FinalCodeHintDisplay : MonoBehaviour
{
    [Header("6칸 숫자 표시")]
    [SerializeField] private List<TextMeshPro> digitTexts = new(); // 6칸 텍스트

    [Header("표시 설정")]
    [SerializeField] private string emptySymbol = "_"; // 빈칸 표시 문자
    [SerializeField] private Color digitColor = Color.white; // 숫자 색상
    [SerializeField] private Color emptyColor = Color.white; // 빈칸 색상

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 여부

    /// <summary>
    /// 힌트 데이터를 화면에 반영한다.
    /// </summary>
    public void ApplyHint(FinalCodeHintData hintData)
    {
        if (hintData == null)
            return;

        if (digitTexts == null || digitTexts.Count == 0)
            return;

        for (int i = 0; i < digitTexts.Count; i++)
        {
            if (digitTexts[i] == null)
                continue;

            bool isRevealed = hintData.IsRevealed(i); // 현재 칸 공개 여부
            digitTexts[i].text = isRevealed ? hintData.GetDigit(i).ToString() : emptySymbol; // 숫자 또는 빈칸 표시
            digitTexts[i].color = isRevealed ? digitColor : emptyColor; // 색상 적용
        }

        Log($"힌트 적용 완료 | {hintData.GetDebugString()}");
    }

    /// <summary>
    /// 화면을 기본 빈칸 상태로 초기화한다.
    /// </summary>
    public void ResetDisplay()
    {
        if (digitTexts == null || digitTexts.Count == 0)
            return;

        for (int i = 0; i < digitTexts.Count; i++)
        {
            if (digitTexts[i] == null)
                continue;

            digitTexts[i].text = emptySymbol; // 빈칸 문자 표시
            digitTexts[i].color = emptyColor; // 빈칸 색상 적용
        }

        Log("힌트 표시 초기화");
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[FinalCodeHintDisplay] {message}", this);
    }
}

/// <summary>
/// 3단계 최종 힌트 1개를 담는 데이터 클래스.
/// 
/// 규칙
/// - Digits는 항상 길이 6
/// - Revealed는 항상 길이 6
/// - Revealed[i]가 true이면 Digits[i]를 화면에 표시한다.
/// </summary>
[Serializable]
public class FinalCodeHintData
{
    public const int HintLength = 6; // 최종 힌트 길이

    [SerializeField] private int[] digits = new int[HintLength]; // 6칸 숫자 데이터
    [SerializeField] private bool[] revealed = new bool[HintLength]; // 각 칸 공개 여부

    public FinalCodeHintData()
    {
        for (int i = 0; i < HintLength; i++)
        {
            digits[i] = 0; // 기본 숫자 초기화
            revealed[i] = false; // 기본 공개 상태 초기화
        }
    }

    public FinalCodeHintData(int[] sourceDigits, bool[] sourceRevealed)
    {
        digits = new int[HintLength];
        revealed = new bool[HintLength];

        for (int i = 0; i < HintLength; i++)
        {
            digits[i] = sourceDigits != null && i < sourceDigits.Length ? sourceDigits[i] : 0; // 숫자 복사
            revealed[i] = sourceRevealed != null && i < sourceRevealed.Length && sourceRevealed[i]; // 공개 여부 복사
        }
    }

    /// <summary>
    /// 특정 칸의 숫자를 반환한다.
    /// </summary>
    public int GetDigit(int index)
    {
        if (index < 0 || index >= HintLength)
            return 0;

        return digits[index];
    }

    /// <summary>
    /// 특정 칸이 공개 상태인지 반환한다.
    /// </summary>
    public bool IsRevealed(int index)
    {
        if (index < 0 || index >= HintLength)
            return false;

        return revealed[index];
    }

    /// <summary>
    /// 특정 칸 데이터를 설정한다.
    /// </summary>
    public void SetCell(int index, int digit, bool isRevealed)
    {
        if (index < 0 || index >= HintLength)
            return;

        digits[index] = digit; // 숫자 저장
        revealed[index] = isRevealed; // 공개 여부 저장
    }

    /// <summary>
    /// 디버그 문자열을 반환한다.
    /// </summary>
    public string GetDebugString()
    {
        string result = string.Empty;

        for (int i = 0; i < HintLength; i++)
        {
            result += revealed[i] ? digits[i].ToString() : "_";
            if (i < HintLength - 1)
                result += " ";
        }

        return result;
    }
}