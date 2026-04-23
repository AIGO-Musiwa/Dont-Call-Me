using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MultiplayerShadowManager : MonoBehaviour
{
    [Header("출력 설정")]
    [SerializeField] private int maxShadowLights = 2; // 플레이어당 조명 개수
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private float updateInterval = 0.3f;

    [Header("자동 스캔 설정")]
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        StartCoroutine(ShadowControlRoutine());
    }

    private IEnumerator ShadowControlRoutine()
    {
        while (true)
        {
            // 1. [핵심] 씬의 모든 조명을 매번 새로 가져옴 (새로 생성된 조명 누락 방지)
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);

            if (players == null || players.Length == 0)
            {
                // 플레이어가 없으면 모든 조명 그림자 OFF
                foreach (var l in allLights) l.shadows = LightShadows.None;
                yield return new WaitForSeconds(1.0f);
                continue;
            }

            // 2. 거리 계산 및 정렬
            var sortedLights = allLights
                .Where(l => l != null && l.enabled && l.type != LightType.Directional)
                .Select(l => {
                    float minDistance = players.Min(p => Vector3.Distance(p.transform.position, l.transform.position));
                    return new { light = l, dist = minDistance };
                })
                .OrderBy(x => x.dist)
                .ToList();

            // 3. [그림자 배분] 상위 N개만 켜고, 나머지는 '예외 없이' 끔
            for (int i = 0; i < sortedLights.Count; i++)
            {
                // 전체 개수가 아니라 (플레이어 수 * 인당 제한)으로 계산 가능
                if (i < (players.Length * maxShadowLights))
                {
                    // 거리 밖이면 끔
                    if (sortedLights[i].dist > scanRadius)
                        sortedLights[i].light.shadows = LightShadows.None;
                    else
                        sortedLights[i].light.shadows = LightShadows.Soft;
                }
                else
                {
                    // 순위 밖이면 무조건 끔
                    sortedLights[i].light.shadows = LightShadows.None;
                }
            }

            yield return new WaitForSeconds(updateInterval);
        }
    }
}