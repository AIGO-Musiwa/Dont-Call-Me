using UnityEngine;

/// <summary>
/// 전선 퍼즐 우측 소켓 상호작용 담당
/// </summary>
public class WireRightSocketInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [SerializeField] private WireConnectionPuzzle ownerPuzzle;
    [SerializeField] private int interactableId;
    [SerializeField] private string promptText = "우측 전선 연결";

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

        int rightIndex = interactableId - 100;

        ownerPuzzle.OnRightSocketPressed(rightIndex, actor);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}