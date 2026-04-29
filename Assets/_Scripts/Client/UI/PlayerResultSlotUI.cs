using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerResultSlotUI : MonoBehaviour
{
    [Header("결과 화면 Player Slot UI")]
    [SerializeField] private GameObject filledGroup;
    [SerializeField] private TextMeshProUGUI nicknameText;
    [SerializeField] private GameObject escapedImage;
    [SerializeField] private GameObject deadImage;
    [SerializeField] private GameObject hostMark;
    [SerializeField] private Image characterFaceImage;

    [Header("닉네임 색상")]
    [SerializeField] private Color localPlayerColor = new Color(1f, 0.85f, 0.2f, 1f); // 노란색
    [SerializeField] private Color defaultColor = Color.white;

    public void SetPlayer(string nickname, PlayerState state, bool isLocalPlayer = false,
        bool isHost = false, Sprite faceSprite = null)
    {
        filledGroup.SetActive(true);

        // 본인 닉네임 색상 변경
        nicknameText.text = nickname;
        nicknameText.color = isLocalPlayer ? localPlayerColor : defaultColor;

        // 방장 표시
        if (hostMark != null)
            hostMark.SetActive(isHost);

        // 캐릭터 얼굴 스프라이트 적용
        if (characterFaceImage != null)
        {
            characterFaceImage.sprite = faceSprite;
            characterFaceImage.gameObject.SetActive(faceSprite != null);
        }

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
                Debug.LogWarning($"[PlayerResultSlotUI] 예상치 못한 상태: {state}. Dead로 폴백.");
                deadImage.SetActive(true);
                escapedImage.SetActive(false);
                break;
        }
    }

    public void SetEmpty()
    {
        filledGroup.SetActive(false);
    }
}
