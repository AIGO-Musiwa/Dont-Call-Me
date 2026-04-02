using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어의 왼손/오른손 슬롯을 관리합니다.
///
/// 규칙:
/// - 왼손: 자기 역할 아이템 전용
/// - 오른손: 일반 아이템, 타인의 역할 아이템
///
/// 이 클래스가 하는 일:
/// - 아이템을 어느 손에 넣을지 결정
/// - 손 소켓에 아이템 붙이기
/// - 드랍 처리
/// - 오른손 아이템 탈취 처리
/// - 원주인 역할 아이템 회수 처리
/// </summary>
public class HandSystem : NetworkBehaviour
{
    // 현재 왼손/오른손에 들고 있는 네트워크 아이템
    [Networked] public NetworkObject LeftHandItem { get; private set; }
    [Networked] public NetworkObject RightHandItem { get; private set; }

    [Header("손 소켓 (아이템 부착 위치)")]
    [SerializeField] private Transform leftHandSocket;
    [SerializeField] private Transform rightHandSocket;

    private RoleSystem _roleSystem;
    private PlayerController _playerController;

    public override void Spawned()
    {
        _roleSystem = GetComponent<RoleSystem>();
        _playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// 아이템 집기의 시작점.
    ///
    /// 판단 순서:
    /// 1. 역할 아이템이고 원주인이면 왼손으로 강제 회수
    /// 2. 내 역할 아이템이면 왼손
    /// 3. 나머지는 오른손
    /// </summary>
    public void PickupItem(ItemObject item)
    {
        if (item == null) return;
        if (item.IsRoleItem && item.IsOriginalOwner(Object.InputAuthority))
        {
            RPC_ForceToLeftHand(item.Object);
            return;
        }
        bool isMyRoleItem = _roleSystem != null && _roleSystem.IsMyRoleItem(item);
        if (isMyRoleItem)
            EquipToSlot(item, HandSlot.Left);
        else
            EquipToSlot(item, HandSlot.Right);
    }

    /// <summary>
    /// 지정된 손 슬롯에 장착합니다.
    /// 이미 아이템이 있으면 먼저 떨어뜨립니다.
    /// </summary>
    private void EquipToSlot(ItemObject item, HandSlot slot)
    {
        if (slot == HandSlot.Left && LeftHandItem != null) DropItem(HandSlot.Left);
        if (slot == HandSlot.Right && RightHandItem != null) DropItem(HandSlot.Right);
        if (slot == HandSlot.Left)
        {
            LeftHandItem = item.Object;
            item.transform.SetParent(leftHandSocket);
        }
        else
        {
            RightHandItem = item.Object;
            item.transform.SetParent(rightHandSocket);
        }
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.OnEquipped(_playerController, slot);
    }

    /// <summary>
    /// 특정 손 슬롯 아이템을 드랍합니다.
    /// </summary>
    public void DropItem(HandSlot slot)
    {
        NetworkObject target = slot == HandSlot.Left ? LeftHandItem : RightHandItem;
        if (target == null) return;
        ItemObject item = target.GetComponent<ItemObject>();
        if (item != null)
        {
            item.transform.SetParent(null);
            item.OnDropped();
        }
        if (slot == HandSlot.Left) LeftHandItem = null;
        else RightHandItem = null;
    }

    /// <summary>
    /// 포획 시 양손 아이템 모두 드랍.
    /// </summary>
    public void DropAllItems()
    {
        DropItem(HandSlot.Left);
        DropItem(HandSlot.Right);
    }
    /// <summary>
    /// 다른 플레이어 오른손 아이템을 탈취합니다.
    /// 왼손은 절대 탈취하지 않습니다.
    /// </summary>
    public void TryStealFrom(PlayerController target)
    {
        HandSystem targetHand = target.GetComponent<HandSystem>();
        if (targetHand == null) return;
        if (targetHand.RightHandItem == null) return;
        ItemObject stolenItem = targetHand.RightHandItem.GetComponent<ItemObject>();
        if (stolenItem == null) return;
        // 대상의 오른손에서 먼저 제거
        targetHand.RPC_DropRightHand();
        // 내 손으로 가져옴
        PickupItem(stolenItem);
    }

    /// <summary>
    /// 역할 아이템 원주인이 회수할 때,
    /// 현재 들고 있는 사람의 오른손에서 빼고 원주인 왼손으로 강제 이동시킵니다.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ForceToLeftHand(NetworkObject itemObj)
    {
        ItemObject item = itemObj.GetComponent<ItemObject>();
        if (item == null) return;
        if (item.CurrentHolder != PlayerRef.None)
        {
            NetworkObject holderObj = Runner.GetPlayerObject(item.CurrentHolder);
            if (holderObj != null)
            {
                HandSystem holderHand = holderObj.GetComponent<HandSystem>();
                holderHand?.DropItem(HandSlot.Right);
            }
        }
        EquipToSlot(item, HandSlot.Left);
    }

    /// <summary>
    /// 내가 탈취당할 때 내 오른손 아이템을 드랍시키는 RPC.
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]

    public void RPC_DropRightHand()
    {
        DropItem(HandSlot.Right);
    }

    public bool HasItem(HandSlot slot)
    => slot == HandSlot.Left ? LeftHandItem != null : RightHandItem != null;

    public NetworkObject GetItem(HandSlot slot)
    => slot == HandSlot.Left ? LeftHandItem : RightHandItem;
}
public enum HandSlot
{
    Left,
    Right
}