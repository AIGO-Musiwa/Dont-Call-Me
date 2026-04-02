using Fusion;
using UnityEngine;

/// <summary>
/// 손전등 아이템
/// ItemObject 상속
///
/// Light 처리 확정 사항:
///   - 드랍해도 Light OFF 안 함 (alwaysOn)
///   - 드는 사람의 카메라 자식으로 Light SetParent (역할/로컬/원격 구분 없음)
///   - 드랍 시 itemLightRoot 자식으로 SetParent → 드랍 시점 로컬 방향 고정
///   - UpdateLightDirection() 없음 — 카메라 자식이므로 자동 추적
/// </summary>
public class FlashlightItem : ItemObject
{
    // ─────────────────────────────────────────
    // Inspector 설정값
    // ─────────────────────────────────────────
    [Header("Light 설정")]
    [SerializeField] private GameObject lightObject;     // Light 컴포넌트를 담은 GameObject
    [SerializeField] private Transform  itemLightRoot;   // 드랍 시 Light 귀속 슬롯 (아이템 자식)

    [SerializeField] private float range     = 15f;
    [SerializeField] private float spotAngle = 45f;
    [SerializeField] private float intensity = 2f;

    // ─────────────────────────────────────────
    // 로컬 변수
    // ─────────────────────────────────────────
    private Light _light;
    private PlayerController _currentHolder;

    // ─────────────────────────────────────────
    // Fusion 생명주기
    // ─────────────────────────────────────────
    public override void Spawned()
    {
        base.Spawned();

        _light = lightObject.GetComponent<Light>();

        // Light 초기 설정
        if (_light != null)
        {
            _light.type      = LightType.Spot;
            _light.range     = range;
            _light.spotAngle = spotAngle;
            _light.intensity = intensity;
        }

        // 스폰 시점에 itemLightRoot 자식으로 초기 배치
        if (lightObject != null && itemLightRoot != null)
        {
            lightObject.transform.SetParent(itemLightRoot);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;
        }

        // 항상 ON — 이후 절대 끄지 않음
        SetLightEnabled(true);
    }

    // ─────────────────────────────────────────
    // IInteractable 오버라이드
    // ─────────────────────────────────────────
    public override string GetPromptText() => "손전등 집기";

    // ─────────────────────────────────────────
    // 장착 / 드랍 오버라이드
    // ─────────────────────────────────────────

    /// <summary>
    /// 드는 사람의 카메라 기준으로 Light 부착
    /// 역할 여부, 로컬/원격 여부 구분 없이 단일 로직
    /// </summary>
    public override void OnEquipped(PlayerController actor, HandSlot slot)
    {
        base.OnEquipped(actor, slot);
        _currentHolder = actor;

        if (lightObject == null) return;

        // 드는 사람의 카메라 슬롯 참조
        Transform holderCameraRoot = actor.GetCameraLightRoot();

        if (holderCameraRoot != null)
        {
            lightObject.transform.SetParent(holderCameraRoot);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            // 카메라 슬롯을 찾을 수 없는 경우 아이템 루트에 유지
            Debug.LogWarning($"[FlashlightItem] {actor.name}의 CameraLightRoot를 찾을 수 없습니다.");
            lightObject.transform.SetParent(itemLightRoot);
        }
    }

    /// <summary>
    /// 드랍 시 itemLightRoot 자식으로 이동
    /// worldPositionStays = true → 드랍 시점의 월드 방향이 로컬 방향으로 고정
    /// Light는 끄지 않음 (alwaysOn)
    /// </summary>
    public override void OnDropped()
    {
        base.OnDropped();
        _currentHolder = null;

        if (lightObject == null || itemLightRoot == null) return;

        // 드랍 시점 월드 방향 보존하며 아이템 루트로 귀속
        lightObject.transform.SetParent(itemLightRoot, worldPositionStays: true);
    }

    // ─────────────────────────────────────────
    // 내부 유틸리티
    // ─────────────────────────────────────────
    private void SetLightEnabled(bool enabled)
    {
        if (_light != null)
            _light.enabled = enabled;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 lightObject가 itemLightRoot 자식인지 확인
        if (lightObject != null && itemLightRoot != null)
        {
            if (!lightObject.transform.IsChildOf(itemLightRoot))
                Debug.LogWarning("[FlashlightItem] lightObject는 itemLightRoot의 자식이어야 합니다.");
        }
    }
#endif
}
