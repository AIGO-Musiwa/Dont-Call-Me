using UnityEngine;

public class FlashLightItem : ItemObject
{
    protected override void Awake()
    {
        itemType = ItemType.Flashlight;
        isRoleItem = true;
        base.Awake();
    }

    public override string GetPromptText(PlayerController actor)
    {
        if (actor != null && actor.NetRightHandItem != null)
            return "기존 아이템 내려놓고 손전등 줍기";

        return "손전등 줍기";
    }
}
