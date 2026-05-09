using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    // ── 상태 ──────────────────────────────────────────────────────────
    private bool sessionEnded;
    private float sessionStartTime;

    // 동일 틱 안에서 여러 플레이어의 상태가 한꺼번에 바뀌어도
    // 모든 변경이 끝난 뒤 딱 한 번만 평가하기 위한 지연 플래그
    private bool evaluatePending;

    private bool escapeUnlocked;

    // 플레이어 상태 캐시
    private readonly Dictionary<PlayerRef, PlayerState> playerStateCache = new();
    private readonly Dictionary<PlayerRef, Zone> playerZoneCache = new();

    [Networked] public float SessionDuration { get; private set; }

    #region Fusion Lifecycle

    public override void Spawned()
    {
        Instance = this;
        sessionEnded = false;
        evaluatePending = false;
        escapeUnlocked = false;
        sessionStartTime = Time.time;

        var launcher = GameLauncher.Instance;
        if (launcher == null) return;

        launcher.OnHostDisconnected += HandleHostDisconnected;
        launcher.OnPlayerLeftEvent += HandlePlayerLeft;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Instance = null;

        var launcher = GameLauncher.Instance;
        if (launcher == null) return;

        launcher.OnHostDisconnected -= HandleHostDisconnected;
        launcher.OnPlayerLeftEvent -= HandlePlayerLeft;
    }

    // 예약된 평가를 현재 틱이 끝난 뒤 실행
    // 한 틱 안에서 여러 플레이어 상태가 동시에 바뀌는 경우 오판정 방지
    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer || !evaluatePending) return;
        evaluatePending = false;
        EvaluateEndConditionNow();
    }

    #endregion

    #region 종료 조건 판정

    // PlayerController.NetPlayerState OnChanged에서 호출
    public void EvaluateEndCondition()
    {
        if (!Runner.IsServer || sessionEnded) return;
        evaluatePending = true;
    }

    // PlayerController.NetPlayerState 변경 시 OnChanged에서 호출.
    public void EvaluateEndConditionNow()
    {
        if (sessionEnded)
        {
            Debug.Log("[GameSessionManager] sessionEnded=true라 무시됨");
            return;
        }

        if (playerStateCache.Count == 0) return;

        // 모든 플레이어가 아직 Normal -> 종료 조건 없음
        bool anyNonNormal = playerStateCache.Values.Any(s => s == PlayerState.Escaped || s == PlayerState.Dead);
        if (!anyNonNormal) return;

        // ── 클리어 ───────────────────────────────────────────
        // 한명 이상 탈츨 AND 나머지 전원이 Escaped 또는 Dead
        bool anyEscaped = playerStateCache.Values.Any(s => s == PlayerState.Escaped);
        bool allDone = playerStateCache.Values.All(s =>
        s == PlayerState.Escaped ||
        s == PlayerState.Dead);

        if (anyEscaped && allDone)
        {
            TriggerSessionEnd(isClear: true);
            return;
        }

        // ── 게임오버 ─────────────────────────────────────────
        // 조건 A: 전원 사망
        bool allDead = playerStateCache.Values.All(s => s == PlayerState.Dead);

        // 조건 B: 한 Zone 내 플레이어 전원 사망 (탈출 조건이 완성되면 ZoneWiped 판정 비활성)
        bool zoneWiped = !escapeUnlocked && IsAnyZoneWipedFromCache();

        if (allDead || zoneWiped)
            TriggerSessionEnd(isClear: false);
    }

    // 캐시 기반 Zone 전멸 판정
    private bool IsAnyZoneWipedFromCache()
    {
        foreach (Zone zone in Enum.GetValues(typeof(Zone)))
        {
            var inZone = playerZoneCache
                .Where(kvp => kvp.Value == zone)
                .Select(kvp => kvp.Key)
                .ToList();

            if (inZone.Count == 0) continue;

            bool allDeadInZone = inZone.All(p => GetCachedState(p) == PlayerState.Dead);
            if (allDeadInZone) return true;
        }
        return false;
    }

    private void TriggerSessionEnd(bool isClear)
    {
        sessionEnded = true;
        SessionDuration = Time.time - sessionStartTime;

        Debug.Log($"[GameSessionManager] 세션 종료 | 클리어={isClear} | 시간={SessionDuration:F1}s");

        Rpc_MoveToLobbyWithResult(isClear, SessionDuration);
    }

    #endregion

    #region RPC

    // 클라이언트가 각자 오브젝트 데이터 복사 후 로비 이동
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_MoveToLobbyWithResult(NetworkBool isClear, float duration)
    {
        // 전 클라이언트가 각자 PlayerController가 살아있는 시점에 payload 빌드
        BuildResultPayload(isClear, duration);

        // 복사 후 호스트만 ReturnToLobby() 호출 -> 나머지는 자동으로 따라옴
        if (Runner.IsServer)
            GameLauncher.Instance.ReturnToLobby();
    }

    // 씬 이동 시 사라지는 오브젝트의 데이터를 ResultPayload에 미리 복사
    private void BuildResultPayload(bool isClear, float duration)
    {
        var payload = new PendingResult
        {
            IsClear = isClear,
            Duration = duration
        };

        // GameEventLogger 데이터 복사
        var logger = GameEventLogger.Instance;
        if (logger != null)
        {
            payload.PuzzlesSolvedZoneA = logger.PuzzlesSolvedZoneA;
            payload.PuzzlesSolvedZoneB = logger.PuzzlesSolvedZoneB;
            payload.RadioUsedZoneA = logger.RadioUsedZoneA;
            payload.RadioUsedZoneB = logger.RadioUsedZoneB;

            foreach (var entry in logger.NetLog)
                payload.TimelineLog.Add(entry);
        }

        // 플레이어 최종 상태 복사 (NetPlayerState를 FinalState에 저장)
        var localData = Runner.GetPlayerObject(Runner.LocalPlayer)?.GetComponent<PlayerData>();
        int localSlot = localData != null ? localData.SlotIndex : -1;

        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
            if (data == null) continue;

            PlayerState finalState = data.FinalPlayerState == PlayerState.Normal
                ? PlayerState.Dead
                : data.FinalPlayerState;

            payload.PlayerResults.Add(new PlayerResultData
            {
                Nickname = data.Nickname.ToString(),
                SlotIndex = data.SlotIndex,
                FinalState = finalState,
                IsLocalPlayer = data.SlotIndex == localSlot,
                PlayerZone = data.PlayerZone
            });
        }

        ResultPayload.Pending = payload;
    }

    #endregion

    #region 이벤트 처리

    // 플레이어 게임 나가기
    public async void LeaveGame()
    {
        var launcher = GameLauncher.Instance;
        if (launcher == null)
        {
            SceneManager.LoadScene(SceneNames.TITLE_INDEX);
            return;
        }

        // LeaveRoom 전에 로컬 플레이어 Dead 처리 및 Despawn
        var runner = launcher.Runner;

        if (runner != null && !runner.IsServer)
        {
            // 클라이언트: 로컬 플레이어 오브젝트 직접 처리
            var localObject = runner.GetPlayerObject(runner.LocalPlayer);
            if (localObject != null)
            {
                var pc = localObject.GetComponent<PlayerData>()?.GetPlayerController();
                pc?.ServerEnterDead();
                runner.Despawn(pc.GetComponent<NetworkObject>());
            }
        }

        await GameLauncher.Instance.LeaveRoom();
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);
    }

    // 호스트 이탈 -> 타이틀
    private void HandleHostDisconnected()
    {
        Debug.LogWarning("[GameSessionManager] 호스트 이탈 → 타이틀 이동");
        if (GameLauncher.Instance != null)
            GameLauncher.Instance.PendingErrorMessage = "호스트 연결이 끊겼습니다.";
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);
    }

    // 클라이언트 이탈 -> Dead 처리 후 게임 유지
    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        var playerObject = runner.GetPlayerObject(player);
        if (playerObject == null) return;       // 이미 LeaveGame에서 처리된 경우

        var data = playerObject.GetComponent<PlayerData>();
        if (data == null) return;

        var pc = data.GetPlayerController();
        if (pc == null) return;

        // LeaveGame를 거치지 않은 비정상 이탈
        pc?.ServerEnterDead();
        runner.Despawn(playerObject);
        Debug.Log($"[GameSessionManager] 이탈 처리 | Player={player}");
    }

    #endregion

    #region 내부 유틸

    // 전체 플레이어 상태 / 구역 캐시 초기화
    public void InitPlayerStateCache()
    {
        playerStateCache.Clear();
        playerZoneCache.Clear();

        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
            if (data == null) continue;

            playerStateCache[player] = PlayerState.Normal;
            playerZoneCache[player] = data.PlayerZone;
        }
        Debug.Log($"[GameSessionManager] 플레이어 캐시 초기화 | 인원={playerStateCache.Count}");
    }

    // 캐시 상태 갱신
    public void UpdatePlayerStateCache(PlayerRef player, PlayerState state)
    {
        if (playerStateCache.ContainsKey(player))
            playerStateCache[player] = state;
    }

    private PlayerController[] GetAllPlayers()
    {
        var list = new List<PlayerController>();
        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
            var pc = data?.GetPlayerController();
            if (pc != null) list.Add(pc);
        }
        return list.ToArray();
    }

    private PlayerState GetCachedState(PlayerRef player)
    {
        return playerStateCache.TryGetValue(player, out var state) ? state : PlayerState.Dead;
    }

    private Zone GetCachedZone(PlayerRef player)
    {
        return playerZoneCache.TryGetValue(player, out var zone) ? zone : Zone.ZoneA;
    }

    #endregion

    #region 외부 호출 함수


    // 같은 Zone의 플레이어 전원이 Captured인 경우 구출이 불가능 하므로 사망처리
    // PlayerController.ServerEnterCaptured()에서 호출
    public void CheckZoneAllCaptured(Zone zone)
    {
        if (!Runner.IsServer) return;

        // PlayerData를 직접 순회
        var zonemates = new List<(PlayerData data, PlayerController pc)>();
        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerData>();
            if (data == null) continue;

            var pc = data.GetPlayerController();
            if (pc == null || pc.NetZone != zone) continue;

            zonemates.Add((data, pc));
        }

        if (zonemates.Count == 0) return;

        // Normal 상태인 플레이어가 한 명이라도 있으면 구출 가능 -> 판정 안함
        bool anyRescuer = zonemates.Any(pair => pair.pc.NetPlayerState == PlayerState.Normal);

        if (anyRescuer) return;

        Debug.Log($"[GameSessionManager] Zone {zone} 전원 구출 불가 → 즉시 사망 처리");

        foreach (var (data, pc) in zonemates)
        {
            if (pc.NetPlayerState == PlayerState.Captured)
                pc.ServerEnterDead();
        }
    }

    // 탈출 조건 달성 시 이 메소드 호출
    public void notifyEscapeUnlocked()
    {
        if (!Runner.IsServer) return;
        escapeUnlocked = true;
        Debug.Log("[GameSessionManager] 탈출 조건 달성 → ZoneWiped 판정 비활성화");
    }

    // 탈출자가 발생한 Zone의 Captured 플레이어 Dead 처리
    public void CheckZoneEscaped(Zone zone)
    {
        if (!Runner.IsServer) return;

        var allPlayers = GetAllPlayers();
        foreach (var p in allPlayers)
        {
            if (p.NetZone == zone && p.NetPlayerState == PlayerState.Captured)
                p.ServerEnterDead();
        }
    }

    #endregion
}
