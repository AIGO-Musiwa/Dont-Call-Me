using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 제어 버튼 상호작용.
/// 
/// 현재 역할
/// - Backspace 버튼만 지원한다.
/// </summary>
public class FinalCodeControlButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("참조")]
    [SerializeField] private FinalCodePuzzle ownerPuzzle; // 이 버튼이 소속된 최종 코드 퍼즐 본체

    [Header("버튼 설정")]
    [SerializeField] private int interactableId; // 자식 상호작용 식별 ID

    [Header("프롬프트")]
    [SerializeField] private string backspacePrompt = "한 글자 삭제"; // 삭제 버튼 프롬프트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    public int InteractableId => interactableId;

    /// <summary>
    /// 현재 플레이어가 이 삭제 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return false;

        return ownerPuzzle.CanEditInput();
    }

    /// <summary>
    /// 삭제 버튼 상호작용 시 퍼즐 본체에 한 글자 삭제를 요청한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return;

        ownerPuzzle.OnBackspacePressed();

        Log("Backspace 버튼 입력");
    }

    /// <summary>
    /// 현재 프롬프트 문구를 반환한다.
    /// </summary>
    public string GetPromptText(PlayerController actor)
    {
        return backspacePrompt;
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[FinalCodeControlButtonInteractable] {message}", this);
    }
}