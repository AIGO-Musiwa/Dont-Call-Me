using Fusion;
using UnityEngine;

/// <summary>
/// 퍼즐 상호작용 공통 베이스
/// 
/// 역할
/// -플레이어 생존 상태 확인
/// -서버 권한에서만 퍼즐 판정
/// -규칙에 따라 오른손 아이템 먼저 드랍
/// -이후 퍼즐 고유 로직 실행
/// </summary>
public abstract class PuzzleInteractableBase : NetworkBehaviour, IInteractable
{
    [Header("퍼즐 상호작용")]
    [SerializeField] private bool dropRightHandBeforeInteract = true;

    public virtual bool CanInteract(PlayerController actor)
    {
        if (actor == null) return false;
        if (actor.NetPlayerState != PlayerState.Alive) return false;
        return CanInteractInternal(actor);
    }

    public void Interact(PlayerController actor)
    {
        if (!HasStateAuthority) return;
        if (actor == null) return;
        if (!CanInteract(actor)) return;

        // 오른손에 아이템이 있으면 퍼즐 상호작용 전에 먼저 드랍
        if (dropRightHandBeforeInteract && actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ServerInteract(actor);
    }

    public virtual string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return "오른손 아이템 내려놓고 퍼즐 상호작용";

        return "퍼즐 상호작용";
    }
    
    /// <summary>
    /// 퍼즐별 추가 상호작용 가능 조건
    /// </summary>
    /// <param name="actor"></param>
    /// <returns></returns>
    protected virtual bool CanInteractInternal(PlayerController actor)
    {
        return true;
    }

    /// <summary>
    /// 실제 퍼즐 고유 판정
    /// 반드시 서버 권한에서만 실행됨
    /// </summary>
    /// <param name="actor"></param>
    protected abstract void ServerInteract(PlayerController actor);
}
