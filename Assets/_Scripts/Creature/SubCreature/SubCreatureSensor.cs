using System.Collections.Generic;
using UnityEngine;

public class SubCreatureSensor : MonoBehaviour
{
    [Header("플레이어 레이어 마스크")]
    public LayerMask playerLayerMask;

    [Header("벽/바닥 레이어 마스크")]
    public LayerMask wallLayerMask;

    // 현재 감지 범위 안에 있는 Normal 상태 플레이어 목록
    private readonly List<PlayerController> playersInRange = new();

    private void Awake()
    {
        // BoxCollider가 반드시 Trigger여야 함
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 레이어 필터
        if((playerLayerMask.value & (1 << other.gameObject.layer)) == 0) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;
        if (pc.NetPlayerState != PlayerState.Normal) return;
        if (pc.NetHideState != HideState.None) return;
        if (playersInRange.Contains(pc)) return;

        // 서브 크리처와 플레이어 사이에 벽/바닥이 있으면 다른 층으로 간주
        if (IsBlockedByWall(pc.transform.position)) return;

        playersInRange.Add(pc);
    }

    private void OnTriggerExit(Collider other)
    {
        // 레이어 필터
        if ((playerLayerMask.value & (1 << other.gameObject.layer)) == 0) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        playersInRange.Remove(pc);
    }

    // 감지 범위 안에 Normal 상태 플레이어 목록을 반환
    public List<PlayerController> GetPlayersInRange()
    {
        // 상태가 변한 플레이어 제거
        // 상태가 변하거나 숨거나 벽에 막힌 플레이어 제거
        playersInRange.RemoveAll(pc =>
            pc == null ||
            pc.NetPlayerState != PlayerState.Normal ||
            pc.NetHideState != HideState.None ||
            IsBlockedByWall(pc.transform.position));

        return playersInRange;
    }

    // 감지 범위 안에 플레이어가 한 명 이상 있는지 반환
    public bool HasPlayerInRange()
    {
        return GetPlayersInRange().Count > 0;
    }

    // 특정 플레이어가 감지 범위 안에 있는지 반환
    public bool IsPlayerInRange(PlayerController pc)
    {
        return playersInRange.Contains(pc);
    }

    // 감지 목록 초기화
    public void ClearPlayers()
    {
        playersInRange.Clear();
    }

    // 서브크리처와 타겟 사이에 벽/바닥이 있으면 true 반환
    private bool IsBlockedByWall(Vector3 targetPos)
    {
        if (wallLayerMask.value == 0) return false;

        Vector3 dir = targetPos - transform.position;
        float dist = dir.magnitude;

        return Physics.Raycast(
            transform.position,
            dir.normalized,
            dist,
            wallLayerMask,
            QueryTriggerInteraction.Ignore);
    }
}
