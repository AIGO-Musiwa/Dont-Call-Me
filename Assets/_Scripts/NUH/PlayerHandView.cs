using Fusion;
using UnityEngine;

public class PlayerHandView : MonoBehaviour
{
    [Header("손 소켓")]
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private Transform leftHandSocket;

    private PlayerController _controller;

    public void Initialize(PlayerController controller)
    {
        _controller = controller;
    }

    private void LateUpdate()
    {
        if (_controller == null || _controller.Runner == null)
            return;

        UpdateHandVisual(_controller.NetRightHandItem, rightHandSocket);
        UpdateHandVisual(_controller.NetLeftHandItem, leftHandSocket);
    }

    private void UpdateHandVisual(NetworkObject itemObject, Transform socket)
    {
        if (itemObject == null || socket == null)
            return;

        Transform itemTransform = itemObject.transform;
        itemTransform.position = socket.position;
        itemTransform.rotation = socket.rotation;
    }
}