using Fusion;
using System;
using UnityEngine;
using System.Collections;

public class CreatureTestSpawner : MonoBehaviour
{
    //public NetworkObject creaturePrefab; //크리처 프리팹을 할당할 변수
    //public NetworkObject creaturePrefab2; //크리처 프리팹을 할당할 변수 
    //public Transform spawnPoint;         //크리처가 생성될 위치를 지정할 변수

    [Header("1동 웨이포인트 묶음")]
    public NetworkObject creaturePrefab_Bldg1;      //1동 크리쳐 프리팹
    public Transform spawnPointBldg1;               //1동 크리쳐 소환 장소
    public Transform bldg1_Waypoint1F;              //1동 1층 웨이포인트
    public Transform bldg1_Waypoint2F;              //1동 2층 웨이포인트
    public Transform bldg1_Waypoint3F;              //1동 3층 웨이포인트
    public Transform bldg1_CreatureRespawn1F;       //1동 포획 후 1층 리스폰 포인트
    public Transform bldg1_CreatureRespawn3F;       //1동 포획 후 3층 리스폰 포인트
    public Transform playerRespawn_Bldg1;           //1동 플레이어 포획 리스폰 포인트
    public Light[] bldg1_Lights;                    //1동 조명


    [Header("2동 환경 설정")]
    public NetworkObject creaturePrefab_Bldg2;
    public Transform spawnPointBldg2;
    public Transform bldg2_Waypoint1F;
    public Transform bldg2_Waypoint2F;
    public Transform bldg2_Waypoint3F;
    public Transform bldg2_CreatureRespawn1F;
    public Transform bldg2_CreatureRespawn3F;
    public Transform playerRespawn_Bldg2;
    public Light[] bldg2_Lights;

    private NetworkRunner runner;
    private bool isSpawning = false;

    void Update()
    {
        if (isSpawning) return;
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning && runner.IsServer)
        {
            //1동 크리쳐 소환 및 웨이포인트, 환경 변수 주입       
            SpawnCreature(
                creaturePrefab_Bldg1, 
                spawnPointBldg1, 
                bldg1_Waypoint1F, 
                bldg1_Waypoint2F, 
                bldg1_Waypoint3F, 
                bldg1_CreatureRespawn1F, 
                bldg1_CreatureRespawn3F, 
                playerRespawn_Bldg1,
                bldg1_Lights
                );

            //2동 크리쳐 소환 및 웨이포인트, 환경 변수 주입
            SpawnCreature(
                creaturePrefab_Bldg2, 
                spawnPointBldg2, 
                bldg2_Waypoint1F, 
                bldg2_Waypoint2F, 
                bldg2_Waypoint3F, 
                bldg2_CreatureRespawn1F, 
                bldg2_CreatureRespawn3F, 
                playerRespawn_Bldg2,
                bldg2_Lights
                );

            isSpawning = true;
            this.enabled = false;
            Debug.Log("크리처 전용 스포너: 웨이포인트 및 환경 변수 주입 완료");
        }
    }

    private void SpawnCreature(NetworkObject prefabToSpawn, Transform spawnPos, Transform wp1, Transform wp2, Transform wp3, Transform respawn1, Transform respawn3, Transform playerRespawn, Light[] lightsToManage)
    {
        if (prefabToSpawn == null || spawnPos == null) return;

        //크리처 소환
        NetworkObject spawnedObj = runner.Spawn(prefabToSpawn, spawnPos.position, spawnPos.rotation);
        CreatureAI ai = spawnedObj.GetComponent<CreatureAI>();

        if (ai != null)
        {
            //웨이포인트 주입
            ai.waypoints1F = ExtractWaypoints(wp1);
            ai.waypoints2F = ExtractWaypoints(wp2);
            ai.waypoints3F = ExtractWaypoints(wp3);


            //맵 환경 변수 주입
            ai.managedLights = lightsToManage;
            ai.playerRespawnPoint = playerRespawn;

            //맵 환경(조명, 리스폰) 주입
            ai.managedLights = lightsToManage;         

            ai.creatureRespawnPoint1F = respawn1;
            ai.creatureRespawnPoint3F = respawn3;
            ai.playerRespawnPoint = playerRespawn;

            //1층부터 순찰 시작
            ai.InitializeAllWaypoints();
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