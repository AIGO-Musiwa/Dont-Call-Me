using UnityEngine;

public class FlashLightItem : ItemObject
{
    protected override void Awake()
    {
        itemType = ItemType.Flashlight;
        isRoleItem = false;     //일단은 기본 아이템화 처리!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!나중에바꿔야함
        base.Awake();
    }

    public override string GetPromptText(PlayerController actor)
    {
        if (actor != null && (actor.NetRightHandItem != null || actor.NetLeftHandItem == null))
            return "기존 아이템 내려놓고 손전등 줍기";

        return "손전등 줍기";
    }
}
