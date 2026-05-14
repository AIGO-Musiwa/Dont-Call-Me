using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 화면 페이드 인/아웃을 담당하는 싱글톤 UI 매니저
/// 탈출 구역 진입 등 시네마틱한 화면 전환이 필요할 때 사용
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("UI 참조")]
    [Tooltip("화면 전체를 덮고 있는 검은색 UI Image")]
    public Image fadeImage;

    [Header("설정")]
    public Color fadeColor = Color.black;

    private void Awake()
    {
        //싱글톤 패턴 구현
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        //초기 상태는 완전히 투명한 검은색
        if (fadeImage != null)
        {
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 지정된 시간 동안 화면을 서서히 까맣게 만듦
    /// </summary>
    public void FadeOut(float duration = 2.0f)
    {
        if (fadeImage == null) return;

        //페이드 아웃 시작 시 이미지 활성화
        fadeImage.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(fadeImage.color.a, 1f, duration));
    }

    /// <summary>
    /// 지정된 시간 동안 화면을 서서히 밝게(투명하게) 만듦
    /// </summary>
    public void FadeIn(float duration = 2.0f)
    {
        if (fadeImage == null) return;

        //페이드 인 시작 시 이미지 활성화 (알파값이 0이 될 때까지 유지)
        fadeImage.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(fadeImage.color.a, 0f, duration));
    }

    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
    {
        float timer = 0f;
        Color color = fadeColor;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            //알파값을 부드럽게 보간
            color.a = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        fadeImage.color = color;
    }
}
