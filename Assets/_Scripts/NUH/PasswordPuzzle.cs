using Fusion;
using UnityEngine;
using TMPro;

/// <summary>
/// 구제장소 비밀번호 입력 퍼즐
/// IInteractable 구현 — 좌클릭으로 UI 열기
/// 정답 입력 시 RestrainRoom.RPC_UnlockRoom() 호출
///
/// 퍼즐 흐름:
///   플레이어 접근 → 좌클릭 → 번호 입력 UI 표시
///   → TrySubmit() → 정답 → RestrainRoom 잠금 해제
///   → 구제장소 진입 → CapturedPlayer 좌클릭 → 즉시 구출
/// </summary>
public class PasswordPuzzle : NetworkBehaviour, IInteractable
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────
    [Networked] public bool IsSolved { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("퍼즐 설정")]
    [SerializeField] private string correctPassword = "1234";  // 게임 로직에서 동적 생성 가능

    [Header("UI")]
    [SerializeField] private GameObject passwordUI;       // 비밀번호 입력 UI 패널
    [SerializeField] private TMP_InputField inputField;   // 번호 입력 필드
    [SerializeField] private TextMeshProUGUI feedbackText; // 오답 피드백 텍스트

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private RestrainRoom _restrainRoom;
    private bool         _isUIOpen;

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        _restrainRoom = GetComponentInParent<RestrainRoom>();
        CloseUI();
    }

    // ─────────────────────────────────────────
    // IInteractable 구현
    // ─────────────────────────────────────────
    public bool CanInteract(PlayerController actor)
    {
        // 이미 해결됐거나 UI가 열려있으면 상호작용 불가
        return !IsSolved && actor.CurrentState == PlayerState.Normal;
    }

    public void OnInteract(PlayerController actor)
    {
        if (!CanInteract(actor)) return;

        // 로컬 플레이어만 UI 표시
        if (!actor.HasInputAuthority) return;

        OpenUI();
    }

    public string GetPromptText() => IsSolved ? "" : "비밀번호 입력";

    // ─────────────────────────────────────────
    // UI 제어
    // ─────────────────────────────────────────
    private void OpenUI()
    {
        _isUIOpen = true;
        if (passwordUI  != null) passwordUI.SetActive(true);
        if (inputField  != null) inputField.text = string.Empty;
        if (feedbackText != null) feedbackText.text = string.Empty;

        // 커서 잠금 해제 (UI 입력을 위해)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void CloseUI()
    {
        _isUIOpen = false;
        if (passwordUI != null) passwordUI.SetActive(false);

        // 커서 재잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ─────────────────────────────────────────
    // 정답 제출 (UI 확인 버튼에서 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// UI 확인 버튼 OnClick에 연결
    /// </summary>
    public void TrySubmit()
    {
        if (inputField == null) return;

        string input = inputField.text.Trim();

        if (input == correctPassword)
        {
            RPC_SolvePuzzle();
            CloseUI();
        }
        else
        {
            // 오답 피드백
            if (feedbackText != null)
                feedbackText.text = "틀렸습니다.";

            if (inputField != null)
                inputField.text = string.Empty;
        }
    }

    /// <summary>
    /// UI 취소 버튼 OnClick에 연결
    /// </summary>
    public void CancelInput()
    {
        CloseUI();
    }

    // ─────────────────────────────────────────
    // RPC
    // ─────────────────────────────────────────
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SolvePuzzle()
    {
        if (IsSolved) return;

        IsSolved = true;
        _restrainRoom?.RPC_UnlockRoom();

        Debug.Log("[PasswordPuzzle] 비밀번호 정답 — 구제장소 잠금 해제");
    }
}

/// <summary>
/// 구제장소에 묶인 플레이어 컴포넌트
/// IInteractable 구현 — 좌클릭 1회로 즉시 구출
///
/// 부착 위치: 플레이어 GameObject
/// RestrainRoom.IsUnlocked가 true일 때만 상호작용 가능
/// </summary>
public class CapturedPlayer : NetworkBehaviour, IInteractable
{
    // ─────────────────────────────────────────
    // 로컬 참조
    // ─────────────────────────────────────────
    private CaptureSystem _captureSystem;
    private RestrainRoom  _currentRestrainRoom;  // 현재 묶인 구제장소

    public override void Spawned()
    {
        _captureSystem = GetComponent<CaptureSystem>();
    }

    /// <summary>
    /// CaptureSystem이 Restrained 상태 진입 시 호출
    /// 현재 구제장소 참조 설정
    /// </summary>
    public void SetRestrainRoom(RestrainRoom room)
    {
        _currentRestrainRoom = room;
    }

    // ─────────────────────────────────────────
    // IInteractable 구현
    // ─────────────────────────────────────────

    /// <summary>
    /// 구출 가능 조건:
    /// 1. 이 플레이어가 Restrained 상태
    /// 2. 구제장소 비밀번호가 해결됨 (IsUnlocked)
    /// 3. 구출자가 Normal 상태
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (_captureSystem == null) return false;
        if (_captureSystem.CurrentState != PlayerState.Restrained) return false;
        if (actor.CurrentState != PlayerState.Normal) return false;
        if (_currentRestrainRoom == null) return false;

        return _currentRestrainRoom.IsUnlocked;
    }

    /// <summary>
    /// 좌클릭 1회 즉시 구출
    /// 홀드 없음
    /// </summary>
    public void OnInteract(PlayerController actor)
    {
        if (!CanInteract(actor)) return;

        RPC_Rescue();
    }

    public string GetPromptText() => "구출하기";

    // ─────────────────────────────────────────
    // RPC
    // ─────────────────────────────────────────
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Rescue()
    {
        _captureSystem?.OnRescued();
        Debug.Log("[CapturedPlayer] 구출 완료");
    }
}
