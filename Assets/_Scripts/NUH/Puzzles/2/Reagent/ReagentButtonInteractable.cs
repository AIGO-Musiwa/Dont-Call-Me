using UnityEngine;

/// <summary>
/// 2-3 시약 제조 퍼즐의 버튼 상호작용 담당.
/// 
/// 역할
/// - 좌 / 우 / 선택 / 제조 / 가열 / 냉각 버튼 입력을
///   ReagentCraftPuzzle 본체로 전달한다.
/// 
/// 주의
/// - 버튼 프롬프트/가능 판정은 ownerPuzzle의 "Networked 상태 기반" 함수로 읽는다.
/// - 실제 상태 변경은 서버 권한에서만 일어난다.
/// </summary>
public class ReagentButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private ReagentCraftPuzzle ownerPuzzle; // 소속 퍼즐 본체
    [SerializeField] private int interactableId; // 자식 상호작용 ID
    [SerializeField] private ReagentButtonType buttonType; // 현재 버튼 타입

    [Header("프롬프트")]
    [SerializeField] private string promptText = "조작"; // 상호작용 프롬프트 문구

    public int InteractableId => interactableId; // 외부에서 읽는 자식 상호작용 ID

    /// <summary>
    /// 현재 플레이어가 이 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
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

        return buttonType switch
        {
            ReagentButtonType.Previous => ownerPuzzle.CanEditCurrentSlot(),
            ReagentButtonType.Next => ownerPuzzle.CanEditCurrentSlot(),
            ReagentButtonType.Confirm => ownerPuzzle.CanEditCurrentSlot(),
            ReagentButtonType.StartCraft => ownerPuzzle.CanStartCraft(),
            ReagentButtonType.Heat => ownerPuzzle.CanRecordActionInput(),
            ReagentButtonType.Cool => ownerPuzzle.CanRecordActionInput(),
            _ => false
        };
    }

    /// <summary>
    /// 버튼 상호작용 시 버튼 타입에 맞는 퍼즐 본체 함수를 호출한다.
    /// 
    /// 전제
    /// - 이 함수는 PlayerController 상호작용 파이프라인을 통해 서버 쪽에서 호출되는 것이 맞다.
    /// - ownerPuzzle 내부에서 HasStateAuthority 검사를 다시 하기 때문에 안전하다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        if (actor != null && actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem(); // 버튼 조작 전 오른손 아이템 드랍

        switch (buttonType)
        {
            case ReagentButtonType.Previous:
                ownerPuzzle.MoveCurrentSlotSelectionLeft();
                break;

            case ReagentButtonType.Next:
                ownerPuzzle.MoveCurrentSlotSelectionRight();
                break;

            case ReagentButtonType.Confirm:
                ownerPuzzle.ConfirmCurrentSlotSelection();
                break;

            case ReagentButtonType.StartCraft:
                ownerPuzzle.TryStartCraft();
                break;

            case ReagentButtonType.Heat:
                ownerPuzzle.TryRecordHeatInput();
                break;

            case ReagentButtonType.Cool:
                ownerPuzzle.TryRecordCoolInput();
                break;
        }
    }

    /// <summary>
    /// 현재 버튼의 상호작용 프롬프트를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}