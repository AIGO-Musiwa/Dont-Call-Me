using UnityEngine;

/// <summary>
/// 전선 퍼즐 확인 버튼 상호작용 담당
/// </summary>
public class WireJudgeButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [SerializeField] private WireConnectionPuzzle ownerPuzzle;
    [SerializeField] private int interactableId = 300;
    [SerializeField] private string promptText = "연결 확인";

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

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ownerPuzzle.OnJudgePressed(actor);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}