using UnityEngine;

/// <summary>
/// 다이얼 퍼즐 우측 회전 버튼 상호작용 담당.
/// 클릭되면 루트 퍼즐에 Right 입력을 전달한다.
/// </summary>
public class DialRotateRightInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private DialPuzzle ownerPuzzle;      // 소속 루트 퍼즐
    [SerializeField] private int interactableId = 1;      // 자식 상호작용 ID

    [Header("프롬프트")]
    [SerializeField] private string promptText = "우측 회전"; // 상호작용 문구

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

        if (ownerPuzzle.IsDialUnlocked)
            return false;

        return true;
    }

    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ownerPuzzle.OnRotateInput(RotationDirection.Right);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}