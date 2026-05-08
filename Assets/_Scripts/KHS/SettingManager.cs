using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임의 모든 환경 설정을 관리하는 중앙 관제 모듈.
/// 오디오 믹서 및 감도 설정을 로컬 저장소(PlayerPrefs)와 동기화한다.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("설정 패널")]
    [SerializeField] private GameObject settingPanel;

    [Header("설정 탭 버튼")]
    [SerializeField] private Button displayButton;
    [SerializeField] private Button audioButton;
    [SerializeField] private Button controlButton;
    [SerializeField] private Button mikeButton;

    [Header("탭 UI 패널")]
    [SerializeField] private GameObject displayUIPanel;
    [SerializeField] private GameObject audioUIPanel;
    [SerializeField] private GameObject controlUIPanel;
    [SerializeField] private GameObject mikeUIPanel;

    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;

    public static bool IsOpen { get; private set; } = false;

    private void Start()
    {
        // 버튼 이벤트 구독
        displayButton.onClick.AddListener(OpenDisplayUIPanel);
        audioButton.onClick.AddListener(OpenAudioUIPanel);
        controlButton.onClick.AddListener(OpenControlUIPanel);
        mikeButton.onClick.AddListener(OpenMikeUIPanel);
        closeButton.onClick.AddListener(CloseSettingPanel);

        CloseAllUIPanel();
        settingPanel.SetActive(false);
    }

    private void Update()
    {
        if (settingPanel.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseSettingPanel();
    }

    // 설정창 끄기 / 닫기
    public void ToggleSettingPanel()
    {
        if (settingPanel == null) return;

        if (MicCalibrationUI.IsCalibrating) return;

        bool isActive = !settingPanel.activeSelf;
        settingPanel.SetActive(isActive);
        IsOpen = isActive;

        if (isActive)
        {
            OpenDisplayUIPanel();

        }
    }

    // 화면 설정 UI창 켜기
    private void OpenDisplayUIPanel()
    {
        CloseAllUIPanel();
        displayUIPanel.SetActive(true);
    }

    // 오디오 UI창 켜기
    private void OpenAudioUIPanel()
    {
        CloseAllUIPanel();
        audioUIPanel.SetActive(true);
    }

    // 조작 UI창 켜기
    private void OpenControlUIPanel()
    {
        CloseAllUIPanel();
        controlUIPanel.SetActive(true);
    }

    private void OpenMikeUIPanel()
    {
        CloseAllUIPanel();
        mikeUIPanel.SetActive(true);
    }

    // 모든 UI창 끄기
    private void CloseAllUIPanel()
    {
        displayUIPanel.SetActive(false);
        audioUIPanel.SetActive(false);
        controlUIPanel.SetActive(false);
        mikeUIPanel.SetActive(false);
    }

    // 설정 창 끄기
    private void CloseSettingPanel()
    {
        settingPanel.SetActive(false);
        IsOpen = false;
    }
}