using UnityEngine;

public class WalkietalkieItem : ItemObject
{
    protected override void Awake()
    {
        itemType = ItemType.WalkieTalkie;
        isRoleItem = true;
        base.Awake();
    }

    public override string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return "기존 아이템 내려놓고 무전기 줍기";

        return "무전기 줍기";
    }
}
