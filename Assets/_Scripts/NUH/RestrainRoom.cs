using Fusion;
using UnityEngine;

/// <summary>
/// 구제장소 잠금/해제 상태 관리
/// PasswordPuzzle이 해결되면 isUnlocked = true
/// CapturedPlayer(IInteractable)의 CanInteract에서 isUnlocked 확인
/// </summary>
public class RestrainRoom : NetworkBehaviour
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────
    [Networked, OnChangedRender(nameof(OnUnlockStateChanged))]
    public bool IsUnlocked { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("구제장소 설정")]
    [SerializeField] private GameObject lockedVisual;    // 잠긴 상태 시각 표시 (자물쇠 등)
    [SerializeField] private GameObject unlockedVisual;  // 해제 상태 시각 표시
    [SerializeField] private Collider   roomBarrier;     // 진입 차단 콜라이더 (잠긴 상태)

    // ─────────────────────────────────────────
    // 외부 참조
    // ─────────────────────────────────────────
    private PasswordPuzzle _puzzle;

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        _puzzle = GetComponentInChildren<PasswordPuzzle>();

        // 초기 잠금 상태
        IsUnlocked = false;
        ApplyLockVisual(false);
    }

    // ─────────────────────────────────────────
    // 잠금 해제 (PasswordPuzzle에서 호출)
    // ─────────────────────────────────────────

    /// <summary>
    /// PasswordPuzzle.TrySubmit()에서 정답 확인 후 호출
    /// StateAuthority에서만 실행
    /// </summary>
    public void RPC_UnlockRoom()
    {
        if (!Runner.IsServer) return;
        if (IsUnlocked)       return;

        IsUnlocked = true;
        Debug.Log("[RestrainRoom] 구제장소 잠금 해제");
    }

    // ─────────────────────────────────────────
    // 시각 처리
    // ─────────────────────────────────────────
    private void OnUnlockStateChanged()
    {
        ApplyLockVisual(IsUnlocked);
    }

    private void ApplyLockVisual(bool unlocked)
    {
        if (lockedVisual   != null) lockedVisual.SetActive(!unlocked);
        if (unlockedVisual != null) unlockedVisual.SetActive(unlocked);
        if (roomBarrier    != null) roomBarrier.enabled = !unlocked;  // 해제 시 진입 가능
    }
}
