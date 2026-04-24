using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{

    // 감도 조절 슬라이더랑 숫자
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityText;

    // PlayerPrefs에 저장할 때 사용할 키워드
    private const string SENSITIVITY_KEY = "MouseSensitivity";

    private void Start()
    {
        // 1. 게임을 켤 때 저장된 감도 값이 있는지 확인, 없으면 기본값 1.0f
        float savedSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, 1.0f);

        // 2. 불러온 값을 슬라이더의 현재 값에 반영
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = savedSensitivity;

            // 3. 슬라이더의 값이 변경될 때마다 OnSensitivityChanged 함수가 자동으로 실행되게 연결
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        // 4. 초기화된 값을 즉시 적용
        ApplySensitivity(savedSensitivity);
    }

    // 슬라이더를 움직일 때마다 호출되는 함수
    private void OnSensitivityChanged(float newValue)
    {
        // 바뀐 값 PlayerPrefs에 저장
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, newValue);
        PlayerPrefs.Save(); // 저장 완료!

        // 실제 게임 내 감도 적용
        ApplySensitivity(newValue);
    }

    private void ApplySensitivity(float value)
    {
        if (sensitivityText != null)
        {
            sensitivityText.text = value.ToString("F1");
        }

        // 다음 단계에서 만들 InputHandler 내부의 함수를 호출할 거예요.
        InputHandler inputHandler = FindAnyObjectByType<InputHandler>();
        if (inputHandler != null)
        {
            inputHandler.SetMouseSensitivity(value);
        }
    }
}