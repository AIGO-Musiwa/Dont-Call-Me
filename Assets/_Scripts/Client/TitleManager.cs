using Fusion;
using Photon.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private GameObject titlePanel;

    [Header("닉네임 입력")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("방 코드 입력")]
    [SerializeField] private TMP_InputField roomCodeInput;

    [Header("버튼")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;

    [Header("에러 패널")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    private GameLauncher _launcher;

    private void Start()
    {
        _launcher = GameLauncher.Instance;
        if (_launcher == null)
        {
            Debug.LogError("[TitleUI] GameLauncher가 씬에 없습니다.");
            return;
        }
        
        // 이벤트 구독
        _launcher.OnJoinFailed += HandleConnectionFailed;
        _launcher.OnPlayerJoinedEvent += HandlePlayerJoined;
    }

    private void OnDestroy()
    {
        if (_launcher == null) return;

        // 이벤트 구독 해제
        _launcher.OnJoinFailed -= HandleConnectionFailed;
        _launcher.OnPlayerJoinedEvent -= HandlePlayerJoined;
    }

    #region 버튼 콜백 (Inspector에서 연결)

    // 방 생성 버튼 클릭
    public void OnCreateRoomClicked()
    {
        if (!TryGetValidNickname(out string nickname)) return;
        _launcher.CreateRoom(nickname);
    }

    // 방 참가 버튼 클릭
    public void OnJoinRoomClicked()
    {
        if (!TryGetValidNickname(out string nickname)) return;

        string roomCode = roomCodeInput.text.Trim().ToUpper();
        if (roomCode.Length != Constants.ROOM_CODE_LENGTH)
        {
            ShowError($"방 코드는 {Constants.ROOM_CODE_LENGTH}자리입니다.");
            return;
        }

        _launcher.JoinRoom(nickname, roomCode);
    }

    #endregion

    #region 이벤트 처리

    // 플레이어 입장 이벤트 처리
    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            Hide();
        }
    }

    // 방 참가/생성 실패 이벤트 처리
    private void HandleConnectionFailed(string reason)
    {
        ShowError(reason);
    }

    #endregion

    #region 외부 공개 메서드

    // Title 씬 메뉴 보이기/숨기기
    public void Show() => titlePanel.SetActive(true);
    public void Hide() => titlePanel.SetActive(false);

    #endregion

    #region 내부 유틸

    // 닉네임 유효성 검사 및 없을 시 랜덤 닉네임 생성
    private bool TryGetValidNickname(out string nickname)
    {
        nickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            nickname = GenerateRandomNickname();
            nicknameInput.text = nickname;
        }

        else if (nickname.Length > Constants.NICKNAME_MAX_LENGTH)
        {
            ShowError($"닉네임은 {Constants.NICKNAME_MAX_LENGTH}자 이하로 입력해주세요.");
            return false;
        }
        return true;
    }

    // 랜덤 닉네임을 위한 글자들
    private static readonly string[] NameSyllables =
    {
        "안", "시", "우", "이", "래", "호", "유", "정", "남", "의",
        "현", "김", "현", "수", "박", "건", "영", "찬", "진", "채",
        "동", "규", "장", "운"
    };

    // 랜덤 닉네임 생성 (3글자)
    private static string GenerateRandomNickname()
    {
        var sb = new System.Text.StringBuilder(3);
        for (int i = 0; i< 3; i++)
        {
            sb.Append(NameSyllables[Random.Range(0, NameSyllables.Length)]);
        }
        return sb.ToString();
    }

    // 에러 창 띄우기
    private void ShowError(string message)
    {
        errorText.text = message;
        errorPanel.SetActive(true);
    }

    // 에러 창 끄기
    public void OnErrorConfirmClicked()
    {
        errorPanel.SetActive(false);
    }

    #endregion
}
