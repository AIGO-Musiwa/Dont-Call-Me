using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

/// <summary>
/// 로컬/원격 플레이어의 시각 레이어, 그림자, ViewModelCamera 활성 상태를 설정한다.
/// 실제 게임 화면을 출력하는 LocalMainCamera와 AudioListener는 씬의 카메라 리그가 관리한다.
/// </summary>
public class PlayerVisualSetter : MonoBehaviour
{
    [Header("메쉬")]
    [SerializeField] private GameObject fullBodyMesh;              // 플레이어 전신 모델 루트

    [Header("ViewModel Camera")]
    [FormerlySerializedAs("playerCamera")]
    [SerializeField] private Camera viewModelCamera;               // 손/아이템 전용 ViewModelCamera

    [Header("레이어")]
    [SerializeField] private string localLayerName = "PlayerSelf"; // 로컬 플레이어 전신 모델 레이어
    [SerializeField] private string remoteLayerName = "RemotePlayer"; // 원격 플레이어 전신 모델 레이어

    public void SetupVisual(bool isLocal)
    {
        if (fullBodyMesh == null)
            return;

        Renderer[] allRenderers = fullBodyMesh.GetComponentsInChildren<Renderer>(true);

        if (isLocal)
        {
            int localLayer = LayerMask.NameToLayer(localLayerName);
            if (localLayer == -1)
                return;

            SetLayerRecursively(fullBodyMesh, localLayer);
            SetViewModelCameraEnabled(true);

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
        }
        else
        {
            int remoteLayer = LayerMask.NameToLayer(remoteLayerName);
            if (remoteLayer == -1)
                remoteLayer = 0;

            SetLayerRecursively(fullBodyMesh, remoteLayer);
            SetViewModelCameraEnabled(false);

            foreach (var renderer in allRenderers)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
        }
    }

    /// <summary>
    /// ViewModelCamera는 로컬 플레이어의 손/아이템 전용 카메라다.
    /// AudioListener는 씬의 LocalMainCamera 하나만 사용해야 하므로 여기서 항상 꺼둔다.
    /// </summary>
    private void SetViewModelCameraEnabled(bool enabled)
    {
        if (viewModelCamera == null)
            return;

        viewModelCamera.enabled = enabled;

        AudioListener listener = viewModelCamera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null)
            return;

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
}
