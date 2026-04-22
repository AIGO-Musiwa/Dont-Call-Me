using UnityEngine;
using UnityEngine.Rendering;

public class PlayerVisualSetter : MonoBehaviour
{
    [Header("메쉬")]
    [SerializeField] private GameObject fullBodyMesh;

    [Header("카메라 & 레이어")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private string localLayerName = "PlayerSelf";
    [SerializeField] private string remoteLayerName = "RemotePlayer";

    public void SetupVisual(bool isLocal)
    {
        if (fullBodyMesh == null) return;

        Renderer[] allRenderers = fullBodyMesh.GetComponentsInChildren<Renderer>(true);
        AudioListener listener = playerCamera != null ? playerCamera.GetComponent<AudioListener>() : null;

        if (isLocal)
        {
            // --- [로컬 플레이어 설정] ---
            int localLayer = LayerMask.NameToLayer(localLayerName);
            if (localLayer == -1) return;

            SetLayerRecursively(fullBodyMesh, localLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                if (listener != null) listener.enabled = true;

                // 내 카메라에서 내 몸뚱아리만 스캔 제외 (안 보이게)
                playerCamera.cullingMask &= ~(1 << localLayer);

                int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
                if (remoteLayer != -1) playerCamera.cullingMask |= (1 << remoteLayer);
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                // 🛠️ [핵심 부품 교체] Off가 아니라 ShadowsOnly를 쓴다!
                // 이렇게 하면 내 몸은 안 보이지만, 바닥에 그림자는 투사됨.
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
        }
        else
        {
            // --- [리모트 플레이어 설정] ---
            int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
            if (remoteLayer == -1) remoteLayer = 0;

            SetLayerRecursively(fullBodyMesh, remoteLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                if (listener != null) listener.enabled = false;
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                // 🛠️ 타인이 보는 내 모습은 몸과 그림자 모두 활성화!
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
}