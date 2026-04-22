using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐의 힌트 화면 표시 담당.
/// 
/// 역할
/// - 가로 벽 30개 on/off 표시
/// - 세로 벽 30개 on/off 표시
/// - 목표 위치를 셀 기준으로 표시
/// </summary>
public class MazeHintView : MonoBehaviour
{
    [Header("셀 참조")]
    [SerializeField] private List<Transform> cellTransforms = new(); // 5x5 셀 위치 참조 목록 (총 25개)

    [Header("가로 벽 참조")]
    [SerializeField] private List<GameObject> horizontalWalls = new(); // 가로 벽 오브젝트 목록 (총 30개)

    [Header("세로 벽 참조")]
    [SerializeField] private List<GameObject> verticalWalls = new();   // 세로 벽 오브젝트 목록 (총 30개)

    [Header("표시 오브젝트")]
    [SerializeField] private Transform targetMarker; // 목표 위치 표시 오브젝트

    [Header("표시 오프셋")]
    [SerializeField] private float targetMarkerOffset = 0.002f; // 셀 기준 TargetMarker를 앞으로 띄울 거리

    [Header("상태 루트")]
    [SerializeField] private GameObject monitorRoot; // 기본 힌트 화면 루트

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private const int GridSize = 5; // 현재 5x5 고정 그리드 크기

    /// <summary>
    /// 미로 벽 데이터를 화면에 반영한다.
    /// </summary>
    public void ApplyMazeWalls(bool[,] horizontalWallData, bool[,] verticalWallData)
    {
        if (horizontalWallData == null || verticalWallData == null)
            return; // 벽 데이터가 없으면 종료

        // 가로 벽 데이터 반영
        for (int row = 0; row < GridSize + 1; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                bool isActive = horizontalWallData[row, col]; // 현재 가로 벽 활성 여부
                SetHorizontalWall(row, col, isActive);        // 해당 가로 벽 오브젝트 on/off 적용
            }
        }

        // 세로 벽 데이터 반영
        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize + 1; col++)
            {
                bool isActive = verticalWallData[row, col];   // 현재 세로 벽 활성 여부
                SetVerticalWall(row, col, isActive);          // 해당 세로 벽 오브젝트 on/off 적용
            }
        }

        Log("미로 벽 데이터 적용 완료");
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
    /// 기본 상태로 초기화한다.
    /// 힌트 화면은 켜고, 모든 벽은 우선 끈다.
    /// </summary>
    public void ResetToDefault()
    {
        if (monitorRoot != null)
            monitorRoot.SetActive(true); // 기본 힌트 화면 표시

        for (int i = 0; i < horizontalWalls.Count; i++)
        {
            if (horizontalWalls[i] == null)
                continue;

            horizontalWalls[i].SetActive(false); // 가로 벽 기본 비활성화
        }

        for (int i = 0; i < verticalWalls.Count; i++)
        {
            if (verticalWalls[i] == null)
                continue;

            verticalWalls[i].SetActive(false); // 세로 벽 기본 비활성화
        }

        Log("기본 상태로 초기화");
    }

    /// <summary>
    /// 지정한 가로 벽 오브젝트의 활성 상태를 설정한다.
    /// row 범위: 0~5, col 범위: 0~4
    /// </summary>
    public void SetHorizontalWall(int row, int col, bool isActive)
    {
        int flatIndex = ToHorizontalWallIndex(row, col); // 2차원 좌표를 1차원 인덱스로 변환
        if (flatIndex < 0 || flatIndex >= horizontalWalls.Count)
            return; // 인덱스 범위 방어

        GameObject wallObject = horizontalWalls[flatIndex]; // 해당 가로 벽 오브젝트
        if (wallObject == null)
            return; // 오브젝트 참조 없으면 종료

        wallObject.SetActive(isActive); // 가로 벽 활성/비활성 반영
    }

    /// <summary>
    /// 지정한 세로 벽 오브젝트의 활성 상태를 설정한다.
    /// row 범위: 0~4, col 범위: 0~5
    /// </summary>
    public void SetVerticalWall(int row, int col, bool isActive)
    {
        int flatIndex = ToVerticalWallIndex(row, col); // 2차원 좌표를 1차원 인덱스로 변환
        if (flatIndex < 0 || flatIndex >= verticalWalls.Count)
            return; // 인덱스 범위 방어

        GameObject wallObject = verticalWalls[flatIndex]; // 해당 세로 벽 오브젝트
        if (wallObject == null)
            return; // 오브젝트 참조 없으면 종료

        wallObject.SetActive(isActive); // 세로 벽 활성/비활성 반영
    }

    /// <summary>
    /// row / col 좌표에 대응하는 셀 Transform을 반환한다.
    /// </summary>
    private Transform GetCellTransform(MazeCellCoord cell)
    {
        if (!IsInside(cell))
            return null; // 범위 밖 좌표면 null 반환

        int flatIndex = ToCellIndex(cell.Row, cell.Col); // 2차원 좌표를 1차원 인덱스로 변환
        if (flatIndex < 0 || flatIndex >= cellTransforms.Count)
            return null; // 인덱스 범위 방어

        return cellTransforms[flatIndex]; // 대응하는 셀 Transform 반환
    }

    /// <summary>
    /// 셀 row / col 좌표를 1차원 인덱스로 변환한다.
    /// row-major 순서를 사용한다.
    /// </summary>
    private int ToCellIndex(int row, int col)
    {
        return row * GridSize + col; // (row, col) -> 0~24 인덱스 변환
    }

    /// <summary>
    /// 가로 벽 row / col 좌표를 1차원 인덱스로 변환한다.
    /// 가로 벽은 6행 x 5열 구조를 사용한다.
    /// </summary>
    private int ToHorizontalWallIndex(int row, int col)
    {
        return row * GridSize + col; // (row, col) -> 0~29 인덱스 변환
    }

    /// <summary>
    /// 세로 벽 row / col 좌표를 1차원 인덱스로 변환한다.
    /// 세로 벽은 5행 x 6열 구조를 사용한다.
    /// </summary>
    private int ToVerticalWallIndex(int row, int col)
    {
        return row * (GridSize + 1) + col; // (row, col) -> 0~29 인덱스 변환
    }

    /// <summary>
    /// 셀 좌표가 5x5 범위 안에 있는지 검사한다.
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

        Debug.Log($"[MazeHintView] {message}", this); // 뷰 디버그 로그 출력
    }
}