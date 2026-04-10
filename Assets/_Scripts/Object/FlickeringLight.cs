using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    private Light myLight;

    [Header("깜빡임 설정")]
    public float minIntensity = 0f;       //가장 어두울 때의 밝기
    public float maxIntensity = 1.5f;     //가장 밝을 때의 밝기

    [Tooltip("숫자가 클수록 더 미친듯이 치지직거림")]
    public float flickerSpeed = 10f;      //깜빡이는 속도

    private float randomizer = 0f;

    void Start()
    {
        myLight = GetComponent<Light>();
        
        //맵에 깜빡이는 조명이 여러 개일 경우, 서로 다르게 깜빡이도록 랜덤 시작점 부여
        randomizer = Random.Range(0f, 65535f);
    }

    void Update()
    {
        //펄린 노이즈(자연스러운 랜덤 곡선)를 사용해 진짜 전구가 고장난 것 같은 불규칙한 느낌 구현
        float noise = Mathf.PerlinNoise(randomizer, Time.time * flickerSpeed);
        myLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}