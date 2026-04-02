/// <summary>
/// "상호작용 가능한 대상"이라는 공통 약속(인터페이스)입니다.
///
/// 이 인터페이스를 구현한 오브젝트는
/// InteractionSystem의 레이캐스트에 감지될 수 있습니다.
///
/// 예를 들어:
/// - 아이템
/// - 퍼즐 버튼
/// - 캐비닛 문
/// - 구출 대상 플레이어
/// 같은 것들이 이 인터페이스를 구현하게 됩니다.
/// </summary>
public interface IInteractable
{
/// <summary>
/// 지금 이 플레이어(actor)가 이 오브젝트와 상호작용 가능한지 검사합니다.
///
/// 왜 필요한가?
/// - 아이템이 이미 다른 사람이 들고 있으면 집을 수 없고
/// - 문이 잠겨 있으면 열 수 없고
/// - 구출 대상이 아니면 구출할 수 없기 때문입니다.
///
/// InteractionSystem은 이 함수를 매 프레임 확인해서
/// 상호작용 프롬프트를 띄울지 말지 판단합니다.
/// </summary>
bool CanInteract(PlayerController actor);
    /// <summary>
    /// 실제 상호작용을 실행합니다.
    ///
    /// 보통 좌클릭 1회 시 호출됩니다.
    /// 예:
    /// - 아이템이면 줍기
    /// - 버튼이면 누르기
    /// - 구출 대상이면 구출하기
    /// </summary>
    void OnInteract(PlayerController actor);
    /// <summary>
    /// 화면에 표시할 상호작용 텍스트를 돌려줍니다.
    /// 예: "집기", "문 열기", "구출하기"
    /// </summary>
    string GetPromptText();
}