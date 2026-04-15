using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 비주얼 및 오디오 수신 장치 동기화 모듈
/// 본체 메쉬의 그림자 설정을 통해 로컬/리모트 표현을 최적화한다.
/// </summary>
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

        // 오디오 리스너 참조 확인
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

            // 본체 레이어를 내 화면 전용 숨김 레이어로 변경
            SetLayerRecursively(fullBodyMesh, localLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = true; // 내 카메라는 활성화
                if (listener != null) listener.enabled = true; // 내 귀(Listener)는 활성화

                // 내 카메라에서 내 레이어만 제외 (내 몸 안 보이게)
                playerCamera.cullingMask &= ~(1 << localLayer);

                int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
                if (remoteLayer != -1) playerCamera.cullingMask |= (1 << remoteLayer);
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                // [정비] 로컬에서는 본체 그림자를 완전히 끈다
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
        }
        else
        {
            // --- [리모트 플레이어 설정] ---
            int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
            if (remoteLayer == -1) remoteLayer = 0;

            // 본체 레이어를 다른 사람이 볼 수 있는 레이어로 변경
            SetLayerRecursively(fullBodyMesh, remoteLayer);

            if (playerCamera != null)
            {
                playerCamera.enabled = false; // 남의 카메라는 비활성화
                if (listener != null) listener.enabled = false; // 남의 귀는 비활성화
            }

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                // [정비] 리모트 유저(남)의 그림자는 정상적으로 출력한다
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