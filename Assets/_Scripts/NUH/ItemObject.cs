using Fusion;
using UnityEngine;

/// <summary>
/// 모든 아이템의 공통 베이스 클래스입니다.
/// 손전등, 무전기, 나중의 퍼즐용 일반 아이템도
/// 기본적으로는 이 클래스를 상속받아 사용하게 됩니다.
/// 
/// 이 클래스의 핵심 책임:
/// - 아이템이 바닥에 있는지 / 누가 들고 있는지 관리
/// - 역할 아이템인지 아닌지 구분
/// - 집기 / 장착 / 드랍 공통 처리
/// </summary>
public class ItemObject : NetworkBehaviour, IInteractable
{
    /// <summary>
    /// 이 아이템의 "원래 주인".
    /// 역할 아이템(손전등/무전기)일 때만 의미가 있습니다.
    /// 일반 아이템이면 None일 수 있습니다.
    /// </summary>
    [Networked] public PlayerRef OriginalOwner { get; set; }

    /// <summary>
    /// 현재 이 아이템을 들고 있는 플레이어.
    /// 바닥에 떨어져 있으면 PlayerRef.None입니다.
    /// </summary>
    [Networked] public PlayerRef CurrentHolder { get; set; }

    /// <summary>
    /// 역할 아이템인지 여부.
    /// RoleSystem에서 스폰할 때 true로 설정합니다.
    /// </summary>
    [Networked] public bool IsRoleItem { get; set; }

    // 물리 관련 컴포넌트 캐시
    protected Rigidbody _rb;
    protected Collider _col;

    public override void Spawned()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
    }

    /// <summary>
    /// 바닥에 떨어져 있을 때만 집을 수 있습니다.
    /// 이미 누가 들고 있으면 상호작용 불가입니다.
    /// </summary>
    public virtual bool CanInteract(PlayerController actor)
    {
        return CurrentHolder == PlayerRef.None;
    }

    /// <summary>
    /// 플레이어가 아이템을 집는 기본 동작.
    /// 실제 어느 손에 갈지는 HandSystem이 판단합니다.
    /// </summary>
    public virtual void OnInteract(PlayerController actor)
    {
        if (!CanInteract(actor)) return;
        HandSystem handSystem = actor.GetComponent<HandSystem>();
        if (handSystem == null) return;
        handSystem.PickupItem(this);
    }

    public virtual string GetPromptText() => "집기";

    /// <summary>
    /// 손에 장착될 때 호출됩니다.
    ///
    /// 여기서는 공통 처리만 합니다.
    /// - 현재 소유자 기록
    /// - 물리 꺼서 손에 고정
    /// </summary>
    public virtual void OnEquipped(PlayerController actor, HandSlot slot)
    {
        CurrentHolder = actor.Object.InputAuthority;
        if (_rb != null) _rb.isKinematic = true;
        if (_col != null) _col.enabled = false;
    }

    /// <summary>
    /// 바닥에 드랍될 때 호출됩니다.
    /// - 현재 소유자 해제
    /// - 물리 다시 켬
    /// </summary>
    public virtual void OnDropped()
    {
        CurrentHolder = PlayerRef.None;
        if (_rb != null) _rb.isKinematic = false;
        if (_col != null) _col.enabled = true;
    }

    /// <summary>
    /// 이 플레이어가 이 아이템의 원주인인지 검사합니다.
    /// 역할 아이템 회수 규칙에 사용됩니다.
    /// </summary>
    public bool IsOriginalOwner(PlayerRef playerRef)
    {
        return IsRoleItem && OriginalOwner == playerRef;
    }
}