using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 문양 레버 퍼즐 힌트
/// 같은 정답 시드를 받아 퍼즐과 동일한 정답 상태를 재생성
/// </summary>
public class SymbolLeverHint : MonoBehaviour, IPuzzleSeedReceiver
{
    [Header("힌트 표시 데이터")]
    [SerializeField] private int hintCount = 6;                         // 힌트 개수
    [SerializeField] private List<bool> hintStates = new();             // 표시할 힌트 상태
    [SerializeField] private List<Transform> arrowTransforms = new();   // 실제 화살표 오브젝트들

    [Header("화살표 회전값")]
    [SerializeField] private Vector3 upArrowEuler = Vector3.zero;                   // 위쪽 화살표 회전값
    [SerializeField] private Vector3 downArrowEuler = new Vector3(0f, 0f, 180f);    // 아래쪽 화살표 회전값

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true;                // 디버그 로그 출력 여부


    /// <summary>
    /// 퍼즐과 동일한 정답 시드를 받아 같은 힌트 상태를 만듦
    /// false = 위 / true = 아래
    /// </summary>
    public void ApplyAnswerSeed(int seed)
    {
        System.Random rng = new(seed);

        EnsureStateListSize(hintStates, hintCount, false);

        for(int i = 0; i < hintCount; i++)
        {
            hintStates[i] = rng.Next(0, 2) == 1;
        }

        ApplyArrowVisuals();

        Log($"힌트 시드 적용 완료 : {seed}");
    }

    /// <summary>
    /// 현재 hintStates를 기준으로 화살표 오브젝트 방향을 적용
    /// </summary>
    private void ApplyArrowVisuals()
    {
        int count = Mathf.Min(hintStates.Count, arrowTransforms.Count);

        for(int i = 0; i < count; i++)
        {
            Transform arrow = arrowTransforms[i];
            if (arrow == null)
                continue;

            Vector3 targetEuler = hintStates[i] ? downArrowEuler : upArrowEuler;
            arrow.localRotation = Quaternion.Euler(targetEuler);
        }
    }

    /// <summary>
    /// 리스트 크기를 hintCount에 맞춤
    /// </summary>
    private void EnsureStateListSize(List<bool> list, int targetCount, bool defaultValue)
    {
        if (list == null)
            return;

        while (list.Count < targetCount)
            list.Add(defaultValue);

        while (list.Count > targetCount)
            list.RemoveAt(list.Count - 1);
    }

    private void Log(string m)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[SymbolLeverHint {m}", this);
    }


}
