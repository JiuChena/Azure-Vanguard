using UnityEngine;

/// <summary>
/// 光环跟随器：LateUpdate 中位置锚定目标 Transform、方向参考偏移基准空间（biped 取 Bip001）做 Lerp 平滑跟随，
/// 跟随后与目标的距离锁定为初始偏移按基准空间缩放后的长度。方向记在基准空间而非目标骨骼本地系，
/// 骨骼动画（如持枪姿势摆动脊柱/头部）不会把偏移方向甩走；偏移经 TransformVector 还原，
/// 缩放层级（如 UI 展示区 600 倍 Pos）下长度随模型等比还原。
/// ExecuteAlways：编辑期绘制偏移 Gizmo（选中时可见），跟随逻辑仅运行期执行。
/// </summary>
[ExecuteAlways]
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

    [Tooltip("偏移基准空间：偏移方向与相对姿态记录在该物体的本地系，应指向模型根（骨骼动画不改它的朝向）。为 null 时退化为跟随目标自身")]
    [SerializeField] private Transform offsetSpace;

    [Header("绑定偏移")]
    //是否已绑定偏移：true 时跟随基准取绑定值，Start 不再实测
    [SerializeField] private bool hasBoundOffset;

    //绑定偏移（偏移基准空间）：「绑定当前偏移」时刻与目标的相对位置
    [SerializeField] private Vector3 boundLocalOffset;

    //绑定相对旋转（偏移基准空间）：「绑定当前偏移」时刻与目标的相对姿态
    [SerializeField] private Quaternion boundLocalRotation;

    // 初始偏移（偏移基准空间的本地单位，未含缩放）：决定跟随的方位与恒定距离，
    // 回放时经基准空间 TransformVector 带上旋转与缩放（UI 展示区 600 倍等缩放层级下随模型等比放大）
    private Vector3 initialLocalOffset;

    // 初始相对旋转（偏移基准空间）：跟随中保持与基准空间的相对姿态
    private Quaternion initialLocalRotation = Quaternion.identity;

    //偏移基准物体：优先 offsetSpace，未指定时退化为跟随目标自身（调用前须已判空 followTarget）
    private Transform OffsetSpace => offsetSpace != null ? offsetSpace : followTarget;

    /// <summary>
    /// 重新捕获初始姿态：以当前与目标的相对位置、相对旋转作为新的偏移与恒定距离。
    /// 目标就位较晚或本物体被传送后调用，可重置跟随基准（仅影响本次运行；绑定偏移后 Start 不再实测）。
    /// </summary>
    public void CaptureInitialPose()
    {
        if (followTarget == null) return;

        // 世界偏移与相对旋转转入偏移基准空间记录（InverseTransformVector 连缩放一起除算，存本地单位），
        // 运行时再经基准空间还原——缩放层级（如 UI 展示区 600 倍）下长度随模型等比还原
        Transform space = OffsetSpace;
        Vector3 worldOffset = transform.position - followTarget.position;
        initialLocalOffset = space.InverseTransformVector(worldOffset);
        initialLocalRotation = Quaternion.Inverse(space.rotation) * transform.rotation;
    }

    /// <summary>
    /// 绑定当前偏移：把当前与目标的相对位置/相对旋转记录为持久基准并立即生效，
    /// 此后 Start 直接采用绑定值（不再依赖首次运行的实测）。编辑器下写盘，
    /// 运行时也可经组件右键菜单或代码调用（配合运行期调参固化）。
    /// </summary>
    [ContextMenu("绑定当前偏移 (Bind Offset)")]
    public void BindOffsetFromCurrentPose()
    {
        if (followTarget == null)
        {
            Debug.LogWarning("[HaloHelper] followTarget 为空，无法绑定偏移", this);
            return;
        }

        //未指定基准空间时提示退化行为：偏移方向会被目标骨骼动画带动
        if (offsetSpace == null)
            Debug.LogWarning("[HaloHelper] 未指定偏移基准空间（应拖模型根），退化为跟随目标本地空间——骨骼动画会带动偏移方向", this);

        //与 CaptureInitialPose 同口径：世界偏移与相对旋转转入偏移基准空间（连缩放一起除算）
        Transform space = OffsetSpace;
        Vector3 worldOffset = transform.position - followTarget.position;
        boundLocalOffset = space.InverseTransformVector(worldOffset);
        boundLocalRotation = Quaternion.Inverse(space.rotation) * transform.rotation;
        hasBoundOffset = true;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        //立即生效，绑定后无需等待下次 Start
        initialLocalOffset = boundLocalOffset;
        initialLocalRotation = boundLocalRotation;
        string spaceName = offsetSpace != null ? offsetSpace.name : followTarget.name;
        Debug.Log($"[HaloHelper] 已绑定偏移 {boundLocalOffset}（{spaceName} 本地空间）", this);
    }

    /// <summary>
    /// 清除绑定偏移：回到 Start 实测当前相对姿态的默认行为。
    /// </summary>
    [ContextMenu("清除绑定偏移 (Unbind Offset)")]
    public void ClearBoundOffset()
    {
        hasBoundOffset = false;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[HaloHelper] 已清除绑定偏移，恢复 Start 实测基准", this);
    }

    /// <summary>
    /// 首次跟随基准：已绑定偏移时直接采用绑定值，否则实测当前相对姿态。
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying) return; //ExecuteAlways 下编辑期也会调 Start，跟随基准只在运行期初始化
        if (hasBoundOffset)
        {
            initialLocalOffset = boundLocalOffset;
            initialLocalRotation = boundLocalRotation;
            return;
        }
        CaptureInitialPose();
    }

    /// <summary>
    /// 平滑跟随目标：位置 Lerp 后重新投影锁定初始距离，旋转 Lerp 保持初始相对姿态。
    /// </summary>
    private void LateUpdate()
    {
        if (!Application.isPlaying) return; //编辑期不动 Transform，避免与手摆位置打架
        if (followTarget == null) return;

        // 帧率无关的 Lerp 系数：1 - e^(-速度×dt)，不同帧率下收敛速度一致
        float positionT = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
        float rotationT = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);

        // 期望位：目标当前位置 + 基准空间还原的初始偏移。TransformVector 带上基准空间的旋转与缩放：
        // 方向随身体转向、不随骨骼动画摆动；长度随缩放层级等比还原（如 UI 展示区 600 倍 Pos）
        Transform space = OffsetSpace;
        Vector3 scaledOffset = space.TransformVector(initialLocalOffset);
        Vector3 desiredPosition = followTarget.position + scaledOffset;

        // Lerp 平滑跟随；把结果重新投影回以目标为球心、缩放后偏移长度为半径的球面，锁死距离
        Vector3 lerpedPosition = Vector3.Lerp(transform.position, desiredPosition, positionT);
        float lockDistance = scaledOffset.magnitude;
        if (lockDistance > 0.0001f)
        {
            Vector3 delta = lerpedPosition - followTarget.position;
            float length = delta.magnitude;
            transform.position = length > 0.0001f
                ? followTarget.position + delta * (lockDistance / length)
                : desiredPosition;
        }
        else
        {
            // 初始与目标重合、无方位可保持：退化为普通位置 Lerp 跟随
            transform.position = lerpedPosition;
        }

        // 旋转跟随：期望姿态 = 偏移基准空间旋转 × 初始相对姿态，平滑逼近
        transform.rotation = Quaternion.Lerp(transform.rotation, space.rotation * initialLocalRotation, rotationT);
    }

    /// <summary>
    /// 当前跟随基准偏移（世界向量）：已绑定取绑定值随基准空间还原，未绑定按当前相对姿态即时换算。
    /// 纯读取不改任何状态，编辑期 Gizmo 与运行期共用同一口径。
    /// </summary>
    private Vector3 GetCurrentWorldOffset()
    {
        if (followTarget == null) return Vector3.zero;

        Transform space = OffsetSpace;
        Vector3 localOffset = hasBoundOffset
            ? boundLocalOffset
            : space.InverseTransformVector(transform.position - followTarget.position);
        return space.TransformVector(localOffset);
    }

    /// <summary>
    /// 选中时绘制偏移 Gizmo：从目标锚点沿偏移方向射出线段（长度=二者距离），
    /// 端点为跟随期望位；红线连接 Halo 实际位与期望位，重合即口径一致。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (followTarget == null) return;

        Vector3 desiredPosition = followTarget.position + GetCurrentWorldOffset();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(followTarget.position, 0.03f); //目标锚点
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(followTarget.position, desiredPosition); //偏移线，长度=锁定距离
        Gizmos.DrawWireSphere(desiredPosition, 0.03f); //期望位端点
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, desiredPosition); //实际位与期望位的差
    }
}
