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
    public NetworkObject creaturePrefab_Bldg1;
    public Transform spawnPointBldg1;
    public Transform bldg1_Waypoint1F;
    public Transform bldg1_Waypoint2F;
    public Transform bldg1_Waypoint3F;
    public Transform bldg1_CreatureRespawn1F;
    public Transform bldg1_CreatureRespawn3F;

    [Header("2동 웨이포인트 묶음")]
    public NetworkObject creaturePrefab_Bldg2;
    public Transform spawnPointBldg2;
    public Transform bldg2_Waypoint1F;
    public Transform bldg2_Waypoint2F;
    public Transform bldg2_Waypoint3F;
    public Transform bldg2_CreatureRespawn1F;
    public Transform bldg2_CreatureRespawn3F;

    [Header("맵 환경 설정")]
    public Light sceneDirectionalLight;
    public Transform playerRespawnPoint;

    private NetworkRunner runner;
    private bool isSpawning = false;

    // Update is called once per frame
    void Update()
    {
        if (isSpawning) return;
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning && runner.IsServer)
        {
            //1동 크리쳐 소환 및 웨이포인트, 환경 변수 주입
            SpawnCreature(creaturePrefab_Bldg1, spawnPointBldg1, bldg1_Waypoint1F, bldg1_Waypoint2F, bldg1_Waypoint3F, bldg1_CreatureRespawn1F, bldg1_CreatureRespawn3F);

            //2동 크리쳐 소환 및 웨이포인트, 환경 변수 주입
            SpawnCreature(creaturePrefab_Bldg2, spawnPointBldg2, bldg2_Waypoint1F, bldg2_Waypoint2F, bldg2_Waypoint3F, bldg2_CreatureRespawn1F, bldg2_CreatureRespawn3F);

            isSpawning = true;
            this.enabled = false;
            Debug.Log("크리처 전용 스포너: 웨이포인트 및 환경 변수(조명, 리스폰) 주입 완료");
        }
    }

    private void SpawnCreature(NetworkObject prefabToSpawn, Transform spawnPos, Transform wp1, Transform wp2, Transform wp3, Transform respawn1, Transform respawn3)
    {
        if (prefabToSpawn == null || spawnPos == null) return;

        //크리처 소환
        NetworkObject spawnedObj = runner.Spawn(prefabToSpawn, spawnPos.position, spawnPos.rotation);

        //소환된 크리처의 ai 컴포넌트 가져오기
        CreatureAI ai = spawnedObj.GetComponent<CreatureAI>();
        if (ai != null)
        {
            //웨이포인트 주입
            ai.waypoints1F = ExtractWaypoints(wp1);
            ai.waypoints2F = ExtractWaypoints(wp2);
            ai.waypoints3F = ExtractWaypoints(wp3);

            //맵 환경(조명, 리스폰) 주입
            ai.directionalLight = this.sceneDirectionalLight;
            ai.playerRespawnPoint = this.playerRespawnPoint;
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
