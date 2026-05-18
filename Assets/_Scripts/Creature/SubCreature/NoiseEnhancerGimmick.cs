using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class NoiseEnhancerGimmick : MonoBehaviour, ISubCreatureGimmick
{
    [Header("dB 배율 설정")]
    [Tooltip("무전 발행 dB 배율. 1 = 정상, 2 = 크리처가 2배 예민하게 반응")]
    public float voicedBMultiplier = 1.2f;

    [Header("코스트 배율 설정")]
    [Tooltip("무전 코스트 누적 배율. 1 = 정상, 2 = 2배 빠르게 누적")]
    public float costRateMultiplier = 1.5f;

    private SubCreatureSensor sensor;
    private SubCreatureController controller;

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
        // 수신 구역 무전기의 송신자 NoiseFilter 강화
        ApplySenderVoiceModulation(true);

        // 수신 구역 무전기 dB 배율 적용
        ApplyVoicedBMultiplier();

        // 메인 크리처 코스트 배율 적용
        ApplyCostMultiplier();

        Debug.Log($"[NoiseEnhancerGimmick] 활성화 ({controller.myZone})");
    }

    public void OnTick(float deltaTime) { }

    public void OnDeactivate()
    {
        // 송신자 NoiseFilter 강화 해제
        ApplySenderVoiceModulation(false);

        // 무전기 dB 배율 복구
        RestoreVoicedBMultiplier();

        // 코스트 배율 복구
        RestoreCostMultiplier();

        Debug.Log($"[NoiseEnhancerGimmick] 비활성화 ({controller.myZone})");
    }

    #endregion

    #region 노이즈 필터 처리

    private void ApplySenderVoiceModulation(bool enhanced)
    {
        WalkieTalkieItem walkie = WalkieTalkieManager.Instance?.GetWalkieTalkieByZone(controller.myZone);
        if (walkie == null) return;
        walkie.SetSenderVoiceModulation(enhanced);
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