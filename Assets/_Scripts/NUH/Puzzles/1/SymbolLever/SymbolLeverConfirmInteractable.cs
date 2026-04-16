using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 확인 버튼 상호작용 스크립트
/// 자식 버튼은 입력만 받고, 실제 판정은 루트 퍼즐(ownerPuzzle)에 위임한다.
/// </summary>
public class SymbolLeverConfirmInteractable : MonoBehaviour, IInteractable
{
    [Header("확인 버튼 설정")]
    [SerializeField] private SymbolLeverPuzzle ownerPuzzle;         // 소속 퍼즐 본체
    [SerializeField] private int interactableId = 100;              // 서버 식별용 상호작용 ID(확인 버튼)

    [Header("프롬프트")]
    [SerializeField] private string confirmPromptText = "정답 확인";

    public int InteractableId => interactableId;

    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (ownerPuzzle.IsSolved)
            return false;

        return true;
    }

    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        // 모든 퍼즐 상호작용 전 오른손 아이템 드랍 규칙
        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ownerPuzzle.ConfirmCurrentState(actor);
    }

    public string GetPromptText(PlayerController actor)
    {
        return confirmPromptText;
    }
}