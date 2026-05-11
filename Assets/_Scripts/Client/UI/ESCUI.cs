using UnityEngine;
using UnityEngine.InputSystem;

public class ESCUI : MonoBehaviour
{
    [Header("ESC 메뉴 UI")]
    [SerializeField] private GameObject escMenuPanel;

    [Header("나가기 확인 패널")]
    [SerializeField] private GameObject exitConfirmationPanel;

    public static bool IsOpen { get; private set; } = false;

    private void Start()
    {
        escMenuPanel.SetActive(false);
        exitConfirmationPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleESCMenu();
    }

    public void ShowESCMenu()
    {
        escMenuPanel.SetActive(true);
        IsOpen = true;
    }

    public void CloseESCMenu()
    {
        escMenuPanel.SetActive(false);
        IsOpen = false;
    }

    public void ToggleESCMenu()
    {
        if (escMenuPanel == null) return;
        if (MicCalibrationUI.IsCalibrating) return;
        if (SettingsManager.IsOpen) return;

        bool isActive = !escMenuPanel.activeSelf;
        escMenuPanel.SetActive(isActive);
        IsOpen = isActive;

        if (isActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            exitConfirmationPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ShowExitConfirmation()
    {
        exitConfirmationPanel.SetActive(true);
        IsOpen = true;
    }

    public void CloseExitConfirmation()
    {
        exitConfirmationPanel.SetActive(false);
        IsOpen = true;
    }

    public void OnClickLeaveButton()
    {
        IsOpen = false;
        GameSessionManager.Instance?.LeaveGame();
    }

}
