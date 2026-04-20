using TMPro;
using UnityEngine;

public class PlayerResultSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject filledGroup;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private GameObject escapedImage;
    [SerializeField] private GameObject deadImage;


    public void SetPlayer(string nickname, PlayerState state, bool isLocalPlayer = false)
    {
        filledGroup.SetActive(true);
        nicknameText.text = nickname;

        switch (state)
        {
            case PlayerState.Escaped:
                escapedImage.SetActive(true);
                deadImage.SetActive(false);
                break;
            case PlayerState.Dead:
                deadImage.SetActive(true);
                escapedImage.SetActive(false);
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
