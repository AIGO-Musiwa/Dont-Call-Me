using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class ZoneLightingManager : NetworkBehaviour
{
    [Header("구역 설정")]
    public Zone myZone;

    [Header("조명 부모 오브젝트")]
    [Tooltip("해당 동의 전체 조명을 포함하는 최상위 부모 객체")]
    public Transform allLightsRoot;

    [Header("3막 이벤트 설정")]
    [Tooltip("3막 진입 시 변경될 조명 색상")]
    public Color act3LightColor = Color.red;

    [Tooltip("3막 진입 시 변경될 목표 밝기")]
    public float act3LightIntensity = 25f;

    [Header("3막 사이렌 연출 설정")]
    [Tooltip("사이렌이 깜빡이는 속도")]
    public float sirenBlinkSpeed = 5.5f;
    [Tooltip("사이렌 깜빡임의 최소 밝기")]
    public float sirenMinIntensity = 0f;

    //수집된 조명들의 원본 데이터를 기억할 구조체
    private class LightData
    {
        public Light light;
        public float originalIntensity;
        public Color originalColor;
    }

    private List<LightData> managedLights = new List<LightData>();

    [Header("네트워크 동기화 상태")]
    [Networked] public NetworkBool IsCaptureDarkout { get; set; }
    [Networked] public NetworkBool IsAct3Active { get; set; }

    //크리처나 게임매니저가 쉽게 찾을 수 있도록 정적 딕셔너리로 관리
    private static Dictionary<Zone, ZoneLightingManager> managers = new Dictionary<Zone, ZoneLightingManager>();

    #region 딕셔너리 등록 및 라이트 수집 로직
    public override void Spawned()
    {
        //딕셔너리에 자신을 등록
        managers[myZone] = this;

        //부모 오브젝트 아래에 있는 모든 Light 컴포넌트 자동 수집
        if (allLightsRoot != null)
        {
            Light[] foundLights = allLightsRoot.GetComponentsInChildren<Light>(true);
            foreach (Light l in foundLights)
            {
                //모든 층이 동일하게 변하므로 3층 판별 로직 삭제
                managedLights.Add(new LightData
                {
                    light = l,
                    originalIntensity = l.intensity,
                    originalColor = l.color
                });
            }
            Debug.Log($"[{myZone}] 조명 관리자: {managedLights.Count}개의 조명을 자동 수집했습니다.");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        //게임 종료 또는 오브젝트 소멸 시 메모리 누수 방지를 위해 명부에서 자신을 제거
        if (managers.ContainsKey(myZone)) managers.Remove(myZone);
    }
    #endregion

    #region 조명 렌더링 및 동기화 로직
    public override void Render()
    {
        //관리할 조명이 없으면 실행하지 않음
        if (managedLights.Count == 0) return;

        //매 프레임 부드럽게 조명 상태를 전환시킴
        foreach (var data in managedLights)
        {
            if (data.light == null) continue;

            //포획 상태일 경우 우선적으로 암전 처리
            if (IsCaptureDarkout)
            {
                data.light.intensity = Mathf.Lerp(data.light.intensity, 0f, Time.deltaTime * 5f);
            }

            //3막 상태일 경우 모든 조명을 설정된 색상과 밝기로 변경
            else if (IsAct3Active)
            {
                //3막 사이렌 연출: 밝기가 시간에 따라 깜박이는 효과 추가
                float pulse = (Mathf.Sin(Time.time * sirenBlinkSpeed) + 1f) / 2f; //0~1 사이의 펄스 값

                //최소 밝기와 최대 밝기 사이를 펄스 값에 따라 변화
                float currentTargetIntensity = Mathf.Lerp(sirenMinIntensity, act3LightIntensity, pulse);

                data.light.color = Color.Lerp(data.light.color, act3LightColor, Time.deltaTime * 5f);
                //data.light.intensity = Mathf.Lerp(data.light.intensity, currentTargetIntensity, Time.deltaTime * 2f);
                data.light.intensity = currentTargetIntensity;
            }

            //평상시 원래 설정된 색상과 밝기로 복구
            else
            {
                data.light.color = Color.Lerp(data.light.color, data.originalColor, Time.deltaTime * 2f);
                data.light.intensity = Mathf.Lerp(data.light.intensity, data.originalIntensity, Time.deltaTime * 2f);
            }
        }
    }
    #endregion

    #region 외부 제어 함수
    //외부에서 호출할 수 있는 제어 함수들 반드시 StateAuthority를 가진 서버가 호출해야 함

    public static ZoneLightingManager GetManager(Zone zone)
    {
        managers.TryGetValue(zone, out ZoneLightingManager manager);
        return manager;
    }

    public void SetCaptureDarkout(bool isDark)
    {
        if (HasStateAuthority) IsCaptureDarkout = isDark;
    }

    public void TriggerAct3Event(bool isActive)
    {
        if (HasStateAuthority) IsAct3Active = isActive;
    }
    #endregion
}