using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class DisplaySettingUI : MonoBehaviour
{
    [Header("해상도")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("창 모드")]
    [SerializeField] private TMP_Dropdown windowModeDropdown;

    [Header("밝기 (URP Post Processing")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TextMeshProUGUI brightnessValueText;

    // 씬에 배치된 Global Volume.
    // ColorAdjustments 오버라이드가 추가된 Volume 연결
    [SerializeField] private Volume globalVolume;

    // 지원 해상도 목록 (빌드 지원 해상도에서 필터링)
    private Resolution[] filteredResolutions;

    // ColorAdjustments 캐시
    private ColorAdjustments colorAdjustments;

    // 창 모드 레이블
    private static readonly string[] WindowModeLabels =
    {
        "전체화면", "창모드", "경계없는 창 모드"
    };

    private static readonly FullScreenMode[] WindowModes =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow
    };

    #region Unity LifeCycle

    private void OnEnable()
    {
        TryFetchColorAdjustments();
        InitResolutionDropdown();
        InitWindowModeDropdown();
        InitBrightnessSlider();
    }

    #endregion

    #region 해상도

    private void InitResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        filteredResolutions = FilterResolutions(Screen.resolutions);

        // 16:9 해상도가 없는 환경 폴백
        if (filteredResolutions.Length == 0)
        {
            Debug.LogWarning("[DisplaySettingsUI] 16:9 해상도를 찾지 못했습니다. 전체 해상도 목록을 사용합니다.");
            filteredResolutions = FilterDuplicates(Screen.resolutions);
        }

        var options = new List<string>();
        int savedIndex = PlayerPrefs.GetInt(Constants.KEY_RESOLUTION_INDEX, filteredResolutions.Length - 1);

        for (int i = 0; i < filteredResolutions.Length; i++)
        {
            var r = filteredResolutions[i];
            options.Add($"{r.width} x {r.height}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(savedIndex);
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    // 16:9 비율 해상도만 필터링하고 중복 제거 후 반환
    private Resolution[] FilterResolutions(Resolution[] all)
    {
        var seen = new HashSet<string>();
        var result = new List<Resolution>();

        foreach (var r in all)
        {
            // 16:9 비율 필터
            float aspect = (float)r.width / r.height;
            if (Mathf.Abs(aspect - (16f / 9f)) > 0.01f) continue;

            string key = $"{r.width} x {r.height}";
            if (seen.Add(key))
                result.Add(r);
        }

        return result.ToArray();
    }

    // 중복 제거
    private Resolution[] FilterDuplicates(Resolution[] all)
    {
        var seen = new HashSet<string>();
        var result = new List<Resolution>();

        foreach (var r in all)
        {
            string key = $"{r.width} x {r.height}";
            if (seen.Add(key))
                result.Add(r);
        }
        return result.ToArray();
    }

    private void OnResolutionChanged(int index)
    {
        if (filteredResolutions == null || index >= filteredResolutions.Length) return;

        var r = filteredResolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt(Constants.KEY_RESOLUTION_INDEX, index);
        Debug.Log($"[DisplaySettingsUI] 해상도 변경 → {r.width} x {r.height}");
    }

    #endregion

    #region 창 모드

    private void InitWindowModeDropdown()
    {
        if (windowModeDropdown == null) return;

        var options = new List<string>(WindowModeLabels);
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(options);

        int savedMode = PlayerPrefs.GetInt(Constants.KEY_WINDOW_MODE, 0);
        windowModeDropdown.SetValueWithoutNotify(savedMode);
        windowModeDropdown.RefreshShownValue();

        windowModeDropdown.onValueChanged.RemoveAllListeners();
        windowModeDropdown.onValueChanged.AddListener(OnWindowModeChanged);
    }

    private void OnWindowModeChanged(int index)
    {
        if (index < 0 || index >= WindowModes.Length) return;

        Screen.fullScreenMode = WindowModes[index];
        PlayerPrefs.SetInt(Constants.KEY_WINDOW_MODE, index);
        Debug.Log($"[DisplaySettingsUI] 창 모드 변경 → {WindowModeLabels[index]}");
    }

    #endregion

    #region 밝기

    private void InitBrightnessSlider()
    {
        if (brightnessSlider == null) return;

        brightnessSlider.minValue = Constants.BRIGHTNESS_MIN;
        brightnessSlider.maxValue = Constants.BRIGHTNESS_MAX;

        float saved = PlayerPrefs.GetFloat(Constants.KEY_BRIGHTNESS, 0f);
        brightnessSlider.SetValueWithoutNotify(saved);
        UpdateBrightnessLabel(saved);
        ApplyBrightness(saved);

        brightnessSlider.onValueChanged.RemoveAllListeners();
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    private void OnBrightnessChanged(float value)
    {
        ApplyBrightness(value);
        UpdateBrightnessLabel(value);
        PlayerPrefs.SetFloat(Constants.KEY_BRIGHTNESS, value);
    }

    // URP ColorAdjustments.postExposure에 EV값 적용
    private void ApplyBrightness(float ev)
    {
        if (colorAdjustments == null)
        {
            TryFetchColorAdjustments();
            if (colorAdjustments == null) return;
        }

        colorAdjustments.postExposure.Override(ev);
    }

    private void UpdateBrightnessLabel(float ev)
    {
        if (brightnessValueText == null) return;

        // 0 기준으로 +/- 표시
        brightnessValueText.text = ev >= 0f
            ? $"+{ev:F1}"
            : $"{ev:F1}";
    }

    // ColorAdjustments 초기화
    private void TryFetchColorAdjustments()
    {
        if (globalVolume == null)
            globalVolume = FindAnyObjectByType<Volume>();

        if (globalVolume == null)
        {
            Debug.LogWarning("[DisplaySettingsUI] Global Volume을 찾지 못했습니다. " +
                             "씬에 Volume 컴포넌트를 배치하고 Inspector에 연결해주세요.");
            return;
        }

        globalVolume.profile.TryGet(out colorAdjustments);

        if (colorAdjustments == null)
            Debug.LogWarning("[DisplaySettingsUI] Volume Profile에 ColorAdjustments 오버라이드가 없습니다.");
    }
    #endregion
}
