using UnityEngine;

/// <summary>
/// 光环跟随器：LateUpdate 中参考目标 Transform 的位置与旋转做 Lerp 平滑跟随，跟随后与目标的距离恒定锁定为初始距离。
/// </summary>
public class HaloHelper : MonoBehaviour
{
    [Header("跟随")]
    [Tooltip("位置跟随收敛速度，单位：1/秒，越大跟随越快越紧")]
    [Min(0.01f)]
    [SerializeField] private float positionLerpSpeed = 8f;

    [Tooltip("旋转跟随收敛速度，单位：1/秒，越大跟随越快越紧")]
    [Min(0.01f)]
    [SerializeField] private float rotationLerpSpeed = 8f;

    [Header("引用")]
    [Tooltip("参考目标 Transform；为 null 时不跟随")]
    [SerializeField] private Transform followTarget;

    // 初始偏移（目标本地空间）：决定跟随的方位与恒定距离，随目标旋转一起转动
    private Vector3 initialLocalOffset;

    // 初始相对旋转（目标本地空间）：跟随中保持与目标的相对姿态
    private Quaternion initialLocalRotation = Quaternion.identity;

    // 初始距离（initialLocalOffset 的模长）；为 0 时退化为普通位置 Lerp 跟随
    private float initialDistance;

    /// <summary>
    /// 重新捕获初始姿态：以当前与目标的相对位置、相对旋转作为新的偏移与恒定距离。
    /// 目标就位较晚或本物体被传送后调用，可重置跟随基准。
    /// </summary>
    public void CaptureInitialPose()
    {
        if (followTarget == null) return;

        // 世界偏移与相对旋转转入目标本地空间记录，运行时再随目标旋转还原
        Vector3 worldOffset = transform.position - followTarget.position;
        initialLocalOffset = Quaternion.Inverse(followTarget.rotation) * worldOffset;
        initialLocalRotation = Quaternion.Inverse(followTarget.rotation) * transform.rotation;
        initialDistance = initialLocalOffset.magnitude;
    }

    /// <summary>
    /// 捕获首次跟随基准。
    /// </summary>
    private void Start()
    {
        CaptureInitialPose();
    }

    /// <summary>
    /// 平滑跟随目标：位置 Lerp 后重新投影锁定初始距离，旋转 Lerp 保持初始相对姿态。
    /// </summary>
    private void LateUpdate()
    {
        if (followTarget == null) return;

        // 帧率无关的 Lerp 系数：1 - e^(-速度×dt)，不同帧率下收敛速度一致
        float positionT = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
        float rotationT = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);

        // 期望位：目标当前位置 + 目标当前旋转下的初始偏移（随目标旋转一起转，与目标距离天然等于初始距离）
        Vector3 desiredPosition = followTarget.position + followTarget.rotation * initialLocalOffset;

        // Lerp 平滑跟随；初始距离有效时把结果重新投影回以目标为球心、初始距离为半径的球面，锁死距离
        Vector3 lerpedPosition = Vector3.Lerp(transform.position, desiredPosition, positionT);
        if (initialDistance > 0.0001f)
        {
            Vector3 delta = lerpedPosition - followTarget.position;
            float length = delta.magnitude;
            transform.position = length > 0.0001f
                ? followTarget.position + delta * (initialDistance / length)
                : desiredPosition;
        }
        else
        {
            // 初始与目标重合、无方位可保持：退化为普通位置 Lerp 跟随
            transform.position = lerpedPosition;
        }

        // 旋转跟随：期望姿态 = 目标旋转 × 初始相对姿态，平滑逼近
        transform.rotation = Quaternion.Lerp(transform.rotation, followTarget.rotation * initialLocalRotation, rotationT);
    }
}
