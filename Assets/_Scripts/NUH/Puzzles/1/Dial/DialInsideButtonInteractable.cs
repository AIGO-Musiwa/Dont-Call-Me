using UnityEngine;

/// <summary>
/// 금고 내부 버튼 상호작용 담당.
/// 다이얼 해제 후 이 버튼을 누르면 퍼즐이 최종 클리어된다.
/// </summary>
public class DialInsideButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private DialPuzzle ownerPuzzle;      // 소속 다이얼 퍼즐
    [SerializeField] private int interactableId = 2;      // 자식 상호작용 ID

    [Header("프롬프트")]
    [SerializeField] private string promptText = "버튼 누르기"; // 상호작용 문구

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

        if (!ownerPuzzle.IsDialUnlocked)
            return false;

        if (ownerPuzzle.IsInsideButtonPressed)
            return false;

        return true;
    }

    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ownerPuzzle.ServerPressInsideButton(actor);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}