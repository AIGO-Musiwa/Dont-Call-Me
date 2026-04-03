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

        UpdateRightHandVisual();
    }

    private void UpdateRightHandVisual()
    {
        NetworkObject rightItem = _controller.NetRightHandItem;
        if (rightItem == null || rightHandSocket == null)
            return;

        Transform itemTransform = rightItem.transform;
        itemTransform.position = rightHandSocket.position;
        itemTransform.rotation = rightHandSocket.rotation;
    }
}
