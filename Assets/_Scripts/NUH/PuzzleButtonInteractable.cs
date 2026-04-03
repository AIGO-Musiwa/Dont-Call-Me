using Fusion;
using UnityEngine;

/// <summary>
/// 최소 단위 퍼즐 버튼 예제
/// 
/// 상태
/// -NetIsPressed : 버튼이 눌렸는지
/// -NetPressCount : 총 몇 번 눌렸는지
/// 
/// 표현
/// -PuzzleButtonView가 눌린 상태를 읽어 메쉬를 내림
/// </summary>
public class PuzzleButtonInteractable : PuzzleInteractableBase
{
    [Header("버튼 설정")]
    [SerializeField] private bool oneShot = true;
    [SerializeField] private PuzzleButtonView buttonView;

    [Networked, OnChangedRender(nameof(OnPressedChanged))]
    public NetworkBool NetIsPressed { get; private set; }

    [Networked]
    public int NetPressCount { get; private set; }


    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetIsPressed = false;
            NetPressCount = 0;
        }

        ApplyPresentation();
    }

    protected override bool CanInteractInternal(PlayerController actor)
    {
        if (oneShot && NetIsPressed)
            return false;

        return true;
    }

    protected override void ServerInteract(PlayerController actor)
    {
        // 원샷 버튼이면 한 번만 눌림
        if (oneShot && NetIsPressed)
            return;

        NetIsPressed = true;
        NetPressCount++;

        Debug.LogWarning($"[PuzzleButtonInteractable] {name} pressed by {actor.Object.InputAuthority}, Count = {NetPressCount} ");

        // TODO:
        // 문 열기, 발전기 카운트 증가, 퍼즐 매니저 notify 등
        // 서버 판정 로직 연결
    }

    public override string GetPromptText(PlayerController actor)
    {
        if (oneShot && NetIsPressed)
            return "이미 눌린 버튼입니다.";

        if (actor != null && actor.NetRightHandItem != null)
            return "오른손 아이템 내려놓고 버튼 누르기";

        return "버튼 누르기";
    }

    private void OnPressedChanged()
    {
        ApplyPresentation();
    }

    private void ApplyPresentation()
    {
        if (buttonView != null)
            buttonView.SetPressed(NetIsPressed);
    }
}
