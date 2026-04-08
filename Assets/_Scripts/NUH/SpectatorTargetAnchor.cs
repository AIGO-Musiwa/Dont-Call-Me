using UnityEngine;

/// <summary>
/// 관전 카메라가 따라갈 기준점을 제공하는 스크립트.
/// 플레이어 회전과 카메라 회전을 분리하기 위해 위치 anchor만 제공한다.
/// </summary>
public class SpectatorTargetAnchor : MonoBehaviour
{
    [SerializeField] private Transform anchor;

    /// <summary>
    /// 관전 카메라가 사용할 기준점을 반환한다.
    /// anchor가 비어 있으면 자기 자신의 Transform을 반환한다.
    /// </summary>
    public Transform GetAnchor()
    {
        return anchor != null ? anchor : transform;
    }
}
