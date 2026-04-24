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

    // IME 누출 방지용 스냅샷
    private string nicknameSnapshot;
    private string roomCodeSnapshot;
    private bool pendingImeGuard;

    private void Start()
    {
        errorPanel.SetActive(false);

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

        FocusNickname();
    }

    private void OnDestroy()
    {
        if (_launcher == null) return;

        // 이벤트 구독 해제
        _launcher.OnJoinFailed -= HandleConnectionFailed;
        _launcher.OnPlayerJoinedEvent -= HandlePlayerJoined;

        roomCodeInput.onValidateInput -= ValidateRoomCodeInput;
    }

    private void Update()
    {
        HandleTabNavigation();
    }

    private void LateUpdate()
    {
        CheckImeGuard();
    }

    #region InputField Tab 순환처리

    private void HandleTabNavigation()
    {
        if (!Keyboard.current.tabKey.wasPressedThisFrame) return;

        if (nicknameInput.isFocused)
            FocusRoomCode();
        else if (roomCodeInput.isFocused)
            FocusNickname();
        else
            FocusNickname();
    }

    private void FocusNickname()
    {
        nicknameInput.ActivateInputField();
        nicknameInput.Select();
    }

    private void FocusRoomCode()
    {
        // 포커스 전환 직전 두 필드를 모두 스냅샷으로 저장
        nicknameSnapshot = nicknameInput.text;
        roomCodeSnapshot = roomCodeInput.text;
        pendingImeGuard = true;

        roomCodeInput.ActivateInputField();
        roomCodeInput.Select();
    }

    private void CheckImeGuard()
    {
        if (!pendingImeGuard) return;
        pendingImeGuard = false;

        if (roomCodeInput.text != roomCodeSnapshot)
        {
            nicknameInput.text = nicknameSnapshot;  // 닉네임 복원
            roomCodeInput.text = roomCodeSnapshot;  // 코드 스냅샷으로 복원 (누출 문자 제거)
        }
    }

    #endregion

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
            if (runner.IsServer)
                runner.LoadScene(SceneRef.FromIndex(SceneNames.LOBBY_INDEX));
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
