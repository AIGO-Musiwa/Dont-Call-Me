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
/// - Stage3HintRoot에 3단계 힌트를 표시한다.
/// </summary>
public class MazePuzzleView : MonoBehaviour
{
    [Header("셀 참조")]
    [SerializeField] private List<Transform> cellTransforms = new(); // 5x5 셀 위치 참조 목록 (총 25개)

    [Header("표시 오브젝트")]
    [SerializeField] private Transform playerPiece; // 현재 플레이어 말 표시 오브젝트
    [SerializeField] private Transform targetMarker; // 목표 위치 표시 오브젝트

    [Header("표시 오프셋")]
    [SerializeField] private float playerPieceOffset = 0.001f; // 셀 기준 PlayerPiece를 앞으로 띄울 거리
    [SerializeField] private float targetMarkerOffset = 0.002f; // 셀 기준 TargetMarker를 앞으로 띄울 거리

    [Header("상태 루트")]
    [SerializeField] private GameObject monitorRoot; // 기본 퍼즐 화면 루트
    [SerializeField] private GameObject solvedRoot; // 성공 표시 루트
    [SerializeField] private GameObject stage3HintRoot; // 성공 후 3단계 힌트 표시 루트

    [Header("3단계 힌트 표시기")]
    [SerializeField] private FinalCodeHintDisplay stage3HintDisplay; // Stage3HintRoot 내부 최종 힌트 표시기

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private const int GridSize = 5; // 현재 5x5 고정 그리드 크기

    /// <summary>
    /// 시작 위치와 목표 위치를 한 번에 화면에 반영한다.
    /// </summary>
    public void ApplyInitialState(MazeCellCoord startCell, MazeCellCoord goalCell)
    {
        MovePieceTo(startCell);
        MoveGoalTo(goalCell);

        Log($"초기 상태 적용 | start={startCell} | goal={goalCell}");
    }

    /// <summary>
    /// 현재 Piece를 지정된 셀 위치로 이동시킨다.
    /// </summary>
    public void MovePieceTo(MazeCellCoord cell)
    {
        if (playerPiece == null)
            return;

        Transform cellTransform = GetCellTransform(cell);
        if (cellTransform == null)
            return;

        Vector3 targetPosition = cellTransform.position - (cellTransform.forward * playerPieceOffset);
        playerPiece.position = targetPosition;
        playerPiece.rotation = cellTransform.rotation;
    }

    /// <summary>
    /// 목표 마커를 지정된 셀 위치로 이동시킨다.
    /// </summary>
    public void MoveGoalTo(MazeCellCoord cell)
    {
        if (targetMarker == null)
            return;

        Transform cellTransform = GetCellTransform(cell);
        if (cellTransform == null)
            return;

        Vector3 targetPosition = cellTransform.position - (cellTransform.forward * targetMarkerOffset);
        targetMarker.position = targetPosition;
        targetMarker.rotation = cellTransform.rotation;
    }

    /// <summary>
    /// 3단계 힌트 데이터를 표시기에 반영한다.
    /// </summary>
    public void ApplyStage3Hint(FinalCodeHintData hintData)
    {
        if (stage3HintDisplay == null)
            return;

        stage3HintDisplay.ApplyHint(hintData);
    }

    /// <summary>
    /// 성공 상태 화면으로 전환한다.
    /// </summary>
    public void ShowSolvedState()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(true);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false);

        Log("성공 상태 화면 전환");
    }

    /// <summary>
    /// 성공 후 3단계 힌트 화면으로 전환한다.
    /// </summary>
    public void ShowStage3HintState()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(false);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(true);

        Log("3단계 힌트 화면 전환");
    }

    /// <summary>
    /// 기본 상태로 화면을 초기화한다.
    /// </summary>
    public void ResetToDefault()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(true);

        if (solvedRoot != null)
            solvedRoot.SetActive(false);

        if (stage3HintRoot != null)
            stage3HintRoot.SetActive(false);

        if (stage3HintDisplay != null)
            stage3HintDisplay.ResetDisplay();

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// row / col 좌표에 대응하는 셀 Transform을 반환한다.
    /// </summary>
    private Transform GetCellTransform(MazeCellCoord cell)
    {
        if (!IsInside(cell))
            return null;

        int index = cell.Row * GridSize + cell.Col;
        if (index < 0 || index >= cellTransforms.Count)
            return null;

        return cellTransforms[index];
    }

    /// <summary>
    /// 좌표가 5x5 범위 안인지 검사한다.
    /// </summary>
    private bool IsInside(MazeCellCoord cell)
    {
        return cell.Row >= 0 && cell.Row < GridSize && cell.Col >= 0 && cell.Col < GridSize;
    }

    /// <summary>
    /// 일반 디버그 로그 출력.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[MazePuzzleView] {message}", this);
    }
}