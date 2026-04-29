using Fusion;
using Photon.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────
    [Header("패널")]
    [SerializeField] private GameObject mainPanel;          // 타이틀 메인
    [SerializeField] private GameObject roomPanel;          // 방 생성/입장 패널

    [Header("닉네임 입력")]
    [SerializeField] private TMP_InputField nicknameInput;

    [Header("방 코드 입력")]
    [SerializeField] private TMP_InputField roomCodeInput;

    [Header("에러 패널")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    private GameLauncher _launcher;
    private string confirmedNickname;       // 확정된 닉네임

    private void Start()
    {
        errorPanel.SetActive(false);
        ShowMain();

        _launcher = GameLauncher.Instance;
        if (_launcher == null)
        {
            Debug.LogError("[TitleUI] GameLauncher가 씬에 없습니다.");
            return;
        }
        
        // 이벤트 구독
        _launcher.OnJoinFailed += HandleConnectionFailed;
        _launcher.OnPlayerJoinedEvent += HandlePlayerJoined;

        // 타이틀 복귀 시 커서 복원
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!string.IsNullOrEmpty(_launcher.PendingErrorMessage))
        {
            ShowError(_launcher.PendingErrorMessage);
            _launcher.PendingErrorMessage = null;
        }

        // roomCode 입력 필터 등록 (영어 + 6글자)
        roomCodeInput.onValidateInput += ValidateRoomCodeInput;
        roomCodeInput.characterLimit = Constants.ROOM_CODE_LENGTH;

        nicknameInput.ActivateInputField();
        nicknameInput.Select();
    }

    private void OnDestroy()
    {
        if (_launcher == null) return;

        // 이벤트 구독 해제
        _launcher.OnJoinFailed -= HandleConnectionFailed;
        _launcher.OnPlayerJoinedEvent -= HandlePlayerJoined;

        roomCodeInput.onValidateInput -= ValidateRoomCodeInput;
    }

    #region roomCode 입력 필터

    private char ValidateRoomCodeInput(string text, int charIndex, char addedChar)
    {
        // 영문자는 대문자로 강제 변환
        if (addedChar >= 'a' && addedChar <= 'z')
            return (char)(addedChar - 32);

        // 대문자 영문 또는 숫자만 허용
        if ((addedChar >= 'A' && addedChar <= 'Z') ||
            (addedChar >= '0' && addedChar <= '9'))
            return addedChar;

        // 그 외 차단
        return '\0';
    }

    #endregion

    #region 버튼 콜백 (Inspector에서 연결)

    // 타이틀 메인 - 시작 버튼
    public void OnStartClicked()
    {
        confirmedNickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(confirmedNickname))
        {
            confirmedNickname = GenerateRandomNickname();
            nicknameInput.text = confirmedNickname;
        }
        else if (confirmedNickname.Length > Constants.NICKNAME_MAX_LENGTH)
        {
            ShowError($"닉네임은 {Constants.NICKNAME_MAX_LENGTH}자 이하로 입력해주세요.");
            return;
        }

        ShowRoom();
    }

    // 방 생성/입장 패널 - 방 생성 버튼
    public void OnCreateRoomClicked()
    {
        _launcher.CreateRoom(confirmedNickname);
    }

    // 방 생성/입장 패널 - 방 참가 버튼
    public void OnJoinRoomClicked()
    {
        string roomCode = roomCodeInput.text.Trim().ToUpper();
        if (roomCode.Length != Constants.ROOM_CODE_LENGTH)
        {
            ShowError($"방 코드는 {Constants.ROOM_CODE_LENGTH}자리입니다.");
            return;
        }

        _launcher.JoinRoom(confirmedNickname, roomCode);
    }

    // 방 생성/입장 패널 - 뒤로가기 버튼
    public void OnBackClicked()
    {
        ShowMain();
    }

    // 에러 창 끄기
    public void OnErrorConfirmClicked()
    {
        errorPanel.SetActive(false);
    }

    #endregion

    #region 이벤트 처리

    // 플레이어 입장 이벤트 처리
    private void HandlePlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer && runner.IsServer)
            runner.LoadScene(SceneRef.FromIndex(SceneNames.LOBBY_INDEX));
    }

    // 방 참가/생성 실패 이벤트 처리
    private void HandleConnectionFailed(string reason)
    {
        ShowError(reason);
    }

    #endregion

    #region 패널 전환

    private void ShowMain()
    {
        mainPanel.SetActive(true);
        roomPanel.SetActive(false);
    }

    private void ShowRoom()
    {
        mainPanel.SetActive(false);
        roomPanel.SetActive(true);
        roomCodeInput.text = string.Empty;
    }

    #endregion

    #region 내부 유틸

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

    #endregion
}
