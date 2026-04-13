using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugSessionTester : MonoBehaviour
{
    private void Update()
    {
        if (!Application.isPlaying) return;

        var runner = GameLauncher.Instance?.Runner;
        if (runner == null || !runner.IsServer) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame)
            SetLocalPlayerState(runner, PlayerState.Escaped);

        if (keyboard.f2Key.wasPressedThisFrame)
            SetLocalPlayerState(runner, PlayerState.Dead);

        if (keyboard.f3Key.wasPressedThisFrame)
            SetAllPlayerState(runner, PlayerState.Dead);

        if (keyboard.f4Key.wasPressedThisFrame)
            SetAllPlayerState(runner, PlayerState.Escaped);

        if (keyboard.f5Key.wasPressedThisFrame)
            SetZonePlayerState(runner, Zone.ZoneA, PlayerState.Dead);
    }

    private void SetAllPlayerState(NetworkRunner runner, PlayerState state)
    {
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            pc.NetPlayerState = state;
        }

        // ChangeDetector가 못 잡을 경우를 대비해 직접 호출
        GameSessionManager.Instance?.EvaluateEndCondition();

        Debug.Log($"[DebugSessionTester] 전원 → {state} | 대상 수: {controllers.Length}");
    }

    private void SetLocalPlayerState(NetworkRunner runner, PlayerState state)
    {
        var controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var pc in controllers)
        {
            if (pc.HasInputAuthority)
            {
                pc.NetPlayerState = state;
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
                pc.NetPlayerState = state;
        }
        Debug.Log($"[DebugSessionTester] {zone} 전원 → {state}");
    }
}
