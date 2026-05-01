using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 개별 숫자 버튼 상호작용 담당.
/// 
/// 역할
/// - 0~9 중 자신이 맡은 InteractableId를 숫자값으로 사용해 루트 퍼즐에 전달한다.
/// - 버튼마다 같은 스크립트를 쓰고 interactableId만 0~9로 다르게 세팅한다.
/// </summary>
public class NumericCodeDigitButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private NumericCodePuzzle ownerPuzzle; // 소속 루트 퍼즐
    [SerializeField] private int interactableId;            // 자식 상호작용 ID이자 입력 숫자값 (0~9)

    [Header("프롬프트")]
    [SerializeField] private string promptText = "숫자 입력";

    public int InteractableId => interactableId;

    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (interactableId < 0 || interactableId > 9)
            return false;

        if (!ownerPuzzle.CanAcceptDigitInput())
            return false;

        return true;
    }

    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        //TODO_Sound - 숫자 코드 버튼 입력
        if (ownerPuzzle != null && ownerPuzzle.audioModule != null)
        {
            ownerPuzzle.audioModule.PlaySound(SoundType.InteractLight);
        }

        ownerPuzzle.OnDigitPressed(interactableId);
    }

    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}