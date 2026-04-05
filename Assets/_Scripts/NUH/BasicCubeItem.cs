using UnityEngine;

public class BasicCubeItem : ItemObject
{
    protected override void Awake()
    {
        itemType = ItemType.BasicCube;
        base.Awake();
    }

    public override string GetPromptText(PlayerController actor)
    {
        return "기본 큐브 줍기";
    }
    
}
