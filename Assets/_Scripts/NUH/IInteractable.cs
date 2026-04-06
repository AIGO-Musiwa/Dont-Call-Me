
/// <summary>
/// 상호작용 대상 공통 규약
/// </summary>
public interface IInteractable
{
    bool CanInteract(PlayerController actor);
    void Interact(PlayerController actor);
    string GetPromptText(PlayerController actor);
}