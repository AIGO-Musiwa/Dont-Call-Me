using UnityEngine;
using UnityEngine.InputSystem;

public class DescriptionUI : MonoBehaviour
{
    [Header("설명 UI Panel")]
    [SerializeField] private GameObject descriptionPanel;

    void Start()
    {
        descriptionPanel.SetActive(false);
    }

    private void Update()
    {
        if(descriptionPanel.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseDescriptionPanel();
    }

    public void ShowDescriptionPanel()
    {
        descriptionPanel.SetActive(true);
    }

    public void CloseDescriptionPanel()
    {
        descriptionPanel.SetActive(false);
    }
}
