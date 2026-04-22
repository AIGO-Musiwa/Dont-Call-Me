using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimelineEventCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timestampText;
    [SerializeField] private TextMeshProUGUI eventTypeText;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private Image cardBackground;

    [Header("카드 색상")]
    [SerializeField] private Color capturedColor = new Color(0.85f, 0.3f, 0.3f, 1f);    // 붉은 색
    [SerializeField] private Color rescuedColor = new Color(0.3f, 0.65f, 0.9f, 1f);     // 푸른 색
    [SerializeField] private Color deadColor = new Color(0.35f, 0.35f, 0.35f, 1f);      // 회 색
    [SerializeField] private Color escapedColor = new Color(0.3f, 0.75f, 0.45f, 1f);    // 초록 색

    [Header("닉네임 색상")]
    [SerializeField] private Color localPlayerColor = new Color(1f, 0.85f, 0.2f, 1f);   // 노란 색
    [SerializeField] private Color defaultColor = Color.white;                          // 흰 색

    public void Setup(GameEventEntry entry, bool isLocalPlayer)
    {
        timestampText.text = FormatTimestamp(entry.Timestamp);
        nicknameText.text = entry.Nickname.ToString();
        nicknameText.color = isLocalPlayer ? localPlayerColor : defaultColor;

        switch (entry.EventType)
        {
            case GameEventType.Captured:
                eventTypeText.text = "포획";
                cardBackground.color = capturedColor;
                break;
            case GameEventType.Rescued:
                eventTypeText.text = "구출";
                cardBackground.color = rescuedColor;
                break;
            case GameEventType.Dead:
                eventTypeText.text = "사망";
                cardBackground.color = deadColor;
                break;
            case GameEventType.Escaped:
                eventTypeText.text = "탈출";
                cardBackground.color = escapedColor;
                break;

        }
    }

    private static string FormatTimestamp(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m} : {s:D2}";
    }
}
