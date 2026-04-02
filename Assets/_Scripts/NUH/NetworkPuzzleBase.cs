using Fusion;
using UnityEngine;

/// <summary>
/// 퍼즐 공통 베이스 클래스
/// 모든 퍼즐 오브젝트가 상속
/// [Networked] 상태 동기화 및 RPC 공통 처리
///
/// 1인칭 근접 조작 방식:
///   퍼즐 전용 카메라 전환 없음
///   플레이어가 직접 근접해서 버튼/레버 등을 좌클릭으로 조작
///
/// 구현체 예시:
///   LeverPuzzle  : NetworkPuzzleBase  — 레버 당기기
///   ButtonPuzzle : NetworkPuzzleBase  — 버튼 순서 맞추기
///   DialPuzzle   : NetworkPuzzleBase  — 다이얼 돌리기
/// </summary>
public abstract class NetworkPuzzleBase : NetworkBehaviour, IInteractable
{
    // ─────────────────────────────────────────
    // 네트워크 동기화 변수
    // ─────────────────────────────────────────
    [Networked, OnChangedRender(nameof(OnPuzzleStateChanged))]
    public int PuzzleState { get; protected set; }  // 퍼즐 종류별 상태값 (자식에서 정의)

    [Networked, OnChangedRender(nameof(OnSolvedChanged))]
    public bool IsSolved { get; protected set; }

    /// <summary>
    /// 마지막으로 상호작용한 플레이어
    /// 퍼즐 연출 등에 활용
    /// </summary>
    [Networked] public PlayerRef LastInteractor { get; private set; }

    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("퍼즐 공통")]
    [SerializeField] private GameObject solvedVisual;    // 해결 시 활성화되는 시각 효과
    [SerializeField] private GameObject unsolvedVisual;  // 미해결 시 표시

    // ─────────────────────────────────────────
    // 이벤트
    // ─────────────────────────────────────────
    public event System.Action<NetworkPuzzleBase> OnPuzzleSolvedEvent;

    // ─────────────────────────────────────────
    // IInteractable 구현
    // ─────────────────────────────────────────
    public virtual bool CanInteract(PlayerController actor)
    {
        return !IsSolved && actor.CurrentState == PlayerState.Normal;
    }

    /// <summary>
    /// 좌클릭 상호작용 — 자식 클래스에서 구체적 퍼즐 로직 구현
    /// </summary>
    public void OnInteract(PlayerController actor)
    {
        if (!CanInteract(actor)) return;
        RPC_Interact(actor.Object.InputAuthority);
    }

    public virtual string GetPromptText() => IsSolved ? "" : "조작하기";

    // ─────────────────────────────────────────
    // RPC
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어 조작 → StateAuthority에서 상태 업데이트
    /// </summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Interact(PlayerRef interactor)
    {
        if (IsSolved) return;

        LastInteractor = interactor;
        HandleInteraction(interactor);
    }

    /// <summary>
    /// 퍼즐 상태 변경 동기화
    /// 자식 클래스에서 RPC_Interact 처리 후 호출
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_UpdateState(int newState)
    {
        PuzzleState = newState;
    }

    // ─────────────────────────────────────────
    // 추상 메서드 — 자식 클래스에서 구현
    // ─────────────────────────────────────────

    /// <summary>
    /// 실제 퍼즐 조작 로직
    /// StateAuthority에서 실행됨
    /// </summary>
    protected abstract void HandleInteraction(PlayerRef interactor);

    /// <summary>
    /// 정답 판정 로직
    /// HandleInteraction 내부에서 호출
    /// </summary>
    protected abstract bool ValidateSolution();

    // ─────────────────────────────────────────
    // 퍼즐 해결 처리
    // ─────────────────────────────────────────

    /// <summary>
    /// ValidateSolution() 통과 시 호출
    /// StateAuthority에서 실행
    /// </summary>
    protected void OnPuzzleSolved()
    {
        if (!Runner.IsServer) return;
        IsSolved = true;
        Debug.Log($"[NetworkPuzzleBase] 퍼즐 해결: {gameObject.name}");
    }

    // ─────────────────────────────────────────
    // [Networked] 변경 감지
    // ─────────────────────────────────────────
    protected virtual void OnPuzzleStateChanged()
    {
        // 자식 클래스에서 오버라이드 — 상태 변화에 따른 시각 처리
    }

    private void OnSolvedChanged()
    {
        if (solvedVisual   != null) solvedVisual.SetActive(IsSolved);
        if (unsolvedVisual != null) unsolvedVisual.SetActive(!IsSolved);

        if (IsSolved)
            OnPuzzleSolvedEvent?.Invoke(this);
    }
}

// ─────────────────────────────────────────────────────────────────
// 구현체 예시 — 레버 퍼즐
// ─────────────────────────────────────────────────────────────────

/// <summary>
/// 레버 퍼즐 구현 예시
/// 두 레버를 모두 올리면 해결
/// </summary>
public class LeverPuzzle : NetworkPuzzleBase
{
    // PuzzleState 비트 플래그로 레버 상태 표현
    // bit 0 = 레버 1, bit 1 = 레버 2
    private const int LEVER_1 = 1 << 0;
    private const int LEVER_2 = 1 << 1;
    private const int ALL_LEVERS = LEVER_1 | LEVER_2;

    [Header("레버 트랜스폼")]
    [SerializeField] private Transform[] leverTransforms;  // 레버 시각적 회전용

    public override string GetPromptText() => IsSolved ? "" : "레버 조작";

    protected override void HandleInteraction(PlayerRef interactor)
    {
        // 가장 가까운 레버 토글 (실제 구현에서는 레이캐스트 히트 정보로 판별)
        // 여기서는 단순화를 위해 순서대로 토글
        int newState = PuzzleState;

        if ((newState & LEVER_1) == 0)
            newState |= LEVER_1;
        else if ((newState & LEVER_2) == 0)
            newState |= LEVER_2;

        RPC_UpdateState(newState);

        if (ValidateSolution())
            OnPuzzleSolved();
    }

    protected override bool ValidateSolution()
    {
        return (PuzzleState & ALL_LEVERS) == ALL_LEVERS;
    }

    protected override void OnPuzzleStateChanged()
    {
        // 레버 시각적 회전 처리
        if (leverTransforms == null) return;

        for (int i = 0; i < leverTransforms.Length; i++)
        {
            if (leverTransforms[i] == null) continue;
            bool isOn = (PuzzleState & (1 << i)) != 0;
            leverTransforms[i].localRotation = Quaternion.Euler(isOn ? -45f : 45f, 0f, 0f);
        }
    }
}
