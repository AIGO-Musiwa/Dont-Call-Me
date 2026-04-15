using UnityEngine;

public class AnimationEventCatcher : MonoBehaviour
{
    // 그림자 메쉬가 던지는 이벤트를 허공으로 날려버림 (에러 방지용)
    public void PlayFootstepByType(int type) { }
    public void PlayFootstepByType(string type) { }
    // Enum을 쓴다면 매개변수를 거기에 맞춰서 하나 비워둬!)
}