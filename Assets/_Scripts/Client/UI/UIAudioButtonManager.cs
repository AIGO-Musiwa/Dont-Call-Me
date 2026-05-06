using UnityEngine;
using UnityEngine.EventSystems; // UI 이벤트를 감지하기 위한 필수 코어
using UnityEngine.UI;

/// <summary>
/// 어떤 UI 요소든 이 컴포넌트만 붙이면 Hover / Click 사운드가 작동하는 범용 센서.
/// </summary>
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("사운드 카트리지 (동일 클립, 다른 피치의 SO 권장)")]
    [SerializeField] private AudioEventSO hoverSoundSO;
    [SerializeField] private AudioEventSO clickSoundSO;

    private Button _btn;

    private void Awake()
    {
        // 버튼 컴포넌트가 있다면 가져와서, 비활성화(Interactable = false) 상태인지 체크할 때 씀
        _btn = GetComponent<Button>();
    }

    /// <summary>
    /// 마우스가 UI 위에 올라갔을 때 (Hover) 찰칵!
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 버튼이 비활성화 상태면 소리를 내지 않음
        if (_btn != null && !_btn.interactable) return;

        if (hoverSoundSO != null && UIAudioManager.Instance != null)
        {
            UIAudioManager.Instance.PlayUISound(hoverSoundSO);
        }
    }

    /// <summary>
    /// UI를 클릭했을 때 (Click) 찰칵!
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 버튼이 비활성화 상태면 소리를 내지 않음
        if (_btn != null && !_btn.interactable) return;

        if (clickSoundSO != null && UIAudioManager.Instance != null)
        {
            UIAudioManager.Instance.PlayUISound(clickSoundSO);
        }
    }
}