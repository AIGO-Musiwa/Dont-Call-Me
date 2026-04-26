using UnityEngine;

/// <summary>
/// 생성된 검은 연결선 상호작용 담당
/// leftIndex 기준 연결 해제 요청을 보낸다.
/// </summary>
public class WireConnectionLineInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [SerializeField] private WireConnectionPuzzle ownerPuzzle;
    [SerializeField] private int interactableId;
    [SerializeField] private string promptText = "연결 해제";

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
        int leftIndex = interactableId - 200;
        ownerPuzzle.OnConnectionLinePressed(leftIndex, actor);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}