using UnityEngine;

/// <summary>
/// 구제구역 키패드 Confirm 버튼 상호작용 담당.
/// 
/// 역할
/// - 클릭되면 RescueZonePuzzle 본체에 Confirm 입력을 전달한다.
/// - 현재 구조에서는 Confirm만 존재하므로 enum 분기 없이 단일 기능만 담당한다.
/// </summary>
public class RescueZoneControlButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private RescueZonePuzzle ownerPuzzle; // 소속 퍼즐 본체
    [SerializeField] private int interactableId;           // 자식 상호작용 식별 ID

    [Header("프롬프트")]
    [SerializeField] private string promptText = "확인";   // HUD 프롬프트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;  // 디버그 로그 출력 여부

    public int InteractableId => interactableId; // PlayerController가 자식 버튼을 식별할 때 사용

    /// <summary>
    /// 현재 플레이어가 이 Confirm 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (!ownerPuzzle.CanAcceptConfirmInput())
            return false;

        return true;
    }

    /// <summary>
    /// Confirm 버튼 클릭 시 퍼즐 본체에 판정 요청을 보낸다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        // 기존 퍼즐 버튼 규칙에 맞춰, 오른손 아이템을 들고 있으면 먼저 드랍
        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ownerPuzzle.ConfirmInput();
        Log($"Confirm 입력 전달 | InteractableId={interactableId}");
    }

    /// <summary>
    /// HUD에 표시할 프롬프트 문구를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RescueZoneControlButtonInteractable] {message}", this);
    }
}