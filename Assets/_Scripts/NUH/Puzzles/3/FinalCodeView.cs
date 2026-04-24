using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 화면 표시 담당.
/// 
/// 역할
/// - 현재 입력된 6자리 숫자를 화면에 반영한다.
/// - 오답 시 "Error - 불일치" TMP를 빨간색으로 3회 점멸시킨다.
/// - 기본 화면 / 성공 화면 전환을 담당한다.
/// </summary>
public class FinalCodeView : MonoBehaviour
{
    [Header("입력 숫자 표시")]
    [SerializeField] private List<TextMeshPro> digitTexts = new(); // 6칸 입력 숫자 표시 TMP 목록

    [Header("에러 TMP")]
    [SerializeField] private TextMeshPro errorText; // 오답 시 표시할 에러 TMP
    [SerializeField] private string errorMessage = "Error - 불일치"; // 오답 시 표시할 문구
    [SerializeField] private Color errorColor = Color.red; // 오답 문구 색상
    [SerializeField] private Color errorHiddenColor = new Color(1f, 0f, 0f, 0f); // 에러 숨김 상태 색상(투명 빨강)

    [Header("입력 숫자 색상")]
    [SerializeField] private Color digitColor = Color.white; // 입력 숫자 색상

    [Header("실패 점멸")]
    [SerializeField] private float failFlashInterval = 0.15f; // 빨강/숨김 전환 간격
    [SerializeField] private int failFlashCount = 3; // 점멸 횟수

    [Header("상태 루트")]
    [SerializeField] private GameObject defaultRoot; // 기본 입력 화면 루트
    [SerializeField] private GameObject solvedRoot; // 성공 화면 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private Coroutine _failFlashRoutine; // 현재 실패 점멸 코루틴 참조

    private void Awake()
    {
        ResetToDefault(); // 시작 시 기본 상태로 초기화
    }

    /// <summary>
    /// 현재 입력 숫자들을 6칸 화면에 반영한다.
    /// 
    /// 예:
    /// 입력 [4]           -> _ _ _ _ _ 4
    /// 입력 [4,4,4]       -> _ _ _ 4 4 4
    /// 입력 [1,2,3,4,5,6] -> 1 2 3 4 5 6
    /// </summary>
    public void ApplyInputDigits(IReadOnlyList<int> digits)
    {
        if (digitTexts == null || digitTexts.Count == 0)
            return; // 표시할 TMP 목록이 없으면 종료

        ClearDigitTexts(); // 먼저 모든 칸 비우기

        if (digits == null || digits.Count == 0)
            return; // 입력이 비어 있으면 빈 상태 유지

        int maxCount = Mathf.Min(digitTexts.Count, digits.Count); // 실제 반영 가능한 개수 계산

        for (int i = 0; i < maxCount; i++)
        {
            int targetTextIndex = digitTexts.Count - maxCount + i; // 오른쪽부터 채워지도록 대상 인덱스 계산
            if (targetTextIndex < 0 || targetTextIndex >= digitTexts.Count)
                continue; // 방어 코드

            TextMeshPro targetText = digitTexts[targetTextIndex]; // 실제 표시 대상 TMP
            if (targetText == null)
                continue; // null 참조 방어

            targetText.text = digits[i].ToString(); // 입력 숫자 표시
            targetText.color = digitColor; // 숫자 색상 적용
        }
    }

    /// <summary>
    /// 오답 시 에러 TMP 점멸 연출을 시작한다.
    /// </summary>
    public void PlayFailFlash()
    {
        StopFailFlashIfRunning(); // 기존 점멸 중이면 먼저 중단
        _failFlashRoutine = StartCoroutine(CoFailFlash()); // 새 점멸 시작
    }

    /// <summary>
    /// 성공 상태 화면으로 전환한다.
    /// </summary>
    public void ShowSolvedState()
    {
        StopFailFlashIfRunning(); // 점멸 중이면 중단
        HideErrorImmediate(); // 에러 문구 즉시 숨김

        if (defaultRoot != null)
            defaultRoot.SetActive(false); // 기본 입력 화면 숨김

        if (solvedRoot != null)
            solvedRoot.SetActive(true); // 성공 화면 표시

        Log("성공 상태 표시");
    }

    /// <summary>
    /// 기본 상태로 화면을 초기화한다.
    /// </summary>
    public void ResetToDefault()
    {
        StopFailFlashIfRunning(); // 점멸 중이면 중단
        ClearDigitTexts(); // 입력 칸 비우기
        HideErrorImmediate(); // 에러 문구 숨기기

        if (defaultRoot != null)
            defaultRoot.SetActive(true); // 기본 입력 화면 표시

        if (solvedRoot != null)
            solvedRoot.SetActive(false); // 성공 화면 숨김

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 모든 숫자 표시를 비운다.
    /// </summary>
    public void ClearDigitTexts()
    {
        if (digitTexts == null)
            return;

        for (int i = 0; i < digitTexts.Count; i++)
        {
            if (digitTexts[i] == null)
                continue;

            digitTexts[i].text = string.Empty; // 텍스트 비우기
            digitTexts[i].color = digitColor; // 기본 숫자 색 복구
        }
    }

    /// <summary>
    /// 에러 TMP를 즉시 숨긴다.
    /// </summary>
    private void HideErrorImmediate()
    {
        if (errorText == null)
            return;

        errorText.text = errorMessage; // 문구는 유지
        errorText.color = errorHiddenColor; // 투명 상태로 숨김
    }

    /// <summary>
    /// 에러 TMP를 즉시 보이게 한다.
    /// </summary>
    private void ShowErrorImmediate()
    {
        if (errorText == null)
            return;

        errorText.text = errorMessage; // 에러 문구 적용
        errorText.color = errorColor; // 빨간색으로 표시
    }

    /// <summary>
    /// 오답 시 에러 문구를 3회 점멸시키는 코루틴.
    /// </summary>
    private IEnumerator CoFailFlash()
    {
        for (int i = 0; i < failFlashCount; i++)
        {
            ShowErrorImmediate(); // 에러 문구 표시
            yield return new WaitForSeconds(failFlashInterval); // 잠시 대기

            HideErrorImmediate(); // 에러 문구 숨김
            yield return new WaitForSeconds(failFlashInterval); // 잠시 대기
        }

        _failFlashRoutine = null; // 코루틴 종료 표시
    }

    /// <summary>
    /// 현재 실패 점멸 코루틴이 돌고 있으면 중단한다.
    /// </summary>
    private void StopFailFlashIfRunning()
    {
        if (_failFlashRoutine == null)
            return; // 실행 중인 코루틴이 없으면 종료

        StopCoroutine(_failFlashRoutine); // 기존 코루틴 중단
        _failFlashRoutine = null; // 참조 초기화
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[FinalCodeView] {message}", this);
    }
}