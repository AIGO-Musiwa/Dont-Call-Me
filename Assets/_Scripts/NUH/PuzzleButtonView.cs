using UnityEngine;

/// <summary>
/// 버튼 눌림 표현 전용
/// [Networked] 상태를 직접 바꾸지 않고, 읽어서 메쉬만 움직인다.
/// </summary>
public class PuzzleButtonView : MonoBehaviour
{
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, -0.02f, 0f);

    private Vector3 _defaultLocalPosition;

    private void Awake()
    {
        if (buttonVisual == null)
            buttonVisual = transform;

        _defaultLocalPosition = buttonVisual.localPosition;
    }

    public void SetPressed(bool pressed)
    {
        if (buttonVisual == null)
            return;

        buttonVisual.localPosition = pressed
            ? _defaultLocalPosition + pressedLocalOffset 
            : _defaultLocalPosition;
    }
}
