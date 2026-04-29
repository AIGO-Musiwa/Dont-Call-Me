using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 서랍 힌트 표시 담당.
/// 
/// 전제
/// - 맵에는 서랍 가구 오브젝트 1개만 배치된다.
/// - 그 가구 안에 여러 개의 서랍 Transform이 있다.
/// - 인스펙터에는 각 서랍 Transform만 등록한다.
/// - 닫힘 상태는 시작 시점의 localPosition을 기준으로 저장한다.
/// - 열림 상태는 닫힘 localPosition 기준 Y축으로 openedLocalYOffset만큼 이동한다.
/// </summary>
public class DrawerHintDisplay : MonoBehaviour
{
    [Header("서랍 Transform 목록")]
    [SerializeField] private List<Transform> drawers = new(); // 실제 이동시킬 서랍 Transform 목록

    [Header("열림 이동값")]
    [SerializeField] private float openedLocalYOffset = -0.0035f; // 열림 상태에서 닫힘 위치 기준으로 더할 Y 오프셋

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 여부

    private readonly List<Vector3> _closedLocalPositions = new(); // 각 서랍의 닫힘 기준 localPosition
    private bool _hasCachedClosedPositions;                       // 닫힘 위치 캐싱 여부

    private void Awake()
    {
        CacheClosedPositions(); // 시작 시점의 위치를 닫힘 기준으로 저장
    }

    /// <summary>
    /// 열린 서랍 개수와 seed를 받아,
    /// 실제 등록된 drawers.Count 기준으로 랜덤한 서랍을 연다.
    /// </summary>
    public void SetOpenedDrawerCount(int openedCount, int seed)
    {
        EnsureCachedClosedPositions();

        if (drawers.Count == 0)
        {
            LogWarning("등록된 서랍이 없어 열린 개수 적용을 건너뜁니다.");
            return;
        }

        int clampedOpenedCount = Mathf.Clamp(openedCount, 0, drawers.Count);

        // 먼저 모든 서랍을 닫힘 상태로 되돌린다.
        for (int i = 0; i < drawers.Count; i++)
            ApplyDrawerState(i, false);

        // seed 기반으로 서랍 인덱스를 섞는다.
        List<int> shuffledIndices = new List<int>(drawers.Count);

        for (int i = 0; i < drawers.Count; i++)
            shuffledIndices.Add(i);

        SeedRandom rng = new SeedRandom(seed);
        rng.Shuffle(shuffledIndices);

        // 섞인 순서에서 openedCount만큼만 연다.
        for (int i = 0; i < clampedOpenedCount; i++)
        {
            int index = shuffledIndices[i];
            ApplyDrawerState(index, true);
        }

        Log($"열린 서랍 적용 완료 | requested={openedCount} | applied={clampedOpenedCount} | drawerCount={drawers.Count} | seed={seed}");
    }

    /// <summary>
    /// 모든 서랍을 시작 시점에 저장한 닫힘 위치로 되돌린다.
    /// </summary>
    public void ResetToDefault()
    {
        EnsureCachedClosedPositions();

        for (int i = 0; i < drawers.Count; i++)
            ApplyDrawerState(i, false);

        Log("서랍 힌트 기본 상태로 초기화");
    }

    /// <summary>
    /// 현재 등록된 서랍들의 시작 localPosition을 닫힘 위치로 저장한다.
    /// </summary>
    private void CacheClosedPositions()
    {
        _closedLocalPositions.Clear();

        for (int i = 0; i < drawers.Count; i++)
        {
            if (drawers[i] == null)
            {
                _closedLocalPositions.Add(Vector3.zero);
                continue;
            }

            _closedLocalPositions.Add(drawers[i].localPosition);
        }

        _hasCachedClosedPositions = true;

        Log($"서랍 닫힘 위치 캐싱 완료 | drawerCount={drawers.Count}");
    }

    /// <summary>
    /// 닫힘 위치 캐싱이 안 되어 있거나 리스트 크기가 맞지 않으면 다시 캐싱한다.
    /// </summary>
    private void EnsureCachedClosedPositions()
    {
        if (!_hasCachedClosedPositions || _closedLocalPositions.Count != drawers.Count)
            CacheClosedPositions();
    }

    /// <summary>
    /// 특정 서랍을 열림/닫힘 상태로 전환한다.
    /// 닫힘: 캐싱된 원래 localPosition
    /// 열림: 캐싱된 원래 localPosition + Y 오프셋
    /// 회전은 건드리지 않는다.
    /// </summary>
    private void ApplyDrawerState(int drawerIndex, bool isOpened)
    {
        if (drawerIndex < 0 || drawerIndex >= drawers.Count)
            return;

        Transform drawer = drawers[drawerIndex];

        if (drawer == null)
            return;

        if (drawerIndex >= _closedLocalPositions.Count)
            return;

        Vector3 targetPosition = _closedLocalPositions[drawerIndex];

        if (isOpened)
            targetPosition.y += openedLocalYOffset;

        drawer.localPosition = targetPosition;
    }

    /// <summary>
    /// 디버그 로그를 출력한다.
    /// </summary>
    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[DrawerHintDisplay] {message}", this);
    }

    /// <summary>
    /// 디버그 경고 로그를 출력한다.
    /// </summary>
    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[DrawerHintDisplay] {message}", this);
    }
}