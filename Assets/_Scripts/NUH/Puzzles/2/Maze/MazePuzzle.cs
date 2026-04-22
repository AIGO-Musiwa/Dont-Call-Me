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
/// </summary>
public class MazePuzzle : PuzzleInteractableBase, IPuzzleSeedReceiver
{
    [Header("참조")]
    [SerializeField] private MazePuzzleView mazeView; // 퍼즐 화면 표시 담당 뷰

    [Header("설정")]
    [SerializeField] private bool showStage3HintImmediately = true; // 성공 직후 바로 3단계 힌트 화면으로 넘길지 여부
    [SerializeField] private bool enableDebugLog = true;            // 디버그 로그 출력 여부

    private MazeAnswerGenerator.MazeAnswerData _answerData; // seed 기반으로 재구성한 미로 데이터
    private bool _hasAnswerSeed;                            // answer seed 적용 완료 여부

    [Networked, OnChangedRender(nameof(OnCurrentCellChanged))]
    private int NetCurrentRow { get; set; } // 현재 Piece가 있는 행 인덱스

    [Networked, OnChangedRender(nameof(OnCurrentCellChanged))]
    private int NetCurrentCol { get; set; } // 현재 Piece가 있는 열 인덱스

    public override void Spawned()
    {
        base.Spawned(); // 부모 기본 Spawned 로직 실행

        RefreshView();  // 현재 네트워크 상태 기준으로 뷰 갱신

        if (IsSolved)
            ApplySolvedPresentation(); // 이미 solved 상태로 스폰되었으면 성공 화면 반영
    }

    /// <summary>
    /// answer seed를 받아 미로 데이터를 재구성한다.
    /// 시작 위치/목표 위치를 뷰에 반영하고 현재 위치를 초기화한다.
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        _answerData = MazeAnswerGenerator.Generate(seed); // seed 기반 미로 데이터 생성
        _hasAnswerSeed = true;                            // 시드 적용 완료 표시

        if (HasStateAuthority)
        {
            NetCurrentRow = _answerData.StartCell.Row; // 현재 행을 시작 위치로 초기화
            NetCurrentCol = _answerData.StartCell.Col; // 현재 열을 시작 위치로 초기화
        }

        if (mazeView != null)
        {
            mazeView.ResetToDefault(); // 뷰를 기본 상태로 초기화
            mazeView.ApplyInitialState(_answerData.StartCell, _answerData.GoalCell); // 시작/목표 위치 반영
        }

        RefreshView(); // 현재 위치 기준 Piece 표시 갱신

        if (IsSolved)
            ApplySolvedPresentation(); // seed 적용 시 이미 solved 상태면 성공 화면 다시 반영

        Log($"answer seed 적용 완료 | start={_answerData.StartCell} | goal={_answerData.GoalCell}");
    }

    /// <summary>
    /// 현재 퍼즐이 방향 입력을 받을 수 있는지 반환한다.
    /// 버튼 interactable이 이 값을 참고한다.
    /// </summary>
    public bool CanAcceptMoveInput()
    {
        if (!_hasAnswerSeed)
            return false; // seed가 없으면 입력 불가

        if (IsSolved)
            return false; // 클리어 후 입력 불가

        return true; // 그 외에는 입력 가능
    }

    /// <summary>
    /// 외부 이동 버튼이 호출하는 이동 시도 함수.
    /// </summary>
    public void TryMove(MazeMoveDirection direction)
    {
        if (!HasStateAuthority)
            return; // 상태 권한이 있는 쪽만 실제 이동 처리

        if (!CanAcceptMoveInput())
            return; // 입력 가능 상태가 아니면 무시

        MazeCellCoord current = new MazeCellCoord(NetCurrentRow, NetCurrentCol); // 현재 위치 좌표
        MazeCellCoord next = GetNextCell(current, direction);                     // 방향 기준 다음 좌표 계산

        if (!IsInside(next))
        {
            Log($"이동 실패 | 범위 밖 | current={current} | direction={direction}");
            return; // 맵 범위 밖이면 이동 불가
        }

        if (!CanMove(current, next))
        {
            Log($"이동 실패 | 벽 막힘 | current={current} | next={next}");
            HandleFailedMove(); // 벽에 막혀 있으면 실패 처리
            return;
        }

        NetCurrentRow = next.Row; // 현재 행 갱신
        NetCurrentCol = next.Col; // 현재 열 갱신

        RefreshView(); // 이동 후 Piece 표시 갱신

        Log($"이동 성공 | current={current} -> next={next}");

        CheckSolved(); // 목표 도달 여부 검사
    }

    /// <summary>
    /// 실패 시 퍼즐 상태를 시작 위치로 초기화한다.
    /// Reset 버튼 없이 실패할 때만 리셋하는 규칙을 따른다.
    /// </summary>
    private void HandleFailedMove()
    {
        MarkFailed(); // 퍼즐 실패 이벤트 기록

        if (_answerData == null)
            return; // 방어 코드

        NetCurrentRow = _answerData.StartCell.Row; // 현재 행을 시작 위치로 되돌림
        NetCurrentCol = _answerData.StartCell.Col; // 현재 열을 시작 위치로 되돌림

        RefreshView(); // 초기 위치로 뷰 갱신

        Log("실패 처리 | 시작 위치로 리셋");
    }

    /// <summary>
    /// 현재 위치가 목표 위치인지 검사하고, 맞으면 성공 처리한다.
    /// </summary>
    private void CheckSolved()
    {
        if (_answerData == null)
            return; // 미로 데이터 없으면 검사 불가

        MazeCellCoord current = new MazeCellCoord(NetCurrentRow, NetCurrentCol); // 현재 위치 좌표

        if (current != _answerData.GoalCell)
            return; // 목표 위치가 아니면 종료

        MarkSolved(); // 퍼즐 성공 상태를 네트워크에 반영

        // 권한 쪽은 즉시 연출 반영
        ApplySolvedPresentation();

        Log("퍼즐 성공 | 목표 위치 도달");
    }

    /// <summary>
    /// 성공 후 화면 상태를 전환한다.
    /// </summary>
    private void ApplySolvedPresentation()
    {
        if (mazeView == null)
            return; // 뷰 없으면 종료

        if (showStage3HintImmediately)
        {
            mazeView.ShowStage3HintState(); // 바로 3단계 힌트 화면으로 전환
            return;
        }

        mazeView.ShowSolvedState(); // 성공 표시 화면으로 전환
    }

    /// <summary>
    /// solved 상태가 네트워크로 변경되었을 때 모든 클라이언트에서 호출된다.
    /// Host/Client 관계없이 성공 화면 전환을 동일하게 반영한다.
    /// </summary>
    protected override void HandleSolvedStateChanged()
    {
        if (!IsSolved)
            return; // solved가 아닌 상태 변화는 무시

        ApplySolvedPresentation(); // 성공 화면 반영
    }

    /// <summary>
    /// 현재 위치 네트워크 값이 바뀌었을 때 Piece 표시를 갱신한다.
    /// </summary>
    private void OnCurrentCellChanged()
    {
        RefreshView(); // 현재 위치 기준 뷰 갱신
    }

    /// <summary>
    /// 현재 Piece 위치를 뷰에 반영한다.
    /// </summary>
    private void RefreshView()
    {
        if (mazeView == null)
            return; // 뷰 없으면 종료

        if (!_hasAnswerSeed || _answerData == null)
            return; // 시드 미적용 상태면 종료

        mazeView.MovePieceTo(new MazeCellCoord(NetCurrentRow, NetCurrentCol)); // 현재 위치로 Piece 이동
    }

    /// <summary>
    /// 현재 셀과 다음 셀 사이에 벽이 없는지 검사한다.
    /// </summary>
    private bool CanMove(MazeCellCoord current, MazeCellCoord next)
    {
        if (_answerData == null)
            return false; // 미로 데이터 없으면 이동 불가

        if (current.Row == next.Row)
        {
            int row = current.Row;                           // 같은 행
            int wallCol = Mathf.Max(current.Col, next.Col); // 두 셀 사이 세로 벽 인덱스
            return !_answerData.VerticalWalls[row, wallCol]; // 세로 벽이 열려 있어야 이동 가능
        }

        if (current.Col == next.Col)
        {
            int col = current.Col;                          // 같은 열
            int wallRow = Mathf.Max(current.Row, next.Row); // 두 셀 사이 가로 벽 인덱스
            return !_answerData.HorizontalWalls[wallRow, col]; // 가로 벽이 열려 있어야 이동 가능
        }

        return false; // 대각 이동 같은 비정상 입력은 이동 불가
    }

    /// <summary>
    /// 현재 좌표와 이동 방향을 기준으로 다음 셀 좌표를 계산한다.
    /// </summary>
    private MazeCellCoord GetNextCell(MazeCellCoord current, MazeMoveDirection direction)
    {
        return direction switch
        {
            MazeMoveDirection.Up => new MazeCellCoord(current.Row - 1, current.Col),    // 위쪽 칸
            MazeMoveDirection.Down => new MazeCellCoord(current.Row + 1, current.Col),  // 아래쪽 칸
            MazeMoveDirection.Left => new MazeCellCoord(current.Row, current.Col - 1),  // 왼쪽 칸
            MazeMoveDirection.Right => new MazeCellCoord(current.Row, current.Col + 1), // 오른쪽 칸
            _ => current // 방어용: 알 수 없는 방향이면 현재 위치 유지
        };
    }

    /// <summary>
    /// 좌표가 5x5 범위 안에 있는지 검사한다.
    /// </summary>
    private bool IsInside(MazeCellCoord coord)
    {
        if (_answerData == null)
            return false; // 미로 데이터 없으면 false

        return coord.Row >= 0 &&
               coord.Row < _answerData.GridSize &&
               coord.Col >= 0 &&
               coord.Col < _answerData.GridSize; // 0 <= row,col < gridSize
    }

    /// <summary>
    /// 루트 퍼즐 직접 상호작용은 사용하지 않는다.
    /// 버튼 상호작용으로만 이동을 받는다.
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
            return; // 로그 꺼져 있으면 종료

        Debug.Log($"[MazePuzzle] {message}", this); // 퍼즐 디버그 로그 출력
    }
}