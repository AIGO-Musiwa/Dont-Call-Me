using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 2-1 숫자 입력 퍼즐의 모니터 하단 입력 / 화면 표현 담당.
/// 
/// 역할
/// - 하단 4자리 숫자 표시
/// - 숫자 입력 시 한 칸씩 왼쪽으로 밀리는 표현 반영
/// - 오답 시 화면 빨간색 3회 점멸
/// - 성공 후 화면 전환용 표시 제어
/// - Stage3HintRoot에 3단계 힌트를 표시한다.
/// </summary>
public class NumericCodeView : MonoBehaviour
{
    [Header("입력 숫자 표시")]
    [SerializeField] private List<TextMeshPro> digitTexts = new(); // 하단 4칸 숫자 표시

    [Header("화면 배경")]
    [SerializeField] private SpriteRenderer screenRenderer; // 화면 배경 SR
    [SerializeField] private Color normalScreenColor = Color.white; // 기본 화면 색
    [SerializeField] private Color failScreenColor = Color.red; // 실패 점멸 색

    [Header("숫자 색")]
    [SerializeField] private Color digitColor = Color.black; // 숫자 색상

    [Header("실패 점멸")]
    [SerializeField] private float failFlashInterval = 0.15f; // 빨강/흰색 전환 간격
    [SerializeField] private int failFlashCount = 3; // 빨간 점멸 횟수

    [Header("상태 루트")]
    [SerializeField] private GameObject inputRoot; // 입력 화면 루트
    [SerializeField] private GameObject solvedRoot; // 성공 표시 루트
    [SerializeField] private GameObject stage3HintRoot; // 성공 후 3단계 힌트 표시 루트

    [Header("3단계 힌트 표시기")]
    [SerializeField] private FinalCodeHintDisplay stage3HintDisplay; // Stage3HintRoot 내부 최종 힌트 표시기

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private Coroutine _failFlashRoutine; // 현재 실패 점멸 코루틴

    private void Awake()
    {
        ResetToDefault();
    }

    /// <summary>
    /// 현재 입력 숫자들을 화면 하단 4칸에 반영한다.
    /// </summary>
    public void ApplyInputDigits(IReadOnlyList<int> digits)
    {
        if (digitTexts == null || digitTexts.Count == 0)
            return;

        ClearDigitTexts();

        if (digits == null || digits.Count == 0)
            return;

        int maxCount = Mathf.Min(digitTexts.Count, digits.Count);

        for (int i = 0; i < maxCount; i++)
        {
            int targetTextIndex = digitTexts.Count - maxCount + i;
            if (targetTextIndex < 0 || targetTextIndex >= digitTexts.Count)
                continue;

            TextMeshPro targetText = digitTexts[targetTextIndex];
            if (targetText == null)
                continue;

            targetText.text = digits[i].ToString();
            targetText.color = digitColor;
        }
    }

    /// <summary>
    /// 3단계 힌트 데이터를 표시기에 반영한다.
    /// </summary>
    public void ApplyStage3Hint(FinalCodeHintData hintData)
    {
        if (stage3HintDisplay == null)
            return;

        stage3HintDisplay.ApplyHint(hintData);
    }

    /// <summary>
    /// 오답 시 빨간색 3회 점멸 연출 시작.
    /// </summary>
    public void PlayFailFlash()
    {
        StopFailFlashIfRunning();
        _failFlashRoutine = StartCoroutine(CoFailFlash());
    }

    /// <summary>
    /// 성공 상태 표시.
    /// </summary>
    public void ShowSolvedState()
    {
        StopFailFlashIfRunning();
        SetScreenColor(normalScreenColor);

        if (inputRoot != null)
            inputRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(true);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false);

        Log("성공 상태 표시");
    }

    /// <summary>
    /// 성공 후 3단계 힌트 표시 상태로 전환.
    /// </summary>
    public void ShowStage3HintState()
    {
        StopFailFlashIfRunning();
        SetScreenColor(normalScreenColor);

        if (inputRoot != null)
            inputRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(true);

        Log("3단계 힌트 표시 상태로 전환");
    }

    /// <summary>
    /// 기본 상태로 초기화.
    /// </summary>
    public void ResetToDefault()
    {
        StopFailFlashIfRunning();
        SetScreenColor(normalScreenColor);
        ClearDigitTexts();

        if (inputRoot != null)
            inputRoot.SetActive(true);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false);

        if (stage3HintDisplay != null)
            stage3HintDisplay.ResetDisplay();

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 모든 숫자 표시를 비운다.
    /// </summary>
    public void ClearDigitTexts()
    {
        for (int i = 0; i < digitTexts.Count; i++)
        {
            if (digitTexts[i] == null)
                continue;

            digitTexts[i].text = string.Empty;
            digitTexts[i].color = digitColor;
        }
    }

    /// <summary>
    /// 화면 배경색을 즉시 바꾼다.
    /// </summary>
    private void SetScreenColor(Color color)
    {
        if (screenRenderer == null)
            return;

        screenRenderer.color = color;
    }

    /// <summary>
    /// 실패 점멸 코루틴.
    /// </summary>
    private IEnumerator CoFailFlash()
    {
        for (int i = 0; i < failFlashCount; i++)
        {
            SetScreenColor(failScreenColor);
            yield return new WaitForSeconds(failFlashInterval);

            SetScreenColor(normalScreenColor);
            yield return new WaitForSeconds(failFlashInterval);
        }

        _failFlashRoutine = null;
    }

    /// <summary>
    /// 현재 실패 점멸 코루틴이 돌고 있으면 중단한다.
    /// </summary>
    private void StopFailFlashIfRunning()
    {
        if (_failFlashRoutine == null)
            return;

        StopCoroutine(_failFlashRoutine);
        _failFlashRoutine = null;
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[NumericCodeView] {message}", this);
    }
}