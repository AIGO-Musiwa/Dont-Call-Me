using UnityEngine;

public class PlayerLookView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraLightRoot;
    [SerializeField] private float eyeOffset = 0.1f;

    private PlayerController _controller;
    private PlayerKCCMotor _motor;

    public Camera ViewCamera => playerCamera;
    public Transform ViewOrigin => playerCamera != null ? playerCamera.transform : cameraHolder;

    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        _motor = controller != null ? controller.KCCMotor : null;

        if (playerCamera == null && cameraHolder != null)
            playerCamera = cameraHolder.GetComponentInChildren<Camera>(true);

        if (cameraHolder == null)
            Debug.LogError("[PlayerLookView] cameraHolder가 비어 있습니다.", this);

        if (playerCamera != null && _controller != null)
            playerCamera.enabled = _controller.HasInputAuthority;

        if (_controller != null && _controller.HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (_controller == null || _motor == null || _motor.KCC == null)
            return;

        if (!_controller.HasInputAuthority)
            return;

        Vector2 pitchRotation = _motor.KCC.GetLookRotation(true, false);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(pitchRotation.x, 0f, 0f);

            Vector3 localPos = cameraHolder.localPosition;
            localPos.y = (_motor.IsCrouching ? _motor.CrouchHeight : _motor.StandHeight) - eyeOffset;
            cameraHolder.localPosition = localPos;
        }
    }

    public Transform GetCameraLightRoot()
    {
        return cameraLightRoot;
    }
}