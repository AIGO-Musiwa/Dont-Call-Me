using UnityEngine;

/// <summary>
/// 구제구역 키패드 숫자 버튼 상호작용 담당.
/// 
/// 역할
/// - 버튼마다 digitValue(0~9)를 하나씩 가진다.
/// - 클릭되면 같은 Zone의 RescueZonePuzzle 본체에 숫자 입력을 전달한다.
/// - 버튼 세트가 여러 개 있어도 ownerPuzzle만 같으면 정상 동작한다.
/// </summary>
public class RescueZoneDigitButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("설정")]
    [SerializeField] private RescueZonePuzzle ownerPuzzle; // 소속 퍼즐 본체
    [SerializeField] private int interactableId;           // 자식 상호작용 식별 ID
    [SerializeField] private int digitValue;               // 이 버튼이 전달할 숫자값 (0~9)

    [Header("프롬프트")]
    [SerializeField] private string promptText = "숫자 입력"; // HUD 프롬프트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false; // 디버그 로그 출력 여부

    public int InteractableId => interactableId; // PlayerController가 자식 버튼을 식별할 때 사용

    /// <summary>
    /// 현재 플레이어가 이 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (digitValue < 0 || digitValue > 9)
            return false;

        if (!ownerPuzzle.CanAcceptDigitInput())
            return false;

        return true;
    }

    /// <summary>
    /// 숫자 버튼 클릭 시 퍼즐 본체에 숫자를 전달한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        // 기존 퍼즐 버튼 규칙에 맞춰, 오른손 아이템을 들고 있으면 먼저 드랍
        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        ownerPuzzle.SubmitDigit(digitValue);
        Log($"숫자 입력 전달 | InteractableId={interactableId} | Digit={digitValue}");
    }

    /// <summary>
    /// HUD에 표시할 프롬프트 문구를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return promptText;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[RescueZoneDigitButtonInteractable] {message}", this);
    }
}