using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugSessionTester : MonoBehaviour
{
    private void Update()
    {
        if (!Application.isPlaying) return;

        var runner = GameLauncher.Instance?.Runner;
        if (runner == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 해당 플레이어 탈출
        if (keyboard.f1Key.wasPressedThisFrame)
            SetLocalPlayerState(runner, PlayerState.Escaped);

        // 해당 플레이어 사망
        if (keyboard.f2Key.wasPressedThisFrame)
            SetLocalPlayerState(runner, PlayerState.Dead);

        if (!runner.IsServer) return;

        // 전체 사망
        if (keyboard.f3Key.wasPressedThisFrame)
            SetAllPlayerState(runner, PlayerState.Dead);

        // 전체 탈출
        if (keyboard.f4Key.wasPressedThisFrame)
            SetAllPlayerState(runner, PlayerState.Escaped);

        // 한 구역 사망
        if (keyboard.f5Key.wasPressedThisFrame)
            SetZonePlayerState(runner, Zone.ZoneA, PlayerState.Dead);

        // ── 통계 테스트 ───────────────────────────────────────────────
        // F6: ZoneA 퍼즐 1개 완료
        if (keyboard.f6Key.wasPressedThisFrame)
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneA);
            Debug.Log($"[DebugSessionTester] ZoneA 퍼즐 완료 | A={GameEventLogger.Instance?.PuzzlesSolvedZoneA}");
        }

        // F7: ZoneB 퍼즐 1개 완료
        if (keyboard.f7Key.wasPressedThisFrame)
        {
            GameEventLogger.Instance?.AddPuzzleSolved(Zone.ZoneB);
            Debug.Log($"[DebugSessionTester] ZoneB 퍼즐 완료 | B={GameEventLogger.Instance?.PuzzlesSolvedZoneB}");
        }

        // F8: ZoneA 라디오 1회 사용
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            GameEventLogger.Instance?.AddRadioUsed(Zone.ZoneA);
            Debug.Log($"[DebugSessionTester] ZoneA 라디오 사용 | A={GameEventLogger.Instance?.RadioUsedZoneA}");
        }

        // F9: ZoneB 라디오 1회 사용
        if (keyboard.f9Key.wasPressedThisFrame)
        {
            GameEventLogger.Instance?.AddRadioUsed(Zone.ZoneB);
            Debug.Log($"[DebugSessionTester] ZoneB 라디오 사용 | B={GameEventLogger.Instance?.RadioUsedZoneB}");
        }
    }

    private void SetAllPlayerState(NetworkRunner runner, PlayerState state)
    {
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (state == PlayerState.Dead)
                pc.ServerEnterDead();
            else if (state == PlayerState.Escaped)
                pc.ServerEnterEscaped();
            else
                pc.NetPlayerState = state;
        }

        Debug.Log($"[DebugSessionTester] 전원 → {state} | 대상 수: {controllers.Length}");
    }

    private void SetLocalPlayerState(NetworkRunner runner, PlayerState state)
    {
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.HasInputAuthority)
            {
                pc.Rpc_DebugSetState(state);    // RPC로 Host에게 요청
                Debug.Log($"[DebugSessionTester] 로컬 플레이어 → {state}");
                return;
            }
        }
        Debug.LogWarning("[DebugSessionTester] 로컬 PlayerController 없음");
    }

    private void SetZonePlayerState(NetworkRunner runner, Zone zone, PlayerState state)
    {
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.HasStateAuthority && pc.NetZone == zone)
            {
                if (state == PlayerState.Dead)
                    pc.ServerEnterDead();
                else if (state == PlayerState.Escaped)
                    pc.ServerEnterEscaped();
                else
                    pc.NetPlayerState = state;
            }
        }
        Debug.Log($"[DebugSessionTester] {zone} 전원 → {state}");
    }
}
