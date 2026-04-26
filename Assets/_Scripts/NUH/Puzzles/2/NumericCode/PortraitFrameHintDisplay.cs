using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숫자 입력 퍼즐의 액자 힌트 표시 담당.
/// 
/// 역할
/// - 미리 배치된 9개의 직원 사진 액자 중
///   지정된 인덱스 목록에만 X 표시를 켠다.
/// </summary>
public class PortraitFrameHintDisplay : MonoBehaviour
{
    [System.Serializable]
    public class FrameMarkTarget
    {
        [Header("액자 표시 대상")]
        public GameObject xMarkObject;
    }

    [Header("액자 목록")]
    [SerializeField] private List<FrameMarkTarget> frames = new();

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;

    /// <summary>
    /// X 표시할 액자 인덱스 목록을 받아 해당 액자만 켠다.
    /// </summary>
    public void SetMarkedFrameIndices(IReadOnlyList<int> markedIndices)
    {
        if (frames.Count == 0)
        {
            LogWarning("등록된 액자가 없어 X 표시 적용을 건너뜁니다.");
            return;
        }

        for (int i = 0; i < frames.Count; i++)
            SetFrameMarked(frames[i], false);

        if (markedIndices == null)
        {
            LogWarning("markedIndices가 null이라 모든 액자를 기본 상태로 둡니다.");
            return;
        }

        for (int i = 0; i < markedIndices.Count; i++)
        {
            int index = markedIndices[i];
            if (index < 0 || index >= frames.Count)
                continue;

            SetFrameMarked(frames[index], true);
        }

        Log($"X 액자 인덱스 적용 완료 | markedCount={markedIndices.Count} | frameCount={frames.Count}");
    }

    public void ResetToDefault()
    {
        for (int i = 0; i < frames.Count; i++)
            SetFrameMarked(frames[i], false);

        Log("액자 힌트 기본 상태로 초기화");
    }

    private void SetFrameMarked(FrameMarkTarget frame, bool isMarked)
    {
        if (frame == null || frame.xMarkObject == null)
            return;

        frame.xMarkObject.SetActive(isMarked);
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[PortraitFrameHintDisplay] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[PortraitFrameHintDisplay] {message}", this);
    }
}