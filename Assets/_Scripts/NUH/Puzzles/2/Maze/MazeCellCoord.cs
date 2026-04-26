using System;
using UnityEngine;

/// <summary>
/// 5x5 미로 퍼즐에서 사용할 셀 좌표 묶음
/// row / col을 항상 같이 다루기 위해 사용
/// </summary>
public class MazeCellCoord : IEquatable<MazeCellCoord>
{
    [SerializeField] private int row;       // 행 인덱스
    [SerializeField] private int col;       // 열 인덱스

    public int Row => row;                // 행 인덱스 외부 읽기용 
    public int Col => col;                // 열 인덱스 외부 읽기용

    /// <summary>
    /// 좌표 생성자
    /// </summary>
    public MazeCellCoord(int row, int col)
    {
        this.row = row;     // 행 값 저장
        this.col = col;     // 열 값 저장
    }

    /// <summary>
    /// 두 좌표가 같은지 비교
    /// </summary>
    public bool Equals(MazeCellCoord other)
    {
        return row == other.row && col == other.col;    // row / col 둘 다 같으면 동일 좌표
    }

    /// <summary>
    /// object 비교용 override
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is MazeCellCoord other && Equals(other);    // obj가 MazeCellCoord 타입이면 Equals 호출
    }

    /// <summary>
    /// 해시코드 생성
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            return (row * 397) ^ col;    // row와 col을 조합한 해시코드 생성 (397은 임의의 소수)
        }
    }

    /// <summary>
    /// 디버그용 문자열
    /// </summary>
    public override string ToString()
    {
        return $"({row}, {col})";       // 좌표 문자열 반환
    }

    public static bool operator == (MazeCellCoord left,  MazeCellCoord right)
    {
        return left.Equals(right);      // == 비교 지원
    }
     
    public static bool operator != (MazeCellCoord left, MazeCellCoord right)
    {
        return !left.Equals(right);     // != 비교 지원
    }


}
