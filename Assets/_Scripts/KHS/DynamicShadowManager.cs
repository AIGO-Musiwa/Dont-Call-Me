using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 멀티플레이 환경(최대 4인)에 최적화된 그림자 관리자.
/// 모든 플레이어의 위치를 고려하여 가장 근접한 조명의 그림자를 활성화한다.
/// </summary>
public class MultiplayerShadowManager : MonoBehaviour
{
    [Header("출력 설정")]
    [SerializeField] private int maxShadowLights = 4; // 플레이어가 4명이니 좀 더 넉넉하게 4~6개 추천
    [SerializeField] private float scanRadius = 25f;
    [SerializeField] private float updateInterval = 0.4f;

    [Header("자동 스캔 설정")]
    [SerializeField] private string playerTag = "Player"; // 플레이어 프리팹의 태그

    private List<Light> _additionalLights = new List<Light>();
    private GameObject[] _players;

    private void Start()
    {
        // 1. 씬 내의 모든 추가 조명 초기 스캔
        Light[] allSceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in allSceneLights)
        {
            if (l.type != LightType.Directional) _additionalLights.Add(l);
        }

        StartCoroutine(ShadowControlRoutine());
    }

    private System.Collections.IEnumerator ShadowControlRoutine()
    {
        while (true)
        {
            // 🛠️ [자동 찾기] 씬 내의 모든 플레이어를 태그로 실시간 스캔
            _players = GameObject.FindGameObjectsWithTag(playerTag);

            if (_players == null || _players.Length == 0)
            {
                yield return new WaitForSeconds(1.0f); // 플레이어가 없으면 천천히 대기
                continue;
            }

            // 🛠️ 멀티플레이어 거리 계산 엔진
            var sortedLights = _additionalLights
                .Where(l => l != null && l.enabled)
                .Select(l => {
                    // 모든 플레이어 중 이 조명과 가장 가까운 거리를 찾음 (Min 연산)
                    float minDistance = _players.Min(p => Vector3.Distance(p.transform.position, l.transform.position));
                    return new { light = l, dist = minDistance };
                })
                .Where(x => x.dist <= scanRadius)
                .OrderBy(x => x.dist)
                .ToList();

            // 상위 N개 그림자 배분
            for (int i = 0; i < sortedLights.Count; i++)
            {
                if (i < maxShadowLights)
                    sortedLights[i].light.shadows = LightShadows.Soft;
                else
                    sortedLights[i].light.shadows = LightShadows.None;
            }

            yield return new WaitForSeconds(updateInterval);
        }
    }
}