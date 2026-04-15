using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 정답 확인 버튼 상호작용 스크립트
/// 플레이어가 클릭하면 부모 SymbolLeverPuzzle에 정답 확인 요청을 보냄
/// </summary>
public class SymbolLeverConfirmInteractable : PuzzleInteractableBase
{
    [Header("확인 버튼 설정")]
    [SerializeField] private SymbolLeverPuzzle ownerPuzzle;             //소속 퍼즐 본체

    [Header("프롬프트")]
    [SerializeField] private string confirmPromptText = "정답 확인";    // 확인 버튼 전용 프롬프트



    /// <summary>
    /// 퍼즐 본체가 있어야 상호작용 가능
    /// 이미 클리어된 퍼즐은 확인 불가
    /// </summary>
    protected override bool CanInteractInternal(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return false;

        if (ownerPuzzle.IsSolved)
            return false;

        return true;
    }

    /// <summary>
    /// 확인 버튼 클릭 시 퍼즐 본체에 판정 요청
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return;

        ownerPuzzle.ConfirmCurrentState(actor);
    }

    public override string GetPromptText(PlayerController actor)
    {
        return confirmPromptText;
    }
}
