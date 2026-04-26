using UnityEngine;

/// <summary>
/// 루트 퍼즐 아래 자식 조작물을 공통 식별하기 위한 인터페이스
/// PlayerInteraction / PlayerController가 퍼즐 종류를 몰라도
/// 같은 방식으로 자식 상호작용 대상을 찾을 수 있게 해준다.
/// </summary>
public interface IChildPuzzleInteractable
{
    /// <summary>
    /// 루트 퍼즐 내부에서 이 자식 조작물을 식별하는 고유 ID
    /// </summary>
    int InteractableId { get; }
}
