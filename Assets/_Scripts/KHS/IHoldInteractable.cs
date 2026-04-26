/// <summary>
/// 꾹 눌러서(Hold) 상호작용하는 대상(라디오 수리 등)을 위한 확장 인터페이스
/// </summary>
public interface IHoldInteractable : IInteractable
{
    /// <summary>
    /// 플레이어가 누르고 있는 동안 매 프레임 서버에서 호출됨
    /// </summary>
    void OnHoldInteract(PlayerController actor, float deltaTime);
}