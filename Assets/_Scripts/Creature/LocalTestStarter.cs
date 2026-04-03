using UnityEngine;
using Fusion;

public class LocalTestStarter : MonoBehaviour
{
    public NetworkRunner runner;
    public NetworkObject creaturePrefab;
    public Transform spawnPoint;
    
    public Transform waypointParent1F;
    public Transform waypointParent2F;
    public Transform waypointParent3F;

    async void Start()
    {
        if (runner == null) runner = gameObject.AddComponent<NetworkRunner>();
        var sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null) sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Single,
            SessionName = "TestRoom",
            SceneManager = sceneManager
        });

        //크리처 스폰
        NetworkObject spawnedCreature = runner.Spawn(creaturePrefab, spawnPoint.position, spawnPoint.rotation);

        //크리처의 두뇌에 접근
        CreatureAI ai = spawnedCreature.GetComponent<CreatureAI>();
        if (ai != null)
        {            
            ai.waypoints1F = ExtractWaypoints(waypointParent1F);
            ai.waypoints2F = ExtractWaypoints(waypointParent2F);
            ai.waypoints3F = ExtractWaypoints(waypointParent3F);

            //1층 순찰 시작!
            ai.SetCurrentFloor(1);
        }
    }
    
    private Transform[] ExtractWaypoints(Transform parent)
    {
        if (parent == null) return new Transform[0]; //비어있으면 빈 배열 반환

        Transform[] waypoints = new Transform[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            waypoints[i] = parent.GetChild(i);
        }
        return waypoints;
    }
}