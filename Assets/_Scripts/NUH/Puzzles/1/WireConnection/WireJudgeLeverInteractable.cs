using UnityEngine;

/// <summary>
/// 전선 퍼즐 Confirm 레버 상호작용 담당.
/// 기존 버튼 대신 레버 프리팹을 눌러 판정을 요청한다.
/// </summary>
public class WireJudgeLeverInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [SerializeField] private WireConnectionPuzzle ownerPuzzle; // 소속 전선 퍼즐
    [SerializeField] private int interactableId = 300; // 자식 상호작용 ID
    [SerializeField] private string promptText = "연결 확인"; // HUD 프롬프트

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

        if (ownerPuzzle.IsJudgeInputLocked)
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