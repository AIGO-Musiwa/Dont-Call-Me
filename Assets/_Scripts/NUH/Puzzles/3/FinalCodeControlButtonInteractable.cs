using UnityEngine;

/// <summary>
/// 3단계 최종 코드 퍼즐의 제어 버튼 상호작용.
/// 
/// 현재 역할
/// - Backspace 버튼만 지원한다.
/// - 플레이어가 상호작용하면 FinalCodeButtonView 눌림 애니메이션을 재생한다.
/// </summary>
public class FinalCodeControlButtonInteractable : MonoBehaviour, IInteractable, IChildPuzzleInteractable
{
    [Header("참조")]
    [SerializeField] private FinalCodePuzzle ownerPuzzle; // 이 버튼이 소속된 최종 코드 퍼즐 본체
    [SerializeField] private FinalCodeButtonView buttonView; // 버튼 눌림 애니메이션 담당 View

    [Header("버튼 설정")]
    [SerializeField] private int interactableId; // 자식 상호작용 식별 ID

    [Header("프롬프트")]
    [SerializeField] private string backspacePrompt = "한 글자 삭제"; // 삭제 버튼 프롬프트

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
    /// 현재 플레이어가 이 삭제 버튼과 상호작용 가능한지 검사한다.
    /// </summary>
    public bool CanInteract(PlayerController actor)
    {
        if (actor == null)
            return false;

        if (ownerPuzzle == null)
            return false;

        if (actor.NetPlayerState != PlayerState.Normal)
            return false;

        return ownerPuzzle.CanEditInput();
    }

    /// <summary>
    /// 삭제 버튼 상호작용 시 버튼 애니메이션을 재생하고,
    /// 퍼즐 본체에 한 글자 삭제를 요청한다.
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
            LogWarning("buttonView가 없어 지우기 버튼 애니메이션을 재생하지 못함");
        }

        //TODO_Sound - 최종 코드 지우기 버튼 입력
        if (ownerPuzzle != null && ownerPuzzle.audioModule != null)
        {
            ownerPuzzle.audioModule.PlaySound(SoundType.InteractLight);
        }

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

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[FinalCodeControlButtonInteractable] {message}", this);
    }
}