using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐의 화면 표시 담당.
/// 
/// 역할
/// - 시작 위치에 PlayerPiece를 배치한다.
/// - 목표 위치에 TargetMarker를 배치한다.
/// - 현재 Piece 위치를 셀 기준으로 갱신한다.
/// - 성공 시 화면 루트를 전환한다.
/// </summary>
public class MazePuzzleView : MonoBehaviour
{
    [Header("셀 참조")]
    [SerializeField] private List<Transform> cellTransforms = new(); // 5x5 셀 위치 참조 목록 (총 25개)

    [Header("표시 오브젝트")]
    [SerializeField] private Transform playerPiece;  // 현재 플레이어 말 표시 오브젝트
    [SerializeField] private Transform targetMarker; // 목표 위치 표시 오브젝트

    [Header("표시 오프셋")]
    [SerializeField] private float playerPieceOffset = 0.001f; // 셀 기준 PlayerPiece를 앞으로 띄울 거리
    [SerializeField] private float targetMarkerOffset = 0.002f; // 셀 기준 TargetMarker를 앞으로 띄울 거리

    [Header("상태 루트")]
    [SerializeField] private GameObject monitorRoot;     // 기본 퍼즐 화면 루트
    [SerializeField] private GameObject solvedRoot;      // 성공 표시 루트
    [SerializeField] private GameObject stage3HintRoot;  // 성공 후 3단계 힌트 표시 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private const int GridSize = 5; // 현재 5x5 고정 그리드 크기

    /// <summary>
    /// 시작 위치와 목표 위치를 한 번에 화면에 반영한다.
    /// 퍼즐 초기화 직후 호출된다.
    /// </summary>
    public void ApplyInitialState(MazeCellCoord startCell, MazeCellCoord goalCell)
    {
        MovePieceTo(startCell); // 시작 셀로 Piece 배치
        MoveGoalTo(goalCell);   // 목표 셀로 목표 마커 배치

        Log($"초기 상태 적용 | start={startCell} | goal={goalCell}");
    }

    /// <summary>
    /// 현재 Piece를 지정된 셀 위치로 이동시킨다.
    /// PlayerPiece는 셀 forward 방향으로 0.001만큼 띄워서 배치한다.
    /// </summary>
    public void MovePieceTo(MazeCellCoord cell)
    {
        if (playerPiece == null)
            return; // Piece 참조 없으면 종료

        Transform cellTransform = GetCellTransform(cell); // 대상 셀 Transform 찾기
        if (cellTransform == null)
            return; // 유효하지 않은 셀이면 종료

        Vector3 targetPosition = cellTransform.position - (cellTransform.forward * playerPieceOffset); // 셀 위치 + Piece 오프셋
        playerPiece.position = targetPosition;  // Piece를 오프셋 적용 위치로 이동
        playerPiece.rotation = cellTransform.rotation; // 필요 시 셀 회전 기준 맞춤
    }

    /// <summary>
    /// 목표 마커를 지정된 셀 위치로 이동시킨다.
    /// TargetMarker는 셀 forward 방향으로 0.002만큼 띄워서 배치한다.
    /// </summary>
    public void MoveGoalTo(MazeCellCoord cell)
    {
        if (targetMarker == null)
            return; // 목표 마커 참조 없으면 종료

        Transform cellTransform = GetCellTransform(cell); // 대상 셀 Transform 찾기
        if (cellTransform == null)
            return; // 유효하지 않은 셀이면 종료

        Vector3 targetPosition = cellTransform.position - (cellTransform.forward * targetMarkerOffset); // 셀 위치 + Target 오프셋
        targetMarker.position = targetPosition; // 목표 마커를 오프셋 적용 위치로 이동
        targetMarker.rotation = cellTransform.rotation; // 필요 시 셀 회전 기준 맞춤
    }

    /// <summary>
    /// 성공 상태 화면으로 전환한다.
    /// Stage3HintRoot를 바로 쓰지 않는 경우 사용한다.
    /// </summary>
    public void ShowSolvedState()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(false); // 기본 퍼즐 화면 숨김

        if (solvedRoot != null)
            solvedRoot.SetActive(true);   // 성공 화면 표시

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false); // 3단계 힌트 화면은 숨김

        Log("성공 상태 화면 전환");
    }

    /// <summary>
    /// 성공 후 3단계 힌트 화면으로 전환한다.
    /// </summary>
    public void ShowStage3HintState()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(false); // 기본 퍼즐 화면 숨김

        if (solvedRoot != null)
            solvedRoot.SetActive(false);  // 성공 화면 숨김

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(true); // 3단계 힌트 화면 표시

        Log("3단계 힌트 화면 전환");
    }

    /// <summary>
    /// 기본 상태로 화면을 초기화한다.
    /// 퍼즐 화면만 보이게 하고 나머지 상태 루트는 끈다.
    /// </summary>
    public void ResetToDefault()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(true); // 기본 퍼즐 화면 표시

        if (solvedRoot != null)
            solvedRoot.SetActive(false); // 성공 화면 숨김

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false); // 3단계 힌트 화면 숨김

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// row / col 좌표에 대응하는 셀 Transform을 반환한다.
    /// </summary>
    private Transform GetCellTransform(MazeCellCoord cell)
    {
        if (!IsInside(cell))
            return null; // 범위 밖 좌표면 null 반환

        int flatIndex = ToFlatIndex(cell.Row, cell.Col); // 2차원 좌표를 1차원 인덱스로 변환

        if (flatIndex < 0 || flatIndex >= cellTransforms.Count)
            return null; // 인덱스 범위 방어

        return cellTransforms[flatIndex]; // 대응하는 셀 Transform 반환
    }

    /// <summary>
    /// row / col 좌표를 1차원 리스트 인덱스로 변환한다.
    /// row-major 순서를 사용한다.
    /// </summary>
    private int ToFlatIndex(int row, int col)
    {
        return row * GridSize + col; // (row, col) -> 0~24 인덱스 변환
    }

    /// <summary>
    /// 좌표가 5x5 범위 안에 있는지 검사한다.
    /// </summary>
    private bool IsInside(MazeCellCoord cell)
    {
        return cell.Row >= 0 &&
               cell.Row < GridSize &&
               cell.Col >= 0 &&
               cell.Col < GridSize; // 0 <= row,col < 5
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return; // 로그 꺼져 있으면 종료

        Debug.Log($"[MazePuzzleView] {message}", this); // 뷰 디버그 로그 출력
    }
}