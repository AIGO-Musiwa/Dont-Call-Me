using Fusion;
using System;
using UnityEngine;
using System.Collections;

public class CreatureTestSpawner : MonoBehaviour
{
    [Header("1동 환경 설정")]
    public NetworkObject creaturePrefab_Bldg1;
    public Transform spawnPointBldg1;
    public Transform bldg1_Waypoint1F;
    public Transform bldg1_Waypoint2F;
    public Transform bldg1_Waypoint3F;
    public Transform bldg1_CreatureRespawn1F;
    public Transform bldg1_CreatureRespawn3F;
    public Transform playerRespawn_Bldg1; //1동 감옥 리스폰 위치
    public Light[] bldg1_Lights; //1동 조명 배열

    [Header("2동 환경 설정")]
    public NetworkObject creaturePrefab_Bldg2;
    public Transform spawnPointBldg2;
    public Transform bldg2_Waypoint1F;
    public Transform bldg2_Waypoint2F;
    public Transform bldg2_Waypoint3F;
    public Transform bldg2_CreatureRespawn1F;
    public Transform bldg2_CreatureRespawn3F;
    public Transform playerRespawn_Bldg2; //2동 감옥 리스폰 위치
    public Light[] bldg2_Lights; //2동 조명 배열

    private NetworkRunner runner;
    private bool isSpawning = false;

    void Update()
    {
        if (isSpawning) return;
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning && runner.IsServer)
        {
            //1동 크리쳐 소환 및 웨이포인트, 환경 변수 주입
            SpawnCreature(creaturePrefab_Bldg1, spawnPointBldg1, bldg1_Waypoint1F, bldg1_Waypoint2F, bldg1_Waypoint3F, bldg1_CreatureRespawn1F, bldg1_CreatureRespawn3F, playerRespawn_Bldg1, bldg1_Lights);

            //2동 크리쳐 소환 및 웨이포인트, 환경 변수 주입
            SpawnCreature(creaturePrefab_Bldg2, spawnPointBldg2, bldg2_Waypoint1F, bldg2_Waypoint2F, bldg2_Waypoint3F, bldg2_CreatureRespawn1F, bldg2_CreatureRespawn3F, playerRespawn_Bldg2, bldg2_Lights);

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
            ai.creatureRespawnPoint1F = respawn1;
            ai.creatureRespawnPoint3F = respawn3;

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