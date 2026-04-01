using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 타이틀 씬 전용 UI 컨트롤러
public class TitleManager : MonoBehaviour
{
    [Header("공통")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_Text       errorText;

    [Header("방 생성")]
    [SerializeField] private Button createRoomButton;

    [Header("방 참가")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button         joinRoomButton;

    [Header("로딩 패널 (연결 중 표시)")]
    [SerializeField] private GameObject loadingPanel;

    // 닉네임 미입력 시 랜덤 생성에 사용할 글자 목록
    private static readonly string[] NameSyllables =
    {
        "안", "시", "우", "이", "래", "호", "유", "정", "남", "의",
        "현", "김", "현", "수", "박", "건", "영", "찬", "진", "채",
        "동", "규", "장", "운"
    };

    // ── 생명주기 ───────────────────────────────────────────────
    private void Start()
    {
        errorText.text = "";
        loadingPanel.SetActive(false);

        NetworkManager.Instance.OnJoinFailed += ShowError;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnJoinFailed -= ShowError;
    }

    // ── 버튼 이벤트 (Inspector에서 연결) ─────────────────────
    public void OnClickCreateRoom()
    {
        SetLoading(true);
        NetworkManager.Instance.CreateRoom(GetOrGenerateNickname());
    }

    public void OnClickJoinRoom()
    {
        if (string.IsNullOrWhiteSpace(roomCodeInput.text))
        {
            ShowError("방 코드를 입력해주세요.");
            return;
        }

        SetLoading(true);
        NetworkManager.Instance.JoinRoom(GetOrGenerateNickname(), roomCodeInput.text.Trim());
    }

    // ── 내부 유틸 ─────────────────────────────────────────────

    // 닉네임 입력 없으면 랜덤 3글자 생성
    private string GetOrGenerateNickname()
    {
        if (!string.IsNullOrWhiteSpace(nicknameInput.text))
            return nicknameInput.text.Trim();

        var rng  = new System.Random();
        var name = new System.Text.StringBuilder();
        for (int i = 0; i < 3; i++)
            name.Append(NameSyllables[rng.Next(NameSyllables.Length)]);
        return name.ToString();
    }

    private void ShowError(string message)
    {
        SetLoading(false);
        errorText.text = message;
    }

    private void SetLoading(bool isLoading)
    {
        loadingPanel.SetActive(isLoading);
        createRoomButton.interactable = !isLoading;
        joinRoomButton.interactable   = !isLoading;
        errorText.text = "";
    }
}
