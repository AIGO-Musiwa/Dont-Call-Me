using UnityEngine;

public class FlashlightWorldLightView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private FlashLightItem ownerItem;
    [SerializeField] private Light worldLight;

    private void Awake()
    {
        if (ownerItem == null)
            ownerItem = GetComponent<FlashLightItem>();

        if (worldLight == null) GetComponentInChildren<Light>(true);
    }

    private void LateUpdate()
    {
        RefreshWorldLightState();
    }

    private void RefreshWorldLightState()
    {
        if (ownerItem == null || worldLight == null) 
            return;

        // 손전등 장착 중이면 월드 라이트 OFF
        // 손전등 떨구면 월드 라이트 On
        bool shouldEnable = !ownerItem.NetIsEquipped;
        SetWorldLightEnabled(shouldEnable);
    }

    private void SetWorldLightEnabled(bool enabled)
    {
        if (worldLight.enabled == enabled)
            return;

        worldLight.enabled = enabled;
    }
}
