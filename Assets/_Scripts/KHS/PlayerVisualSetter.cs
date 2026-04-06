using UnityEngine;
using UnityEngine.Rendering;

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

        if (isLocal)
        {
            // --- [로컬 플레이어 설정] ---
            int localLayer = LayerMask.NameToLayer(localLayerName);
            if (localLayer == -1) { Debug.LogError($"{localLayerName} 레이어가 없습니다!"); return; }

            SetLayerRecursively(fullBodyMesh, localLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = true; // 내 카메라는 켠다
                playerCamera.cullingMask &= ~(1 << localLayer);

                // [보강] 내 카메라가 상대방 레이어는 확실히 보도록 추가
                int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
                if (remoteLayer != -1) playerCamera.cullingMask |= (1 << remoteLayer);
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

                // [추가] 내 몸이 내 손전등 빛을 받아서 밝아지거나 시야를 방해하지 않게 함
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

            // [수정] 상대방의 프리팹에 붙어있는 카메라는 무조건 끈다
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }

            // 상대방 몸체 렌더러 설정
            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.On; // 그림자 던지기 ON
                renderer.receiveShadows = true; // 그림자 받기 ON
            }

            // 리모트 플레이어는 별도 그림자 메쉬가 필요 없음
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