using UnityEngine;

/// <summary>
/// 다이얼 퍼즐 좌측 회전 버튼 상호작용 담당
/// 클릭되면 루트 퍼즐에 Left 입력을 전달
/// </summary>
public class DialRotateLeftInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private DialPuzzle ownerPuzzle;    // 소속 루트 퍼즐
    [SerializeField] private int interactableId = 0;    // 자식 상호작용 ID

    [Header("프롬프트")]
    [SerializeField] private string promptText = "좌측 회전";

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

        ownerPuzzle.OnRotateInput(RotationDirection.Left);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}
