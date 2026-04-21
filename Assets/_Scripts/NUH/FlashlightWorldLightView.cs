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

        if (worldLight == null) 
            worldLight =  GetComponentInChildren<Light>(true);
    }

    private void LateUpdate()
    {
        // 🛠️ [안전 차단기] 대상 아이템이 아직 네트워크에 소환되지 않았다면 모터 가동 중지!
        // (참고: ownerItem 변수 이름은 기공사가 선언한 변수명에 맞게 수정해 줘)
        if (ownerItem == null || ownerItem.Object == null || !ownerItem.Object.IsValid)
            return;

        // 전원이 확실히 들어왔을 때만 상태 갱신
        RefreshWorldLightState();
    }

    private void RefreshWorldLightState()
    {
        if (worldLight == null) 
            return;

        // 아직 Fusion에 attach 안 된 NetworkBehaviour면 [Network] 값 읽지 않음
        if (!CanReadNetworkState())
        {
            SetWorldLightEnabled(false);
            return;
        }

        // 손전등 장착 중이면 월드 라이트 OFF
        // 손전등 떨구면 월드 라이트 On
        bool shouldEnable = !ownerItem.NetIsEquipped;
        SetWorldLightEnabled(shouldEnable);
    }

    private bool CanReadNetworkState()
    {
        if (ownerItem == null)
            return false;

        if (ownerItem.Object == null)
            return false;

        return ownerItem.Object.IsValid;
    }

    private void SetWorldLightEnabled(bool enabled)
    {
        if (worldLight.enabled == enabled)
            return;

        worldLight.enabled = enabled;
    }
}
