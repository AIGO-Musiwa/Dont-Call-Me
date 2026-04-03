using Fusion;
using System;
using UnityEngine;
using System.Collections;

public class CreatureTestSpawner : MonoBehaviour
{
    public NetworkObject creaturePrefab; //크리처 프리팹을 할당할 변수
    public Transform spawnPoint;         //크리처가 생성될 위치를 지정할 변수

    [Header("웨이포인트 묶음")]
    public Transform waypointParent1F;
    public Transform waypointParent2F;
    public Transform waypointParent3F;

    [Header("맵 환경 설정")]
    public Light sceneDirectionalLight;
    public Transform playerRespawnPoint;
    public Transform creatureRespawnPoint1F;
    public Transform creatureRespawnPoint3F;

    private NetworkRunner runner;
    private bool isSpawning = false;

    // Update is called once per frame
    void Update()
    {
        if (isSpawning) return;
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning && runner.IsServer)
        {
            //크리처 소환
            NetworkObject spawnedObj = runner.Spawn(creaturePrefab, spawnPoint.position, spawnPoint.rotation);

            //소환된 크리처의 ai 컴포넌트 가져오기
            CreatureAI ai = spawnedObj.GetComponent<CreatureAI>();
            if (ai != null)
            {
                //웨이포인트 주입
                ai.waypoints1F = ExtractWaypoints(waypointParent1F);
                ai.waypoints2F = ExtractWaypoints(waypointParent2F);
                ai.waypoints3F = ExtractWaypoints(waypointParent3F);

                //맵 환경(조명, 리스폰) 주입
                ai.directionalLight = this.sceneDirectionalLight;
                ai.playerRespawnPoint = this.playerRespawnPoint;
                ai.creatureRespawnPoint1F = this.creatureRespawnPoint1F;
                ai.creatureRespawnPoint3F = this.creatureRespawnPoint3F;

                //1층부터 순찰 시작
                ai.SetCurrentFloor(1);
            }

            isSpawning = true;
            Debug.Log("크리처 전용 스포너: 웨이포인트 및 환경 변수(조명, 리스폰) 주입 완료");
        }
    }

    private Transform[] ExtractWaypoints(Transform parent)
    {
        if (parent == null) return new Transform[0];
        Transform[] waypoints = new Transform[parent.childCount];

        for (int i = 0; i < parent.childCount; i++)
        {
            waypoints[i] = parent.GetChild(i);
        }
        return waypoints;
    }    
}
