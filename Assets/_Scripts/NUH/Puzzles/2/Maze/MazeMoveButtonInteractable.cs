using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐의 방향 버튼 상호작용 담당.
/// 
/// 역할
/// - 위/아래/왼쪽/오른쪽 버튼 입력을 MazePuzzle에 전달한다.
/// - 버튼별로 같은 스크립트를 사용하고, moveDirection만 다르게 설정한다.
/// </summary>
public class MazeMoveButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private MazePuzzle ownerPuzzle;              // 소속 루트 퍼즐
    [SerializeField] private int interactableId;                  // 자식 상호작용 ID
    [SerializeField] private MazeMoveDirection moveDirection;     // 이 버튼이 전달할 이동 방향

    [Header("프롬프트")]
    [SerializeField] private string promptText = "이동";          // 상호작용 안내 문구

    public int InteractableId => interactableId; // 외부에서 읽는 상호작용 ID

    /// <summary>
    /// 현재 플레이어가 이 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false; // 플레이어 참조 없으면 불가

        if (ownerPuzzle == null)
            return false; // 퍼즐 참조 없으면 불가

        if (actor.NetPlayerState != PlayerState.Normal)
            return false; // 일반 생존 상태가 아니면 불가

        if (!ownerPuzzle.CanAcceptMoveInput())
            return false; // 퍼즐이 입력을 받을 수 없는 상태면 불가

        return true; // 모든 조건 통과 시 상호작용 가능
    }

    /// <summary>
    /// 버튼 상호작용 시 해당 방향 입력을 퍼즐 본체로 전달한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return; // 상호작용 불가 상태면 종료

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem(); // 오른손 아이템 들고 있으면 먼저 드랍

        ownerPuzzle.TryMove(moveDirection); // 퍼즐에 이동 방향 전달
    }

    /// <summary>
    /// 상호작용 프롬프트 문구를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return promptText; // 현재 버튼의 프롬프트 문구 반환
    }
}