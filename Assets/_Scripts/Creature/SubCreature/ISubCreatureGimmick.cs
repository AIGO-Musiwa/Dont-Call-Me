using UnityEngine;

public interface ISubCreatureGimmick
{
    // Active 상태 진입 시 1회 호출. 초기 효과 적용
    void OnActivate();

    // Active 상태 매 FixedUpdateNetwork 호출. 지속 효과 갱신
    void OnTick(float deltaTime);

    // Active 상태 종료 시 1회 호출. 모든 효과 해제
    void OnDeactivate();
}
