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

    [Networked] public float SessionDuration { get; private set; }

    #region Fusion Lifecycle

    public override void Spawned()
    {
        Instance = this;
        sessionEnded = false;
        evaluatePending = false;
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

        var players = GetAllPlayers();
        if (players.Length == 0) return;

        // 모든 플레이어가 아직 Normal -> 종료 조건 없음
        bool anyNonNormal = players.Any(p =>
        p.NetPlayerState == PlayerState.Escaped ||
        p.NetPlayerState == PlayerState.Dead);
        if (!anyNonNormal) return;

        // ── 클리어 ───────────────────────────────────────────
        // 한명 이상 탈츨 AND 나머지 전원이 Escaped 또는 Dead
        bool anyEscaped = players.Any(p => p.NetPlayerState == PlayerState.Escaped);
        bool allDone = players.All(p =>
        p.NetPlayerState == PlayerState.Escaped ||
        p.NetPlayerState == PlayerState.Dead);

        if (anyEscaped && allDone)
        {
            TriggerSessionEnd(isClear: true);
            return;
        }

        // ── 게임오버 ─────────────────────────────────────────
        // 조건 A: 전원 사망
        bool allDead = players.All(p => p.NetPlayerState == PlayerState.Dead);
        // 조건 B: 한 Zone 내 플레이어 전원 사망
        bool zoneWiped = IsAnyZoneWiped(players);

        if (allDead || zoneWiped)
            TriggerSessionEnd(isClear: false);
    }

    private bool IsAnyZoneWiped(PlayerController[] players)
    {
        foreach (Zone zone in Enum.GetValues(typeof(Zone)))
        {
            var inZone = players.Where(p => p.NetZone == zone).ToArray();
            if (inZone.Length == 0) continue;
            if (inZone.All(p => p.NetPlayerState == PlayerState.Dead))
                return true;
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
            payload.PuzzlesSolved = logger.PuzzlesSolved;
            payload.RadioUsed = logger.RadioUsed;

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

            var pc = data.GetPlayerController();

            payload.PlayerResults.Add(new PlayerResultData
            {
                Nickname = data.Nickname.ToString(),
                SlotIndex = data.SlotIndex,
                FinalState = data.FinalPlayerState,
                IsLocalPlayer = data.SlotIndex == localSlot
            });
        }

        ResultPayload.Pending = payload;
    }

    #endregion

    #region 이벤트 처리

    // 호스트 이탈 -> 타이틀
    private void HandleHostDisconnected()
    {
        Debug.LogWarning("[GameSessionManager] 호스트 이탈 → 타이틀 이동");
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);
    }

    // 클라이언트 이탈 -> Dead 처리 후 게임 유지
    private void HandlePlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;
 
        var pc = runner.GetPlayerObject(player)?.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.NetPlayerState = PlayerState.Dead;
    }

    #endregion

    #region 내부 유틸

    // 같은 Zone의 플레이어 전원이 Captured인 경우 구출이 불가능 하므로 사망처리
    // PlayerController.ServerEnterCaptured()에서 호출
    public void CheckZoneAllCaptured(Zone zone)
    {
        if (!Runner.IsServer) return;

        var allPlaers = GetAllPlayers();
        var zonemates = GetAllPlayers().Where(p => p.NetZone == zone).ToArray();

        // Normal 상태인 플레이가 한명이라도 있으면 구출 가능 -> 판정 안함
        bool anyRescuer = zonemates.Any(p => p.NetPlayerState == PlayerState.Normal);
        if (anyRescuer) return;

        Debug.Log($"[GameSessionManager] Zone {zone} 전원 구출 불가 → 즉시 사망 처리");

        foreach (var p in zonemates)
        {
            if (p.NetPlayerState == PlayerState.Captured)
                p.ServerEnterDead();
        }
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

    #endregion
}
