using UnityEngine;

/// <summary>
/// 相机跟随：挂在 Main Camera 上。target 留空时自动查找场景中的 PlayerMovement。
/// 只平滑跟随目标的 X/Y，相机自身的 Z（拍摄距离）保持不变。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("要跟随的目标；留空则自动查找场景中的 PlayerMovement")]
    [SerializeField] private Transform target;
    [Tooltip("平滑时间：越小跟得越紧，越大越柔和")]
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 velocity;

    void LateUpdate()
    {
        if (target == null)
        {
            PlayerMovement p = FindObjectOfType<PlayerMovement>();
            if (p != null)
                target = p.transform;
        }
        if (target == null)
            return;

        Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}
