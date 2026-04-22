using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 서랍 힌트 표시 담당.
/// 
/// 전제
/// - 맵에는 서랍 가구 오브젝트 1개만 배치된다.
/// - 그 가구 안에 여러 개의 서랍 Transform이 있다.
/// - 열린 서랍 개수는 generator가 정하고,
///   어떤 서랍이 열릴지는 실제 drawer 개수 기준으로 seed 랜덤 선택한다.
/// </summary>
public class DrawerHintDisplay : MonoBehaviour
{
    [System.Serializable]
    public class DrawerStateTarget
    {
        [Header("대상 서랍")]
        public Transform drawerTransform;

        [Header("닫힘 상태")]
        public Vector3 closedLocalPosition;
        public Vector3 closedLocalEuler;

        [Header("열림 상태")]
        public Vector3 openedLocalPosition;
        public Vector3 openedLocalEuler;
    }

    [Header("서랍 가구 내부 서랍 목록")]
    [SerializeField] private List<DrawerStateTarget> drawers = new();

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    /// <summary>
    /// 열린 서랍 개수와 seed를 받아,
    /// 실제 등록된 drawers.Count 기준으로 랜덤한 서랍을 연다.
    /// </summary>
    public void SetOpenedDrawerCount(int openedCount, int seed)
    {
        if (drawers.Count == 0)
        {
            LogWarning("등록된 서랍이 없어 열린 개수 적용을 건너뜁니다.");
            return;
        }

        int clampedOpenedCount = Mathf.Clamp(openedCount, 0, drawers.Count);

        for (int i = 0; i < drawers.Count; i++)
            ApplyDrawerState(drawers[i], false);

        List<int> shuffledIndices = new List<int>(drawers.Count);
        for (int i = 0; i < drawers.Count; i++)
            shuffledIndices.Add(i);

        SeedRandom rng = new SeedRandom(seed);
        rng.Shuffle(shuffledIndices);

        for (int i = 0; i < clampedOpenedCount; i++)
        {
            int index = shuffledIndices[i];
            ApplyDrawerState(drawers[index], true);
        }

        Log($"열린 서랍 적용 완료 | requested={openedCount} | applied={clampedOpenedCount} | drawerCount={drawers.Count} | seed={seed}");
    }

    public void ResetToDefault()
    {
        for (int i = 0; i < drawers.Count; i++)
            ApplyDrawerState(drawers[i], false);

        Log("서랍 힌트 기본 상태로 초기화");
    }

    private void ApplyDrawerState(DrawerStateTarget drawer, bool isOpened)
    {
        if (drawer == null || drawer.drawerTransform == null)
            return;

        if (isOpened)
        {
            drawer.drawerTransform.localPosition = drawer.openedLocalPosition;
            drawer.drawerTransform.localRotation = Quaternion.Euler(drawer.openedLocalEuler);
            return;
        }

        drawer.drawerTransform.localPosition = drawer.closedLocalPosition;
        drawer.drawerTransform.localRotation = Quaternion.Euler(drawer.closedLocalEuler);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[DrawerHintDisplay] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[DrawerHintDisplay] {message}", this);
    }
}