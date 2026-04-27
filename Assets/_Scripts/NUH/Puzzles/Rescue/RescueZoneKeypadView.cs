using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 구제구역 키패드 1개의 화면 표시 담당
/// Zone당 2개 존재 가능
/// </summary>
public class RescueZoneKeypadView : MonoBehaviour
{
    [Header("입력 숫자 TMP")]
    [SerializeField] private TMP_Text[] digitTexts; // 입력 숫자 4칸 표시 TMP

    [Header("상태 UI")]
    [SerializeField] private TMP_Text errorText;      // 오답 표시 TMP
    [SerializeField] private GameObject defaultRoot;  // 기본 상태 루트
    [SerializeField] private GameObject solvedRoot;   // 성공 상태 루트

    [Header("실패 연출")]
    [SerializeField] private int failFlashCount = 3;          // 오답 점멸 횟수
    [SerializeField] private float failFlashInterval = 0.2f;  // 오답 점멸 간격

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;     // 디버그 로그 출력 여부

    private Coroutine _failCoroutine; // 실패 연출 코루틴 핸들

    /// <summary>
    /// 현재 입력 숫자를 화면에 반영
    /// </summary>
    public void RefreshInput(int[] digits, int inputCount)
    {
        if (digitTexts == null || digitTexts.Length == 0)
            return;

        // 일단 전부 비우기
        for (int i = 0; i < digitTexts.Length; i++)
        {
            if (digitTexts[i] == null)
                continue;

            digitTexts[i].text = string.Empty;
        }

        if (digits == null || inputCount <= 0)
            return;

        int maxCount = Mathf.Min(inputCount, digitTexts.Length);

        // 입력된 숫자를 오른쪽부터 채우기
        for (int i = 0; i < maxCount; i++)
        {
            int targetIndex = digitTexts.Length - maxCount + i; // 뒤에서부터 채울 인덱스 계산

            if (targetIndex < 0 || targetIndex >= digitTexts.Length)
                continue;

            if (digitTexts[targetIndex] == null)
                continue;

            digitTexts[targetIndex].text = digits[i].ToString();
        }
    }

    /// <summary>
    /// 기본 상태 화면 표시
    /// </summary>
    public void ShowDefault()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(true);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 성공 상태 화면 표시
    /// </summary>
    public void ShowSolved()
    {
        if (defaultRoot != null)
            defaultRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(true);

        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 입력 표시 초기화
    /// </summary>
    public void ClearInputVisual()
    {
        if (digitTexts == null)
            return;

        for (int i = 0; i < digitTexts.Length; i++)
        {
            if (digitTexts[i] == null)
                continue;

            digitTexts[i].text = string.Empty;
        }
    }

    /// <summary>
    /// 오답 점멸 연출 재생
    /// </summary>
    public void PlayFailFlash()
    {
        if (_failCoroutine != null)
            StopCoroutine(_failCoroutine);

        _failCoroutine = StartCoroutine(CoFailFlash());
    }

    private IEnumerator CoFailFlash()
    {
        if (errorText == null)
            yield break;

        for (int i = 0; i < failFlashCount; i++)
        {
            errorText.gameObject.SetActive(true);
            yield return new WaitForSeconds(failFlashInterval);

            errorText.gameObject.SetActive(false);
            yield return new WaitForSeconds(failFlashInterval);
        }

        _failCoroutine = null;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RescueZoneKeypadView] {message}", this);
    }
}