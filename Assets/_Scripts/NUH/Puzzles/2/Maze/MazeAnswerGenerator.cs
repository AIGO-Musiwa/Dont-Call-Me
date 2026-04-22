using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐의 정답 데이터 생성 유틸.
/// 같은 seed를 받은 퍼즐 본체와 힌트가 동일한 미로를 재구성할 수 있도록 사용한다.
/// </summary>
public static class MazeAnswerGenerator
{
    /// <summary>
    /// 한 판의 미로 퍼즐 데이터를 담는 컨테이너.
    /// </summary>
    [Serializable]
    public class MazeAnswerData
    {
        public int GridSize;                  // 미로 한 변 크기 (기본 5)
        public MazeCellCoord StartCell;       // 플레이어 시작 셀
        public MazeCellCoord GoalCell;        // 도착 목표 셀
        public bool[,] HorizontalWalls;       // 가로 벽 데이터 [gridSize + 1, gridSize]
        public bool[,] VerticalWalls;         // 세로 벽 데이터 [gridSize, gridSize + 1]
        public readonly List<MazeCellCoord> SolutionPath = new(); // 시작부터 목표까지의 정답 경로
    }

    private const int DefaultGridSize = 5;      // 기본 5x5 고정 크기
    private const float ExtraWallChance = 0.25f; // 기본 길 생성 후 추가 벽을 막을 확률

    /// <summary>
    /// seed 기준으로 미로 데이터를 생성한다.
    /// </summary>
    public static MazeAnswerData Generate(int seed)
    {
        SeedRandom rng = new SeedRandom(seed);                    // 시드 기반 랜덤 생성기
        MazeAnswerData data = new MazeAnswerData();               // 결과 데이터 생성

        data.GridSize = DefaultGridSize;                          // 현재는 5x5 고정
        data.HorizontalWalls = new bool[data.GridSize + 1, data.GridSize]; // 가로 벽 배열 생성
        data.VerticalWalls = new bool[data.GridSize, data.GridSize + 1];   // 세로 벽 배열 생성

        InitializeAllWallsClosed(data);                           // 시작 상태는 모든 벽을 막힌 상태로 초기화
        BuildPerfectMaze(data, rng);                              // 먼저 반드시 도달 가능한 기본 미로 생성
        AddExtraOpenConnections(data, rng);                       // 난이도 조절용으로 일부 벽 추가 개방

        data.StartCell = BuildStartCell(data, rng);               // 시작 위치 생성
        data.GoalCell = BuildGoalCell(data, rng, data.StartCell); // 목표 위치 생성 (시작과 다르게)

        BuildSolutionPath(data);                                  // 시작점~목표점 실제 경로 계산

        return data;                                              // 완성된 미로 데이터 반환
    }

    /// <summary>
    /// 모든 벽을 막힌 상태(true)로 초기화한다.
    /// </summary>
    private static void InitializeAllWallsClosed(MazeAnswerData data)
    {
        for (int row = 0; row < data.GridSize + 1; row++)
        {
            for (int col = 0; col < data.GridSize; col++)
            {
                data.HorizontalWalls[row, col] = true; // 가로 벽 전부 막기
            }
        }

        for (int row = 0; row < data.GridSize; row++)
        {
            for (int col = 0; col < data.GridSize + 1; col++)
            {
                data.VerticalWalls[row, col] = true; // 세로 벽 전부 막기
            }
        }
    }

    /// <summary>
    /// DFS 기반으로 완전 연결 미로(perfect maze)를 만든다.
    /// 모든 칸이 연결되며, 최소한 하나의 경로는 항상 존재한다.
    /// </summary>
    private static void BuildPerfectMaze(MazeAnswerData data, SeedRandom rng)
    {
        bool[,] visited = new bool[data.GridSize, data.GridSize]; // 방문 여부 기록
        Stack<MazeCellCoord> stack = new Stack<MazeCellCoord>();  // DFS 스택

        MazeCellCoord start = new MazeCellCoord(0, 0);            // 생성 시작 셀
        stack.Push(start);                                        // 시작 셀 push
        visited[start.Row, start.Col] = true;                     // 시작 셀 방문 처리

        while (stack.Count > 0)
        {
            MazeCellCoord current = stack.Peek();                           // 현재 셀 확인
            List<MazeCellCoord> unvisitedNeighbors = GetUnvisitedNeighbors(current, visited, data.GridSize); // 미방문 이웃 찾기

            if (unvisitedNeighbors.Count == 0)
            {
                stack.Pop();                                                // 더 갈 곳 없으면 백트래킹
                continue;
            }

            rng.Shuffle(unvisitedNeighbors);                                // 이웃 순서 랜덤화
            MazeCellCoord next = unvisitedNeighbors[0];                     // 다음 이동 셀 선택

            OpenWallBetween(data, current, next);                           // 현재 셀과 다음 셀 사이 벽 개방
            visited[next.Row, next.Col] = true;                            // 다음 셀 방문 처리
            stack.Push(next);                                               // 다음 셀로 진행
        }
    }

    /// <summary>
    /// 완전 미로 생성 후 일부 벽을 추가로 열어서 경로 다양성을 늘린다.
    /// </summary>
    private static void AddExtraOpenConnections(MazeAnswerData data, SeedRandom rng)
    {
        for (int row = 0; row < data.GridSize; row++)
        {
            for (int col = 0; col < data.GridSize; col++)
            {
                MazeCellCoord current = new MazeCellCoord(row, col); // 현재 셀

                // 오른쪽 이웃과의 벽 추가 개방 시도
                MazeCellCoord right = new MazeCellCoord(row, col + 1);
                if (IsInside(right, data.GridSize) && IsWallClosedBetween(data, current, right))
                {
                    if (rng.NextFloat() < ExtraWallChance)
                        OpenWallBetween(data, current, right); // 오른쪽 벽 개방
                }

                // 아래쪽 이웃과의 벽 추가 개방 시도
                MazeCellCoord down = new MazeCellCoord(row + 1, col);
                if (IsInside(down, data.GridSize) && IsWallClosedBetween(data, current, down))
                {
                    if (rng.NextFloat() < ExtraWallChance)
                        OpenWallBetween(data, current, down); // 아래쪽 벽 개방
                }
            }
        }
    }

    /// <summary>
    /// 시작 셀을 생성한다.
    /// 현재는 5x5 모든 칸 중 하나를 랜덤 선택한다.
    /// </summary>
    private static MazeCellCoord BuildStartCell(MazeAnswerData data, SeedRandom rng)
    {
        int row = rng.NextInt(0, data.GridSize); // 시작 행 랜덤
        int col = rng.NextInt(0, data.GridSize); // 시작 열 랜덤
        return new MazeCellCoord(row, col);      // 시작 좌표 반환
    }

    /// <summary>
    /// 목표 셀을 생성한다.
    /// 시작 셀과 다른 칸이면서 실제 도달 가능한 칸만 선택한다.
    /// </summary>
    private static MazeCellCoord BuildGoalCell(MazeAnswerData data, SeedRandom rng, MazeCellCoord startCell)
    {
        List<MazeCellCoord> reachableCells = GetReachableCells(data, startCell); // 시작점에서 도달 가능한 칸 목록

        // 시작 셀은 목표 후보에서 제거
        for (int i = reachableCells.Count - 1; i >= 0; i--)
        {
            if (reachableCells[i] == startCell)
                reachableCells.RemoveAt(i);
        }

        if (reachableCells.Count == 0)
            return startCell; // 방어용: 비정상 상황이면 시작 셀 반환

        rng.Shuffle(reachableCells);             // 후보 순서 랜덤화
        return reachableCells[0];                // 첫 번째 후보를 목표 셀로 사용
    }

    /// <summary>
    /// 시작점에서 목표점까지 실제 경로를 찾아 저장한다.
    /// </summary>
    private static void BuildSolutionPath(MazeAnswerData data)
    {
        data.SolutionPath.Clear(); // 기존 경로 초기화

        Dictionary<MazeCellCoord, MazeCellCoord> parentMap = new Dictionary<MazeCellCoord, MazeCellCoord>(); // BFS 부모 기록
        Queue<MazeCellCoord> queue = new Queue<MazeCellCoord>();                                              // BFS 큐
        HashSet<MazeCellCoord> visited = new HashSet<MazeCellCoord>();                                        // 방문 집합

        queue.Enqueue(data.StartCell);            // 시작 셀 큐 삽입
        visited.Add(data.StartCell);              // 시작 셀 방문 처리

        bool found = false;                       // 목표 발견 여부

        while (queue.Count > 0)
        {
            MazeCellCoord current = queue.Dequeue(); // 현재 셀 꺼내기

            if (current == data.GoalCell)
            {
                found = true;                        // 목표 발견
                break;
            }

            List<MazeCellCoord> neighbors = GetMovableNeighbors(data, current); // 실제 이동 가능한 이웃들

            for (int i = 0; i < neighbors.Count; i++)
            {
                MazeCellCoord next = neighbors[i];  // 다음 후보 셀

                if (visited.Contains(next))
                    continue;                       // 이미 방문한 칸은 스킵

                visited.Add(next);                  // 방문 처리
                parentMap[next] = current;          // 부모 기록
                queue.Enqueue(next);                // 큐에 추가
            }
        }

        if (!found)
            return; // 방어용: 비정상적으로 경로가 없으면 빈 경로 유지

        List<MazeCellCoord> reversedPath = new List<MazeCellCoord>(); // 역순 경로 임시 저장
        MazeCellCoord step = data.GoalCell;                           // 목표에서 시작

        reversedPath.Add(step);                                       // 목표칸 추가

        while (step != data.StartCell)
        {
            step = parentMap[step];                                   // 부모 방향으로 거슬러 올라감
            reversedPath.Add(step);                                   // 역순 경로 누적
        }

        reversedPath.Reverse();                                       // 시작 -> 목표 순서로 뒤집기
        data.SolutionPath.AddRange(reversedPath);                     // 최종 경로 저장
    }

    /// <summary>
    /// 현재 셀의 미방문 이웃 목록을 구한다.
    /// </summary>
    private static List<MazeCellCoord> GetUnvisitedNeighbors(MazeCellCoord current, bool[,] visited, int gridSize)
    {
        List<MazeCellCoord> result = new List<MazeCellCoord>(); // 결과 리스트

        MazeCellCoord up = new MazeCellCoord(current.Row - 1, current.Col);     // 위쪽 셀
        MazeCellCoord down = new MazeCellCoord(current.Row + 1, current.Col);   // 아래쪽 셀
        MazeCellCoord left = new MazeCellCoord(current.Row, current.Col - 1);   // 왼쪽 셀
        MazeCellCoord right = new MazeCellCoord(current.Row, current.Col + 1);  // 오른쪽 셀

        AddIfUnvisited(result, up, visited, gridSize);       // 위쪽 후보 추가
        AddIfUnvisited(result, down, visited, gridSize);     // 아래쪽 후보 추가
        AddIfUnvisited(result, left, visited, gridSize);     // 왼쪽 후보 추가
        AddIfUnvisited(result, right, visited, gridSize);    // 오른쪽 후보 추가

        return result; // 미방문 이웃 목록 반환
    }

    /// <summary>
    /// 좌표가 유효 범위 안이고 아직 방문하지 않았으면 목록에 넣는다.
    /// </summary>
    private static void AddIfUnvisited(List<MazeCellCoord> result, MazeCellCoord coord, bool[,] visited, int gridSize)
    {
        if (!IsInside(coord, gridSize))
            return; // 범위 밖이면 제외

        if (visited[coord.Row, coord.Col])
            return; // 이미 방문했으면 제외

        result.Add(coord); // 유효한 미방문 셀 추가
    }

    /// <summary>
    /// 좌표가 gridSize 범위 안에 있는지 검사한다.
    /// </summary>
    private static bool IsInside(MazeCellCoord coord, int gridSize)
    {
        return coord.Row >= 0 &&
               coord.Row < gridSize &&
               coord.Col >= 0 &&
               coord.Col < gridSize; // 0 <= row,col < gridSize
    }

    /// <summary>
    /// 두 인접 셀 사이의 벽을 연다.
    /// </summary>
    private static void OpenWallBetween(MazeAnswerData data, MazeCellCoord a, MazeCellCoord b)
    {
        if (a.Row == b.Row)
        {
            // 좌우 이동인 경우 세로 벽 개방
            int row = a.Row;                          // 같은 행
            int wallCol = Mathf.Max(a.Col, b.Col);   // 두 셀 사이 세로 벽 열 인덱스
            data.VerticalWalls[row, wallCol] = false; // 세로 벽 open
            return;
        }

        if (a.Col == b.Col)
        {
            // 상하 이동인 경우 가로 벽 개방
            int col = a.Col;                          // 같은 열
            int wallRow = Mathf.Max(a.Row, b.Row);   // 두 셀 사이 가로 벽 행 인덱스
            data.HorizontalWalls[wallRow, col] = false; // 가로 벽 open
        }
    }

    /// <summary>
    /// 두 인접 셀 사이 벽이 아직 닫혀 있는지 검사한다.
    /// </summary>
    private static bool IsWallClosedBetween(MazeAnswerData data, MazeCellCoord a, MazeCellCoord b)
    {
        if (a.Row == b.Row)
        {
            int row = a.Row;                         // 같은 행
            int wallCol = Mathf.Max(a.Col, b.Col);  // 세로 벽 인덱스
            return data.VerticalWalls[row, wallCol]; // 세로 벽 상태 반환
        }

        int col = a.Col;                            // 같은 열
        int wallRow = Mathf.Max(a.Row, b.Row);     // 가로 벽 인덱스
        return data.HorizontalWalls[wallRow, col]; // 가로 벽 상태 반환
    }

    /// <summary>
    /// 현재 셀에서 실제 이동 가능한 이웃 셀 목록을 구한다.
    /// </summary>
    private static List<MazeCellCoord> GetMovableNeighbors(MazeAnswerData data, MazeCellCoord current)
    {
        List<MazeCellCoord> result = new List<MazeCellCoord>(); // 결과 리스트

        MazeCellCoord up = new MazeCellCoord(current.Row - 1, current.Col);     // 위쪽 후보
        MazeCellCoord down = new MazeCellCoord(current.Row + 1, current.Col);   // 아래쪽 후보
        MazeCellCoord left = new MazeCellCoord(current.Row, current.Col - 1);   // 왼쪽 후보
        MazeCellCoord right = new MazeCellCoord(current.Row, current.Col + 1);  // 오른쪽 후보

        AddIfMovable(data, current, up, result);        // 위쪽 이동 가능하면 추가
        AddIfMovable(data, current, down, result);      // 아래쪽 이동 가능하면 추가
        AddIfMovable(data, current, left, result);      // 왼쪽 이동 가능하면 추가
        AddIfMovable(data, current, right, result);     // 오른쪽 이동 가능하면 추가

        return result; // 이동 가능한 이웃 반환
    }

    /// <summary>
    /// target 셀이 범위 안에 있고 current에서 실제 이동 가능하면 목록에 넣는다.
    /// </summary>
    private static void AddIfMovable(MazeAnswerData data, MazeCellCoord current, MazeCellCoord target, List<MazeCellCoord> result)
    {
        if (!IsInside(target, data.GridSize))
            return; // 범위 밖이면 제외

        if (IsWallClosedBetween(data, current, target))
            return; // 벽이 막혀 있으면 제외

        result.Add(target); // 이동 가능하면 추가
    }

    /// <summary>
    /// 시작점에서 도달 가능한 모든 셀을 BFS로 구한다.
    /// </summary>
    private static List<MazeCellCoord> GetReachableCells(MazeAnswerData data, MazeCellCoord start)
    {
        List<MazeCellCoord> result = new List<MazeCellCoord>(); // 결과 리스트
        Queue<MazeCellCoord> queue = new Queue<MazeCellCoord>(); // BFS 큐
        HashSet<MazeCellCoord> visited = new HashSet<MazeCellCoord>(); // 방문 집합

        queue.Enqueue(start);   // 시작 셀 enqueue
        visited.Add(start);     // 시작 셀 방문 처리

        while (queue.Count > 0)
        {
            MazeCellCoord current = queue.Dequeue();     // 현재 셀 꺼내기
            result.Add(current);                         // 도달 가능 목록에 추가

            List<MazeCellCoord> neighbors = GetMovableNeighbors(data, current); // 이동 가능한 이웃

            for (int i = 0; i < neighbors.Count; i++)
            {
                MazeCellCoord next = neighbors[i];       // 다음 셀

                if (visited.Contains(next))
                    continue;                            // 이미 방문했으면 스킵

                visited.Add(next);                       // 방문 처리
                queue.Enqueue(next);                     // 큐에 추가
            }
        }

        return result; // 도달 가능한 모든 셀 반환
    }
}