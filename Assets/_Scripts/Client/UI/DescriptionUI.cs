using UnityEngine;

public class DescriptionUI : MonoBehaviour
{
    [Header("설명 UI Panel")]
    [SerializeField] private GameObject descriptionPanel;

    void Start()
    {
        descriptionPanel.SetActive(false);
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
