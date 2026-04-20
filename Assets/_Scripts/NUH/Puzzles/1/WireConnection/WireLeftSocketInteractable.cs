using UnityEngine;

public class WireLeftSocketInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [SerializeField] private WireConnectionPuzzle ownerPuzzle;
    [SerializeField] private int interactableId;
    [SerializeField] private string promptText = "좌측 전선 선택";

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

        ownerPuzzle.OnLeftSocketPressed(InteractableId, actor);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}
