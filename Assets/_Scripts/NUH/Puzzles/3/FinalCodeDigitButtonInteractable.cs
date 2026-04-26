using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 숫자 버튼 상호작용.
/// 
/// 역할
/// - interactableId 값을 그대로 입력 숫자(0~9)로 사용한다.
/// - 플레이어가 상호작용하면 FinalCodePuzzle에 숫자 입력을 전달한다.
/// </summary>
public class FinalCodeDigitButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("참조")]
    [SerializeField] private FinalCodePuzzle ownerPuzzle; // 이 버튼이 소속된 최종 코드 퍼즐 본체

    [Header("버튼 설정")]
    [SerializeField] private int interactableId; // 자식 상호작용 식별 ID이자 실제 입력 숫자값(0~9)

    [Header("프롬프트")]
    [SerializeField] private string promptPrefix = "숫자 입력"; // 프롬프트 앞부분

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    public int InteractableId => interactableId;

    /// <summary>
    /// 현재 플레이어가 이 숫자 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return false;

        if (interactableId < 0 || interactableId > 9)
            return false;

        return ownerPuzzle.CanAcceptDigitInput();
    }

    /// <summary>
    /// 숫자 버튼 상호작용 시 퍼즐 본체에 숫자 입력을 전달한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return;

        if (interactableId < 0 || interactableId > 9)
            return;

        ownerPuzzle.OnDigitPressed(interactableId);

        Log($"숫자 버튼 입력 | value={interactableId}");
    }

    /// <summary>
    /// 현재 프롬프트 문구를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return $"{promptPrefix} {interactableId}";
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[FinalCodeDigitButtonInteractable] {message}", this);
    }
}