using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingUIController : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text percentText;

    private Coroutine _progressCoroutine;

    private void Start()
    {
        GameLauncher.Instance.OnSceneLoadStarted += ShowLoadingUI;
        GameLauncher.Instance.OnSceneLoadFinished += HideLoadingUI;
    }

    private void OnDestroy()
    {
        if (GameLauncher.Instance == null) return;
        GameLauncher.Instance.OnSceneLoadStarted -= ShowLoadingUI;
        GameLauncher.Instance.OnSceneLoadFinished -= HideLoadingUI;
    }

    // 로딩 시작 ─────────────────────────────────────────
    private void ShowLoadingUI()
    {
        loadingPanel.SetActive(true);
        progressBar.value = 0f;
        percentText.text = "0%";

        if (_progressCoroutine != null) StopCoroutine(_progressCoroutine);
        _progressCoroutine = StartCoroutine(FakeProgress());
    }

    // 로딩 완료 ─────────────────────────────────────────
    private void HideLoadingUI()
    {
        if (_progressCoroutine != null) StopCoroutine(_progressCoroutine);
        _progressCoroutine = StartCoroutine(FinishAndHide());
    }

    // 0% → 90% 천천히 채우기 ────────────────────────────
    IEnumerator FakeProgress()
    {
        float progress = 0f;
        while (progress < 0.9f)
        {
            progress += Time.deltaTime * 0.5f;
            SetProgress(Mathf.Min(progress, 0.9f));
            yield return null;
        }
    }

    // 완료 시 100%로 채우고 패널 닫기 ────────────────────
    IEnumerator FinishAndHide()
    {
        float t = progressBar.value;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            SetProgress(Mathf.Min(t, 1f));
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);
        loadingPanel.SetActive(false);
    }

    private void SetProgress(float value)
    {
        progressBar.value = value;
        percentText.text = Mathf.Round(value * 100) + "%";
    }
}