using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐의 개별 레버 상호작용 스크립트
/// 플레이어가 클릭하면 부모 SymbolLeverPuzzle에 자기 인덱스를 전달
/// </summary>
public class SymbolLeverInteractable : PuzzleInteractableBase
{
    [Header("레버 설정")]
    [SerializeField] private SymbolLeverPuzzle ownerPuzzle;         // 소속 퍼즐 본체
    [SerializeField] private int leverIndex;                        // 이 레버의 인덱스


    [Header("프롬프트")]
    [SerializeField] private string leverPromptText = "레버 조작";  // 레버 전용 프롬프트


    /// <summary>
    /// 퍼즐 본체와 인덱스가 있어야 상호작용 가능
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
    /// 레버 클릭 시 퍼즐 본체에 토글 요청
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        if (ownerPuzzle == null)
            return;

        ownerPuzzle.ToggleLever(leverIndex);
    }

    public override string GetPromptText(PlayerController actor)
    {
        return leverPromptText;
    }
}
