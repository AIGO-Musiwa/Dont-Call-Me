using Fusion;
using UnityEngine;

public class PlayerFlashlightView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerController _controller;
    [SerializeField] private Light _heldFlashlightLight;

    private Transform _cameraLightRoot;

    public void Initialize(PlayerController playerController)
    {
        _controller = playerController;
        _cameraLightRoot = _controller != null ? _controller.GetCameraLightRoot() : null;

        if(_cameraLightRoot != null && _heldFlashlightLight != null)
        {
            _heldFlashlightLight.transform.SetParent(_cameraLightRoot, false);
            _heldFlashlightLight.transform.localPosition = Vector3.zero;
            _heldFlashlightLight.transform.localRotation = Quaternion.identity;
        }

        RefreshHeldLightState();
    }

    private void LateUpdate()
    {
        RefreshHeldLightState();
    }

    private void RefreshHeldLightState()
    {
        if (_controller == null || _heldFlashlightLight == null)
            return;

        bool hasFlashlightEquipped = HasEquippedFlashlight();
        SetHeldLightEnabled(hasFlashlightEquipped);
    }

    private bool HasEquippedFlashlight()
    {
        return IsFlashlightNetworkObject(_controller.NetRightHandItem) ||
            IsFlashlightNetworkObject(_controller.NetLeftHandItem);
    }

    private bool IsFlashlightNetworkObject(NetworkObject networkObject)
    {
        if (networkObject == null)
            return false;

        return networkObject.GetComponent<FlashLightItem>() != null;
    }

    private void SetHeldLightEnabled(bool enabled)
    {
        if (_heldFlashlightLight.enabled == enabled)
            return;

        _heldFlashlightLight.enabled = enabled;
    }
}
