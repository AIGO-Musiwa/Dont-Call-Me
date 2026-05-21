using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//화면 페이드 인/아웃을 담당하는 싱글톤 UI 매니저
//탈출 구역 진입 등 시네마틱한 화면 전환이 필요할 때 사용
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("UI 참조")]
    [Tooltip("화면 전체를 덮고 있는 검은색 UI Image")]
    public Image fadeImage;

    [Header("시네마틱 눈꺼풀")]
    public RectTransform topEyelid;
    public RectTransform bottomEyelid;

    [Header("시네마틱 설정(URP 사용 시)")]
    public Volume globalVolume;
    [Tooltip("값이 낮을수록 다 떠도 약간 뿌옇게 보입니다. (기본추천: 15~20)")]
    public float maxClearStart = 15f;
    [Tooltip("값이 낮을수록 멀리 있는 배경이 더 뿌옇게 보입니다. (기본추천: 30)")]
    public float maxClearEnd = 30f;

    private DepthOfField depthOfField;
    private Coroutine currentFadeCoroutine;
    private float screenHeight;

    [Header("설정")]
    public Color fadeColor = Color.black;

    private void Awake()
    {
        //싱글톤 패턴 구현
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        //화면 높이 계산
        if (fadeImage != null && fadeImage.canvas != null)
        {
            screenHeight = fadeImage.canvas.GetComponent<RectTransform>().rect.height;
        }

        else
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null) screenHeight = parentCanvas.GetComponent<RectTransform>().rect.height;
            else screenHeight = 1080f;
        }

        //볼륨에서 DOF 컴포넌트 참조
        if (globalVolume != null && globalVolume.profile.TryGet(out depthOfField))
        {
            //초기값 세팅
            depthOfField.active = false;
            depthOfField.gaussianStart.value = maxClearStart;
            depthOfField.gaussianEnd.value = maxClearEnd;
        }

        //검정색 이미지 초기화
        if (fadeImage != null)
        {
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeImage.gameObject.SetActive(false);
        }

        //눈꺼풀 초기화 (꺼둠)
        if (topEyelid != null) topEyelid.gameObject.SetActive(false);
        if (bottomEyelid != null) bottomEyelid.gameObject.SetActive(false);
    }

    //뿌연 상태에서 서서히 선명해지며 눈꺼풀이 열리는 시네마틱 효과 연출
    public void FadeInCinematic(float duration)
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            //검은색 상태에서 시작
            Color c = fadeColor;
            c.a = 1.0f;
            fadeImage.color = c;
        }

        //눈꺼풀 활성화 및 정중앙 배치 (완전히 감은 상태)
        if (topEyelid != null)
        {
            topEyelid.gameObject.SetActive(true);
            topEyelid.anchoredPosition = Vector2.zero;
        }
        if (bottomEyelid != null)
        {
            bottomEyelid.gameObject.SetActive(true);
            bottomEyelid.anchoredPosition = Vector2.zero;
        }

        if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(CinematicWakeUpRoutine(duration));
    }

    //지정된 시간 동안 화면을 서서히 까맣게 만듦
    public void FadeOut(float duration = 2.0f)
    {
        if (fadeImage == null) return;

        if (!fadeImage.gameObject.activeSelf)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }

        //페이드 아웃 시작 시 이미지 활성화
        fadeImage.gameObject.SetActive(true);

        if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(FadeAlphaRoutine(fadeImage.color.a, 1f, duration, false));
    }

    //지정된 시간 동안 화면을 서서히 밝게(투명하게) 만듦
    public void FadeIn(float duration = 2.0f)
    {
        if (fadeImage == null) return;

        //페이드 인 시작 시 이미지 활성화
        fadeImage.gameObject.SetActive(true);

        if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(FadeAlphaRoutine(fadeImage.color.a, 0f, duration, true));
    }

    //시네마틱 효과 연출 코루틴    
    private IEnumerator CinematicWakeUpRoutine(float duration)
    {
        //블러 효과 활성화
        if (depthOfField != null)
        {
            depthOfField.active = true;
            depthOfField.gaussianStart.value = 0f;
            depthOfField.gaussianEnd.value = 0.1f;
        }

        //깜박이는 연출을 위해 페이드 이미지는 잠시 켜뒀다가 바로 끔
        if (fadeImage != null) fadeImage.gameObject.SetActive(false);

        float elapsed = 0f;
        float moveDistance = screenHeight / 2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);

            float openProgress = 0f;

            //타이밍 명확화: 살짝 뜨고 -> 크게 뜨고 -> 완전히 뜨는 3단계 구간 분리
            if (normalized < 0.15f)
            {
                //0~15% 구간: 눈을 30%만 살짝 떴다가 감음
                float t = normalized / 0.15f;
                openProgress = Mathf.Sin(t * Mathf.PI) * 0.3f;
            }

            else if (normalized < 0.25f)
            {
                //15~25% 구간: 잠시 눈을 꽉 감고 대기
                openProgress = 0f;
            }

            else if (normalized < 0.45f)
            {
                //25~45% 구간: 눈을 60% 정도 더 크게 떴다가 감음
                float t = (normalized - 0.25f) / 0.20f;
                openProgress = Mathf.Sin(t * Mathf.PI) * 0.6f;
            }

            else if (normalized < 0.5f)
            {
                //45~50% 구간: 다시 눈 감고 잠시 대기
                openProgress = 0f;
            }

            else
            {
                //50~100% 구간: 마지막으로 스르륵 눈을 완전히 뜸
                float t = (normalized - 0.5f) / 0.5f;
                openProgress = Mathf.SmoothStep(0f, 1f, t);
            }

            //계산값이 마이너스가 되지 않도록 안전장치
            openProgress = Mathf.Max(0f, openProgress);

            //위아래 눈꺼풀 이동 적용
            if (topEyelid != null) topEyelid.anchoredPosition = new Vector2(0, Mathf.Lerp(0f, moveDistance, openProgress));
            if (bottomEyelid != null) bottomEyelid.anchoredPosition = new Vector2(0, Mathf.Lerp(0f, -moveDistance, openProgress));

            //블러 초점 거리 증가(점점 선명해짐)
            if (depthOfField != null)
            {
                depthOfField.gaussianStart.value = Mathf.Lerp(0f, maxClearStart, normalized);
                depthOfField.gaussianEnd.value = Mathf.Lerp(0.1f, maxClearEnd, normalized);
            }

            yield return null;
        }

        //마무리 연출 정리
        if (topEyelid != null) topEyelid.gameObject.SetActive(false);
        if (bottomEyelid != null) bottomEyelid.gameObject.SetActive(false);
        if (depthOfField != null) depthOfField.active = false;
    }

    //단순 알파 보간 코루틴
    private IEnumerator FadeAlphaRoutine(float start, float end, float duration, bool disableOnEnd)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);

            Color c = fadeColor;
            c.a = Mathf.Lerp(start, end, normalized);
            if (fadeImage != null) fadeImage.color = c;
            yield return null;
        }

        //최종 상태 보정
        if (fadeImage != null)
        {
            Color finalC = fadeColor;
            finalC.a = end;
            fadeImage.color = finalC;
            if (disableOnEnd && end == 0f) fadeImage.gameObject.SetActive(false);
        }
    }
}