using UnityEngine;

/// <summary>
/// 퍼즐이 외부에서 정답 생성용 시드를 받을 수 있도록 하는 공통 인터페이스
/// PuzzleSpawnManager가 스폰 직후 호출
/// </summary>
public interface IPuzzleSeedReceiver
{
    /// <summary>
    /// 퍼즐 정답 생성용 시드를 적용
    /// </summary>
    void ApplyAnswerSeed(int seed);
}
