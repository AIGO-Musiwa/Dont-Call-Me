using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class NoiseEnhancerGimmick : MonoBehaviour, ISubCreatureGimmick
{
    [Header("노이즈 강화 설정")]
    [Tooltip("노이즈 강화 강도 0 ~ 1")]
    [Range(0f, 1f)]
    public float noiseIntensity = 0.9f;

    [Header("dB 배율 설정")]
    [Tooltip("무전 발행 dB 배율. 1 = 정상, 2 = 크리처가 2배 예민하게 반응")]
    public float voicedBMultiplier = 1.2f;

    [Header("코스트 배율 설정")]
    [Tooltip("무전 코스트 누적 배율. 1 = 정상, 2 = 2배 빠르게 누적")]
    public float costRateMultiplier = 1.5f;

    private SubCreatureSensor sensor;
    private SubCreatureController controller;

    // 노이즈 강화 중인 플레이어의 NetworkId 추적 (RPC 해제용)
    private readonly List<NetworkId> noisedPlayerIds = new();

    // 배율을 적용한 참조
    private CreatureWalkieTracker affectedTracker = null;
    private WalkieTalkieItem affectedWalkie = null;

    public void Setup(SubCreatureSensor sensor, SubCreatureController controller)
    {
        this.sensor = sensor;
        this.controller = controller;
    }

    #region ISubCreatureGimmick 구현

    public void OnActivate()
    {
        noisedPlayerIds.Clear();

        // 범위 안 플레이어 노이즈 강화
        ApplyToPlayersInRange();

        // 수신 구역 무전기 dB 배율 적용
        ApplyVoicedBMultiplier();

        // 메인 크리처 코스트 배율 적용
        ApplyCostMultiplier();

        Debug.Log($"[NoiseEnhancerGimmick] 활성화 ({controller.myZone})");
    }

    public void OnTick(float deltaTime)
    {
        // 범위 이탈 플레이어 노이즈 해제 감시
        UpdateNoiseRange();
    }

    public void OnDeactivate()
    {
        // 노이즈 강화 중인 모든 플레이어 해제
        foreach (NetworkId id in noisedPlayerIds)
            controller.RPC_SetEnhanceNoise(id, false, 0f);

        noisedPlayerIds.Clear();

        // 무전기 dB 배율 복구
        RestoreVoicedBMultiplier();

        // 코스트 배율 복구
        RestoreCostMultiplier();

        Debug.Log($"[NoiseEnhancerGimmick] 비활성화 ({controller.myZone})");
    }

    #endregion

    #region 노이즈 필터 처리

    private void ApplyToPlayersInRange()
    {
        foreach (PlayerController pc in sensor.GetPlayersInRange())
        {
            NetworkObject no = pc.GetComponent<NetworkObject>();
            if (no == null || noisedPlayerIds.Contains(no.Id)) continue;

            controller.RPC_SetEnhanceNoise(no.Id, true, noiseIntensity);
            noisedPlayerIds.Add(no.Id);
        }
    }

    private void UpdateNoiseRange()
    {
        // 현재 범위 안 플레이어 NetworkId 수집
        HashSet<NetworkId> inRangeIds = new();
        foreach (PlayerController pc in sensor.GetPlayersInRange())
        {
            NetworkObject no = pc.GetComponent<NetworkObject>();
            if (no != null) inRangeIds.Add(no.Id);
        }

        // 새로 범위에 들어온 플레이어 적용
        foreach (NetworkId id in inRangeIds)
        {
            if (!noisedPlayerIds.Contains(id))
            {
                controller.RPC_SetEnhanceNoise(id, true, noiseIntensity);
                noisedPlayerIds.Add(id);
            }
        }

        // 범위를 이탈한 플레이어 해제
        for (int i = noisedPlayerIds.Count - 1; i >= 0; i--)
        {
            if (!inRangeIds.Contains(noisedPlayerIds[i]))
            {
                controller.RPC_SetEnhanceNoise(noisedPlayerIds[i], false, 0f);
                noisedPlayerIds.RemoveAt(i);
            }
        }
    }

    #endregion

    #region 무전기 dB 배율 처리

    // 수신 구역 무전기에 dB 배율 적용
    private void ApplyVoicedBMultiplier()
    {
        WalkieTalkieItem walkie = WalkieTalkieManager.Instance?.GetWalkieTalkieByZone(controller.myZone);
        if (walkie == null) return;

        walkie.SetVoicedBMultiplier(voicedBMultiplier);
        affectedWalkie = walkie;
    }

    private void RestoreVoicedBMultiplier()
    {
        if (affectedWalkie == null) return;
        affectedWalkie.SetVoicedBMultiplier(1f);
        affectedWalkie = null;
    }

    #endregion

    #region 코스트 배율 처리

    private void ApplyCostMultiplier()
    {
        CreatureAI[] allCreatures = FindObjectsByType<CreatureAI>
            (FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (CreatureAI ai in allCreatures)
        {
            if (ai.myZone != controller.myZone) continue;

            CreatureWalkieTracker tracker = ai.GetComponent<CreatureWalkieTracker>();
            if (tracker == null) continue;

            tracker.SetCostRateMultiplier(costRateMultiplier);
            affectedTracker = tracker;
            break;
        }
    }

    private void RestoreCostMultiplier()
    {
        if (affectedTracker == null) return;
        affectedTracker.SetCostRateMultiplier(1f);
        affectedTracker = null;
    }

    #endregion
}
