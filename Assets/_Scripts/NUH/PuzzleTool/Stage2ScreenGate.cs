using System.Collections;
using UnityEngine;

/// <summary>
/// Stage2 퍼즐 화면 표시 게이트.
/// 
/// 역할
/// - 각 Stage2 퍼즐이 자기 Stage2ScreenRoot를 직접 관리한다.
/// - Stage2ScreenRoot 참조는 PuzzleSpawnEntry.Stage2ScreenRoot만 사용한다.
/// - StageManager의 Zone별 Stage1 완료 Networked 상태를 기준으로 화면을 켜고 끈다.
/// </summary>
public class Stage2ScreenGate : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PuzzleSpawnEntry spawnEntry; // 이 퍼즐의 SpawnEntry

    [Header("초기 상태")]
    [SerializeField] private bool hideUntilStageUnlocked = true; // Stage1 완료 전까지 숨길지 여부

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = true; // 디버그 로그 출력 여부

    private Coroutine _refreshRoutine; // SpawnZone 도착 대기 코루틴
    private bool _hasAppliedState;     // 마지막 상태 적용 여부
    private bool _lastAppliedActive;   // 마지막 적용 active 값

    private GameObject Stage2ScreenRoot
    {
        get
        {
            if (spawnEntry == null)
                return null;

            return spawnEntry.Stage2ScreenRoot;
        }
    }

    private void Awake()
    {
        CacheReferences();

        // Stage2 화면 기본 정책은 Stage1 완료 전 숨김.
        // 단, 실제 화면 루트는 PuzzleSpawnEntry.Stage2ScreenRoot만 사용한다.
        if (hideUntilStageUnlocked)
            ApplyScreenActive(false, true);
    }

    private void OnEnable()
    {
        RestartRefreshRoutine();
    }

    private void OnDisable()
    {
        if (_refreshRoutine != null)
        {
            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }
    }

    /// <summary>
    /// StageManager가 Networked 상태 변경 시 호출하는 화면 갱신 함수.
    /// </summary>
    public void RefreshScreenState()
    {
        CacheReferences();

        GameObject screenRoot = Stage2ScreenRoot;
        if (screenRoot == null)
            return;

        if (spawnEntry == null || spawnEntry.ProgressTarget == null)
        {
            if (hideUntilStageUnlocked)
                ApplyScreenActive(false);

            return;
        }

        PuzzleInteractableBase progressTarget = spawnEntry.ProgressTarget;

        if (!progressTarget.HasSpawnZone)
        {
            if (hideUntilStageUnlocked)
                ApplyScreenActive(false);

            return;
        }

        StageManager stageManager = StageManager.Instance;
        if (stageManager == null)
        {
            if (hideUntilStageUnlocked)
                ApplyScreenActive(false);

            return;
        }

        Zone myZone = progressTarget.SpawnZone;
        bool unlocked = stageManager.IsZoneStage1Completed(myZone);

        ApplyScreenActive(unlocked);
    }

    /// <summary>
    /// 필요한 참조를 자동 보정한다.
    /// </summary>
    private void CacheReferences()
    {
        if (spawnEntry == null)
            spawnEntry = GetComponent<PuzzleSpawnEntry>();
    }

    /// <summary>
    /// 네트워크 스폰 직후 SpawnZone이 늦게 도착할 수 있으므로 몇 프레임 동안 상태를 재확인한다.
    /// </summary>
    private void RestartRefreshRoutine()
    {
        if (_refreshRoutine != null)
            StopCoroutine(_refreshRoutine);

        _refreshRoutine = StartCoroutine(CoRefreshUntilReady());
    }

    private IEnumerator CoRefreshUntilReady()
    {
        yield return null;

        for (int i = 0; i < 120; i++)
        {
            RefreshScreenState();

            if (spawnEntry != null &&
                spawnEntry.Stage2ScreenRoot != null &&
                spawnEntry.ProgressTarget != null &&
                spawnEntry.ProgressTarget.HasSpawnZone &&
                StageManager.Instance != null)
            {
                _refreshRoutine = null;
                yield break;
            }

            yield return null;
        }

        LogWarning("Stage2ScreenGate 초기 상태 갱신 실패 또는 SpawnZone 미도착");
        _refreshRoutine = null;
    }

    /// <summary>
    /// Stage2 화면 루트 active 상태를 적용한다.
    /// </summary>
    private void ApplyScreenActive(bool active, bool force = false)
    {
        GameObject screenRoot = Stage2ScreenRoot;
        if (screenRoot == null)
            return;

        if (!force && _hasAppliedState && _lastAppliedActive == active)
            return;

        screenRoot.SetActive(active);

        _hasAppliedState = true;
        _lastAppliedActive = active;

        Log($"Stage2 화면 상태 적용 | active={active}");
    }

    private void Log(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[Stage2ScreenGate] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.LogWarning($"[Stage2ScreenGate] {message}", this);
    }
}