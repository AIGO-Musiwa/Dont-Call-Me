using UnityEngine;


/// <summary>
/// 본인의 화면에서는 몸을 가리고 그림자만 남기며, 남들에겐 전체를 보여주는 역할을 하는 컴포넌트
/// </summary>
public class PlayerVisualSetter : MonoBehaviour
{
    [Header("메쉬")]
    [SerializeField] private GameObject fullBodyMesh; //실제 캐릭터 외형
    [SerializeField] private GameObject shadowMesh; //그림자 메쉬

    [Header("카메라")]
    [SerializeField] private Camera playerCamera; //플레이어의 카메라
    [SerializeField] private string localLayerName = "PlayerSelf"; //자신만 보이는 레이어 이름

    public void SetupVisual(bool isLocal)
    {
        if (isLocal)
        {
            // --- [로컬 플레이어 전용 세팅] ---
            int localLayer = LayerMask.NameToLayer(localLayerName);

            // 1. 내 몸의 레이어를 변경하여 내 카메라가 못 보게 함
            SetLayerRecursively(fullBodyMesh, localLayer);

            // 2. 내 카메라의 Culling Mask에서 해당 레이어 제외
            if (playerCamera != null)
                playerCamera.cullingMask &= ~(1 << localLayer);

            // 3. 그림자 전용 메쉬를 활성화하여 내 발밑에 그림자 생성
            if (shadowMesh != null) shadowMesh.SetActive(true);
        }
        else
        {
            // --- [리모트 플레이어 세팅] ---
            // 남이 보는 내 모습에선 그림자 전용 메쉬가 필요 없으므로 제거
            if (shadowMesh != null) shadowMesh.SetActive(false);
        }
    }
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
}
