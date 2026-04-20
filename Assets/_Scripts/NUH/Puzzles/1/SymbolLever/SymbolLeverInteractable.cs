using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 개별 레버 상호작용 스크립트
/// 자식 레버 조작물은 입력만 받고,
/// 실제 상태 변경은 루트 퍼즐(ownerPuzzle)에 위임한다.
/// </summary>
public class SymbolLeverInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("레버 설정")]
    [SerializeField] private SymbolLeverPuzzle ownerPuzzle;         // 소속 퍼즐 본체
    [SerializeField] private int interactableId;                    // 서버 식별용 상호작용 ID(레버 0~5)

    [Header("프롬프트")]
    [SerializeField] private string leverPromptText = "레버 조작";

    public int InteractableId => interactableId;

    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (ownerPuzzle.IsLeverAlreadyPulled(InteractableId))
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

        ownerPuzzle.OnLeverPulled(interactableId);
    }

    public string GetPromptText(PlayerController actor)
    {
        return leverPromptText;
    }
}