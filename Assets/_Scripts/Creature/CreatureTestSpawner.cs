using Fusion;
using UnityEngine;

public class CreatureTestSpawner : MonoBehaviour
{
    [Header("1동 환경 설정")]
    public NetworkObject creaturePrefab_Bldg1;      //1동 크리처 프리팹
    public Transform spawnPointBldg1;               //1동 크리처 소환 위치
    public Transform bldg1_Waypoint1F;              //1동 1층 웨이포인트
    public Transform bldg1_Waypoint2F;              //1동 2층 웨이포인트
    public Transform bldg1_Waypoint3F;              //1동 3층 웨이포인트
    public Transform bldg1_CreatureRespawn1F;       //1동 포획 후 1층 리스폰 위치
    public Transform bldg1_CreatureRespawn3F;       //1동 포획 후 3층 리스폰 위치
    public Transform playerRespawn_Bldg1;           //1동 플레이어 포획 리스폰 위치

    [Header("2동 환경 설정")]
    public NetworkObject creaturePrefab_Bldg2;      //2동 크리처 프리팹
    public Transform spawnPointBldg2;               //2동 크리처 소환 위치
    public Transform bldg2_Waypoint1F;              //2동 1층 웨이포인트
    public Transform bldg2_Waypoint2F;              //2동 2층 웨이포인트
    public Transform bldg2_Waypoint3F;              //2동 3층 웨이포인트
    public Transform bldg2_CreatureRespawn1F;       //2동 포획 후 1층 리스폰 위치
    public Transform bldg2_CreatureRespawn3F;       //2동 포획 후 3층 리스폰 위치
    public Transform playerRespawn_Bldg2;           //2동 플레이어 포획 리스폰 위치

    private NetworkRunner runner;
    private bool isSpawning = false;

    void Update()
    {
        //중복 소환 방지
        if (isSpawning) return;

        //네트워크 러너 찾기
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();

        //서버에서만 크리처 소환 진행
        if (runner != null && runner.IsRunning && runner.IsServer)
        {
            //1동 크리처 소환 및 환경 변수 주입
            SpawnCreature(
                creaturePrefab_Bldg1, 
                spawnPointBldg1, 
                bldg1_Waypoint1F, 
                bldg1_Waypoint2F, 
                bldg1_Waypoint3F, 
                bldg1_CreatureRespawn1F, 
                bldg1_CreatureRespawn3F, 
                playerRespawn_Bldg1
                );

            //2동 크리처 소환 및 환경 변수 주입
            SpawnCreature(
                creaturePrefab_Bldg2, 
                spawnPointBldg2, 
                bldg2_Waypoint1F, 
                bldg2_Waypoint2F, 
                bldg2_Waypoint3F, 
                bldg2_CreatureRespawn1F, 
                bldg2_CreatureRespawn3F, 
                playerRespawn_Bldg2
                );

            //소환 완료 상태 저장 및 스크립트 비활성화
            isSpawning = true;
            this.enabled = false;
            Debug.Log("크리처 전용 스포너: 웨이포인트 주입 완료");
        }
    }

    private void SpawnCreature(NetworkObject prefabToSpawn, Transform spawnPos, Transform wp1, Transform wp2, Transform wp3, Transform respawn1, Transform respawn3, Transform playerRespawn)
    {
        //프리팹 및 소환 위치 예외 처리
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

            //리스폰 위치 주입
            ai.creatureRespawnPoint1F = respawn1;
            ai.creatureRespawnPoint3F = respawn3;
            ai.playerRespawnPoint = playerRespawn;

            //순찰 초기화
            ai.InitializeAllWaypoints();
        }
    }

    private Transform[] ExtractWaypoints(Transform parent)
    {
        //부모 오브젝트 예외 처리
        if (parent == null) return new Transform[0];

        //자식 오브젝트를 웨이포인트 배열로 변환
        Transform[] waypoints = new Transform[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            waypoints[i] = parent.GetChild(i);
        }
        return waypoints;
    }
}