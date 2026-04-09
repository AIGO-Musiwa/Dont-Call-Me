using Fusion;
using Photon.Voice.Unity;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DebugMicController : MonoBehaviour
{
    [Header("UI 연결")]
    public Toggle muteToggle;

    [SerializeField] private Recorder localRecorder;

    private void Start()
    {
        if (muteToggle != null)
        {
            muteToggle.onValueChanged.AddListener(ToggleMute);
        }

        StartCoroutine(FindLocalRecorderRoutine());
    }

    private void Update()
    {
        // M 키를 누를 때마다 음소거 토글
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (muteToggle != null)
            {
                // UI 토글의 상태를 반전시킵니다. 
                // (Toggle의 상태가 변하면 아까 연결해둔 OnValueChanged가 알아서 작동하여 마이크도 꺼짐/켜짐 처리됨)
                muteToggle.isOn = !muteToggle.isOn; 
            }
        }
    }

    private IEnumerator FindLocalRecorderRoutine()
    {
        while (localRecorder == null)
        {
            NetworkObject[] allNetObjs = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

            foreach (var netObj in allNetObjs)
            {
                if (netObj.HasInputAuthority)
                {
                    if (netObj.Runner != null)
                    {
                        localRecorder = netObj.Runner.GetComponent<Recorder>();
                    }

                    if (localRecorder != null)
                    {
                        if (muteToggle != null)
                        {
                            localRecorder.TransmitEnabled = muteToggle.isOn;
                        }
                        break;
                    }
                }
            }
            if(localRecorder == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    public void ToggleMute(bool isMuted)
    {
        if (localRecorder == null) return;

        localRecorder.TransmitEnabled = isMuted;
    }
}
