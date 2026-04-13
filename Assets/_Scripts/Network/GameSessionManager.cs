using Fusion;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Header("결과 UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private ResultUI resultUI;

    private bool sessionEnded;

    #region Fusion Lifecycle

    public override void Spawned()
    {
        Instance = this;

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

    #endregion

    #region 종료 조건 판정

    // PlayerController.NetPlayerState 변경 시 OnChanged에서 호출.
    public void EvaluateEndCondition()
    {
        if (!Runner.IsServer) return;
        if (sessionEnded) return;

        var players = GetAllPlayers();
        if (players.Length == 0) return;

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
        Debug.Log($"[GameSessionManager] 세션 종료 | 클리어={isClear}");
        Rpc_ShowResult(isClear);
    }

    #endregion

    #region RPC

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_ShowResult(NetworkBool isClear)
    {
        resultPanel.SetActive(true);
        resultUI?.Setup(isClear);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void Rpc_RequestReturnToLobby()
    {
        GameLauncher.Instance?.ReturnToLobby();
    }

    #endregion

    #region 이벤트 처리

    // 호스트 이탈 -> 타이틀
    private void HandleHostDisconnected()
    {
        Debug.LogWarning("[GameSessionManager] 호스트 이탈 → 타이틀 이동");
        SceneManager.sceneLoaded += OnTitleSceneLoaded;
        SceneManager.LoadScene(SceneNames.TITLE_INDEX);
    }
    
    private void OnTitleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex != SceneNames.TITLE_INDEX) return;
        SceneManager.sceneLoaded -= OnTitleSceneLoaded;
        FindFirstObjectByType<TitleManager>()?.Show();
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
