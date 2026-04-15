using Fusion;
using UnityEngine;

public class PlayerHandView : MonoBehaviour
{
    [Header("손 소켓")]
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private Transform leftHandSocket;

    private PlayerController _controller;

    // 비주얼 세터와 규격을 맞춘 레이어 이름
    private const string LocalHiddenLayer = "PlayerSelf";

    public void Initialize(PlayerController controller)
    {
        _controller = controller;
    }

    private void LateUpdate()
    {
        if (_controller == null || _controller.Runner == null)
            return;

        // 1. 오른손 아이템 업데이트
        UpdateHandVisual(_controller.NetRightHandItem, rightHandSocket);

        // 2. 왼손 아이템 업데이트
        UpdateHandVisual(_controller.NetLeftHandItem, leftHandSocket);
    }

    private void UpdateHandVisual(NetworkObject itemObject, Transform socket)
    {
        if (itemObject == null || socket == null)
            return;

        // [기존 로직] 위치 및 회전 동기화
        Transform itemTransform = itemObject.transform;
        itemTransform.position = socket.position;
        itemTransform.rotation = socket.rotation;

        // [추가 로직] 시각적 격리 (내 눈에만 숨기기)
        // 내 캐릭터(HasInputAuthority)라면 PlayerSelf 레이어로, 아니면 Default로 설정
        bool isLocal = _controller.Object.HasInputAuthority;
        int targetLayer = isLocal
            ? LayerMask.NameToLayer(LocalHiddenLayer)
            : LayerMask.NameToLayer("Default");

        // 아이템의 모든 자식 메쉬까지 레이어를 일괄 변경
        SetLayerRecursively(itemObject.gameObject, targetLayer);
    }

    /// <summary>
    /// 아이템과 그 하위의 모든 부품들까지 레이어를 변경하는 함수
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        // 이미 레이어가 맞다면 연산 생략 (최적화)
        if (obj.layer != newLayer)
        {
            obj.layer = newLayer;
        }

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}