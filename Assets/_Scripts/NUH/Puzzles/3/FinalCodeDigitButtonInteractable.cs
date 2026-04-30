using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 숫자 버튼 상호작용.
/// 
/// 역할
/// - interactableId 값을 그대로 입력 숫자(0~9)로 사용한다.
/// - 플레이어가 상호작용하면 FinalCodeButtonView 눌림 애니메이션을 재생한다.
/// - FinalCodePuzzle에 숫자 입력을 전달한다.
/// </summary>
public class FinalCodeDigitButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("참조")]
    [SerializeField] private FinalCodePuzzle ownerPuzzle; // 이 버튼이 소속된 최종 코드 퍼즐 본체
    [SerializeField] private FinalCodeButtonView buttonView; // 버튼 눌림 애니메이션 담당 View

    [Header("버튼 설정")]
    [SerializeField] private int interactableId; // 자식 상호작용 식별 ID이자 실제 입력 숫자값(0~9)

    [Header("프롬프트")]
    [SerializeField] private string promptPrefix = "숫자 입력"; // 프롬프트 앞부분

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    public int InteractableId => interactableId;

    private void Awake()
    {
        // 인스펙터에서 buttonView 연결을 깜빡했을 때 부모/자식에서 자동 탐색한다.
        if (buttonView == null)
            buttonView = GetComponentInParent<FinalCodeButtonView>();

        if (buttonView == null)
            buttonView = GetComponentInChildren<FinalCodeButtonView>();
    }

    /// <summary>
    /// 현재 플레이어가 이 숫자 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        if (interactableId < 0 || interactableId > 9)
            return false;

        return ownerPuzzle.CanAcceptDigitInput();
    }

    /// <summary>
    /// 숫자 버튼 상호작용 시 버튼 애니메이션을 재생하고,
    /// 퍼즐 본체에 숫자 입력을 전달한다.
    /// </summary>
    public void Interact(PlayerController actor)
    {
        if (!CanInteract(actor))
            return;

        if (actor.NetRightHandItem != null)
            actor.ServerDropRightHandItem();

        if (buttonView != null)
        {
            buttonView.PlayPress();
        }
        else
        {
            LogWarning($"buttonView가 없어 버튼 애니메이션을 재생하지 못함 | value={interactableId}");
        }

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

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[FinalCodeDigitButtonInteractable] {message}", this);
    }
}