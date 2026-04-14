using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///  비주얼 및 오디오 수신 장치 동기화 모듈
/// </summary>
public class PlayerVisualSetter : MonoBehaviour
{
    [Header("메쉬")]
    [SerializeField] private GameObject fullBodyMesh;
    [SerializeField] private GameObject shadowMesh;

    [Header("카메라 & 레이어")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private string localLayerName = "PlayerSelf";
    [SerializeField] private string remoteLayerName = "RemotePlayer";

    public void SetupVisual(bool isLocal)
    {
        if (fullBodyMesh == null) return;

        Renderer[] allRenderers = fullBodyMesh.GetComponentsInChildren<Renderer>(true);

        // [증설] 오디오 리스너 참조 확인
        AudioListener listener = null;
        if (playerCamera != null)
        {
            listener = playerCamera.GetComponent<AudioListener>();
        }

        if (isLocal)
        {
            // --- [로컬 플레이어 설정] ---
            int localLayer = LayerMask.NameToLayer(localLayerName);
            if (localLayer == -1) { Debug.LogError($"{localLayerName} 레이어가 없습니다!"); return; }

            SetLayerRecursively(fullBodyMesh, localLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = true; // 내 카메라는 켠다

                //  내 귀(Listener)는 연다!
                if (listener != null) listener.enabled = true;

                playerCamera.cullingMask &= ~(1 << localLayer);

                int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
                if (remoteLayer != -1) playerCamera.cullingMask |= (1 << remoteLayer);
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }

            if (shadowMesh != null) shadowMesh.SetActive(true);
        }
        else
        {
            // --- [리모트 플레이어 설정] ---
            int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
            if (remoteLayer == -1) remoteLayer = 0;

            SetLayerRecursively(fullBodyMesh, remoteLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = false; // 남의 카메라는 끈다

                //  남의 귀(Listener)는 무조건 닫는다!
                if (listener != null) listener.enabled = false;
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            if (shadowMesh != null) shadowMesh.SetActive(false);
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