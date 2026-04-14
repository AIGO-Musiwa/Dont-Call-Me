using TMPro;
using UnityEngine;

public class PlayerResultSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject filledGroup;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private TextMeshProUGUI stateText;

    private static readonly Color colorEscaped = new Color(0.3f, 0.9f, 0.3f);
    private static readonly Color colorDead = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color colorDefault = Color.white;

    public void SetPlayer(string nickname, PlayerState state, bool isLocalPlayer = false)
    {
        filledGroup.SetActive(true);
        nicknameText.text = nickname;
        nicknameText.color = isLocalPlayer ? Color.yellow : Color.white;

        switch (state)
        {
            case PlayerState.Escaped:
                stateText.text = "탈출";
                stateText.color = colorEscaped;
                break;
            case PlayerState.Dead:
                stateText.text = "사망";
                stateText.color = colorDead;
                break;
            default:
                Debug.LogWarning("탈출 조건에 문제 생김");
                break;
        }
    }

    public void SetEmpty()
    {
        filledGroup.SetActive(false);
    }
}
