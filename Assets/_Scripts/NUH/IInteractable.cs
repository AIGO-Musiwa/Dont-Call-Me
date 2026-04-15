/// <summary>
/// 상호작용 대상 공통 규약.
/// 플레이어가 바라보고 좌클릭했을 때 호출될 최소 인터페이스.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 현재 플레이어가 이 대상을 상호작용 가능한지 검사.
    /// </summary>
    bool CanInteract(PlayerController actor);

    /// <summary>
    /// 실제 상호작용 실행.
    /// 서버 권한에서 최종 판정되도록 구현하는 것을 전제로 함.
    /// </summary>
    void Interact(PlayerController actor);

    /// <summary>
    /// 현재 대상 위에 보여줄 프롬프트 문구 반환.
    /// </summary>
    string GetPromptText(PlayerController actor);
}