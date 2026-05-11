using Fusion;
using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐 본체 로직.
/// 
/// 역할
/// - seed 기반으로 미로 데이터를 재구성한다.
/// - 현재 말(Piece) 위치를 관리한다.
/// - 상하좌우 이동 입력을 처리한다.
/// - 목표 위치 도달 시 퍼즐 클리어 처리한다.
/// - 실패 시 퍼즐 상태를 시작 위치로 초기화한다.
/// - solved 상태가 네트워크로 바뀌면 모든 클라이언트에서 화면 전환을 반영한다.
/// - 성공 시 Stage3HintRoot에 배정된 3단계 힌트를 표시할 수 있다.
/// </summary>
public class MazePuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private MazePuzzleView mazeView; // 퍼즐 화면 표시 담당 뷰

    [Header("설정")]
    [SerializeField] private bool showStage3HintImmediately = true; // 성공 직후 바로 3단계 힌트 화면으로 넘길지 여부
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private MazeAnswerGenerator.MazeAnswerData _answerData; // seed 기반으로 재구성한 미로 데이터
    private bool _hasAnswerSeed; // answer seed 적용 완료 여부

    private FinalCodeHintData _stage3HintData; // 이 퍼즐이 표시할 3단계 힌트 데이터
    private bool _hasStage3HintData; // 3단계 힌트 데이터 적용 여부

    [Networked, OnChangedRender(nameof(OnCurrentCellChanged))]
    private int NetCurrentRow { get; set; } // 현재 Piece가 있는 행 인덱스

    [Networked, OnChangedRender(nameof(OnCurrentCellChanged))]
    private int NetCurrentCol { get; set; } // 현재 Piece가 있는 열 인덱스

    public override void Spawned()
    {
        base.Spawned();

        RefreshView();

        if (IsSolved)
            ApplySolvedPresentation();
    }

    /// <summary>
    /// answer seed를 받아 미로 데이터를 재구성한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = MazeAnswerGenerator.Generate(seed);
        _hasAnswerSeed = true;

        if (HasStateAuthority)
        {
            NetCurrentRow = _answerData.StartCell.Row;
            NetCurrentCol = _answerData.StartCell.Col;
        }

        if (mazeView != null)
        {
            mazeView.ResetToDefault();
            mazeView.ApplyInitialState(_answerData.StartCell, _answerData.GoalCell);
        }

        RefreshView();

        if (IsSolved)
            ApplySolvedPresentation();

        Log($"answer seed 적용 완료 | start={_answerData.StartCell} | goal={_answerData.GoalCell}");
    }

    /// <summary>
    /// 이 퍼즐이 성공 후 표시할 3단계 힌트 데이터를 세팅한다.
    /// 데이터는 Networked 값으로 저장되어 Host뿐 아니라 Client 화면에도 동일하게 반영된다.
    /// </summary>
    public void SetStage3HintData(FinalCodeHintData hintData)
    {
        SetNetworkStage3HintData(hintData);
    }

    /// <summary>
    /// Networked Stage3 힌트 데이터가 변경되었을 때 모든 클라이언트에서 호출된다.
    /// </summary>
    protected override void HandleStage3HintDataChanged()
    {
        RefreshStage3HintViewFromNetwork();

        if (IsSolved)
            ApplySolvedPresentation();
    }

    /// <summary>
    /// Networked Stage3 힌트 데이터를 로컬 캐시와 View에 반영한다.
    /// </summary>
    private bool RefreshStage3HintViewFromNetwork()
    {
        if (!TryGetNetworkStage3HintData(out FinalCodeHintData hintData))
        {
            _stage3HintData = null;
            _hasStage3HintData = false;
            return false;
        }

        _stage3HintData = hintData;
        _hasStage3HintData = true;

        if (mazeView != null)
            mazeView.ApplyStage3Hint(_stage3HintData);

        return true;
    }

    /// <summary>
    /// 현재 퍼즐이 방향 입력을 받을 수 있는지 반환한다.
    /// </summary>
    public bool CanAcceptMoveInput()
    {
        if (!_hasAnswerSeed)
            return false;

        if (IsSolved)
            return false;

        return true;
    }

    /// <summary>
    /// 외부 이동 버튼이 호출하는 이동 시도 함수.
    /// </summary>
    public void TryMove(MazeMoveDirection direction)
    {
        if (!HasStateAuthority)
            return;

        if (!CanAcceptMoveInput())
            return;

        MazeCellCoord current = new MazeCellCoord(NetCurrentRow, NetCurrentCol);
        MazeCellCoord next = GetNextCell(current, direction);

        if (!IsInside(next))
        {
            Log($"이동 실패 | 범위 밖 | current={current} | direction={direction}");
            return;
        }

        if (!CanMove(current, next))
        {
            Log($"이동 실패 | 벽 막힘 | current={current} | next={next}");
            HandleFailedMove();
            return;
        }

        NetCurrentRow = next.Row;
        NetCurrentCol = next.Col;

        RefreshView();

        Log($"이동 성공 | current={current} -> next={next}");

        CheckSolved();
    }

    /// <summary>
    /// 실패 시 퍼즐 상태를 시작 위치로 초기화한다.
    /// </summary>
    private void HandleFailedMove()
    {
        MarkFailed();

        if (_answerData == null)
            return;

        NetCurrentRow = _answerData.StartCell.Row;
        NetCurrentCol = _answerData.StartCell.Col;

        RefreshView();

        Log("실패 처리 | 시작 위치로 리셋");
    }

    /// <summary>
    /// 현재 위치가 목표 위치인지 검사하고, 맞으면 성공 처리한다.
    /// </summary>
    private void CheckSolved()
    {
        if (_answerData == null)
            return;

        MazeCellCoord current = new MazeCellCoord(NetCurrentRow, NetCurrentCol);

        if (current != _answerData.GoalCell)
            return;

        MarkSolved();
        ApplySolvedPresentation();

        Log("퍼즐 성공 | 목표 위치 도달");
    }

    /// <summary>
    /// 성공 후 화면 상태를 전환한다.
    /// </summary>
    private void ApplySolvedPresentation()
    {
        if (mazeView == null)
            return;

        RefreshStage3HintViewFromNetwork();

        if (_hasStage3HintData)
            mazeView.ApplyStage3Hint(_stage3HintData);

        if (showStage3HintImmediately)
        {
            mazeView.ShowStage3HintState();
            return;
        }

        mazeView.ShowSolvedState();
    }

    /// <summary>
    /// solved 상태가 네트워크로 변경되었을 때 모든 클라이언트에서 호출된다.
    /// </summary>
    protected override void HandleSolvedStateChanged()
    {
        if (!IsSolved)
            return;

        ApplySolvedPresentation();
    }

    /// <summary>
    /// 현재 위치 네트워크 값이 바뀌었을 때 Piece 표시를 갱신한다.
    /// </summary>
    private void OnCurrentCellChanged()
    {
        RefreshView();
    }

    /// <summary>
    /// 현재 Piece 위치를 뷰에 반영한다.
    /// </summary>
    private void RefreshView()
    {
        if (mazeView == null)
            return;

        if (!_hasAnswerSeed || _answerData == null)
            return;

        mazeView.MovePieceTo(new MazeCellCoord(NetCurrentRow, NetCurrentCol));
    }

    /// <summary>
    /// 현재 칸에서 다음 칸으로 실제 이동 가능한지 검사한다.
    /// MazeAnswerData 안의 벽 배열을 직접 확인한다.
    /// </summary>
    private bool CanMove(MazeCellCoord current, MazeCellCoord next)
    {
        if (_answerData == null)
            return false; // 정답 데이터 없으면 이동 불가

        // 인접 칸이 아니면 이동 불가
        int rowDiff = Mathf.Abs(current.Row - next.Row);
        int colDiff = Mathf.Abs(current.Col - next.Col);

        if (rowDiff + colDiff != 1)
            return false;

        // 좌우 이동인 경우: VerticalWalls 검사
        if (current.Row == next.Row)
        {
            int wallRow = current.Row; // 같은 행
            int wallCol = Mathf.Max(current.Col, next.Col); // 두 칸 사이 세로 벽 인덱스

            // 벽이 닫혀 있으면 이동 불가, 열려 있으면 이동 가능
            return !_answerData.VerticalWalls[wallRow, wallCol];
        }

        // 상하 이동인 경우: HorizontalWalls 검사
        if (current.Col == next.Col)
        {
            int wallCol = current.Col; // 같은 열
            int wallRow = Mathf.Max(current.Row, next.Row); // 두 칸 사이 가로 벽 인덱스

            // 벽이 닫혀 있으면 이동 불가, 열려 있으면 이동 가능
            return !_answerData.HorizontalWalls[wallRow, wallCol];
        }

        return false; // 그 외 비정상 케이스 방어
    }

    /// <summary>
    /// 현재 칸에서 방향 기준 다음 칸을 계산한다.
    /// </summary>
    private MazeCellCoord GetNextCell(MazeCellCoord current, MazeMoveDirection direction)
    {
        return direction switch
        {
            MazeMoveDirection.Up => new MazeCellCoord(current.Row - 1, current.Col),
            MazeMoveDirection.Down => new MazeCellCoord(current.Row + 1, current.Col),
            MazeMoveDirection.Left => new MazeCellCoord(current.Row, current.Col - 1),
            MazeMoveDirection.Right => new MazeCellCoord(current.Row, current.Col + 1),
            _ => current
        };
    }

    /// <summary>
    /// 해당 셀이 5x5 범위 안인지 검사한다.
    /// </summary>
    private bool IsInside(MazeCellCoord cell)
    {
        return cell.Row >= 0 && cell.Row < 5 && cell.Col >= 0 && cell.Col < 5;
    }

    /// <summary>
    /// 루트 퍼즐 직접 상호작용은 사용하지 않는다.
    /// </summary>
    protected override void ServerInteract(PlayerController actor)
    {
        // 루트 직접 상호작용 없음
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[MazePuzzle] {message}", this);
    }
}