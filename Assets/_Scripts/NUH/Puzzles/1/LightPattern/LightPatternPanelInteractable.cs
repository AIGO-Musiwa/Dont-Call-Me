using UnityEngine;

/// <summary>
/// 점등 패턴 퍼즐의 개별 패널 상호작용 담당
/// 자신의 패널 인덱스(= interactableId)를 루트 퍼즐에 전달한다.
/// </summary>
public class LightPatternPanelInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private LightPatternPuzzle ownerPuzzle; // 소속 루트 퍼즐
    [SerializeField] private int interactableId;             // 패널 인덱스이자 자식 상호작용 ID (0~8)

    [Header("프롬프트")]
    [SerializeField] private string promptText = "패널 입력"; // 플레이어에게 보여줄 상호작용 문구

    /// <summary>
    /// 현재 패널의 고유 인덱스 반환
    /// PlayerController가 자식 상호작용 대상을 식별할 때 사용
    /// </summary>
    public int InteractableId => interactableId;

    /// <summary>
    /// 현재 플레이어가 이 패널과 상호작용 가능한지 검사
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false; // 상호작용 주체 없음

        if (ownerPuzzle == null)
            return false; // 루트 퍼즐 연결 안 됨

        if (actor.NetPlayerState != PlayerState.Normal)
            return false; // 생존 상태가 아니면 입력 불가

        if (ownerPuzzle.IsSolved)
            return false; // 이미 클리어한 퍼즐은 입력 불가

        return true;
    }

    /// <summary>
    /// 실제 상호작용 처리
    /// 오른손 아이템을 먼저 떨구고, 패널 인덱스를 루트 퍼즐에 전달
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem(); // 퍼즐 상호작용 전 오른손 아이템 드랍

        ownerPuzzle.OnPanelPressed(interactableId);
    }

    /// <summary>
    /// HUD에 표시할 프롬프트 반환
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }
}