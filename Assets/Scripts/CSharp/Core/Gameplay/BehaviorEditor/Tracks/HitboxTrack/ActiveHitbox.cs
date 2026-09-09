using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 缓存单个 Hitbox 定义及本次播放解析出的参考骨骼。
    /// </summary>
    internal readonly struct ActiveHitbox
    {
        /// <summary>当前命中区域静态定义。</summary>
        public HitboxDef Definition { get; }
        /// <summary>本次播放解析的参考骨骼。</summary>
        public Transform ReferenceTransform { get; }
        /// <summary>当前区域是否使用世界空间偏移。</summary>
        public bool UseWorldSpace { get; }

        /// <summary>
        /// 创建本次播放的 Hitbox 缓存。
        /// </summary>
        /// <param name="definition">当前命中区域定义；不得为 null。</param>
        /// <param name="referenceTransform">解析出的参考骨骼；世界空间区域时为 null。</param>
        public ActiveHitbox(HitboxDef definition, Transform referenceTransform)
        {
            Definition = definition;
            ReferenceTransform = referenceTransform;
            UseWorldSpace = string.IsNullOrWhiteSpace(definition.referenceBone);
        }

        /// <summary>
        /// 判断当前区域是否处于配置的生效时间窗内。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        /// <returns>位于起始时间（含）和结束时间（不含）之间时返回 true。</returns>
        public bool IsActive(float elapsedTime)
        {
            return elapsedTime >= Definition.startTime && elapsedTime < Definition.startTime + Definition.duration;
        }

        /// <summary>
        /// 计算当前区域的世界空间中心、旋转与尺寸。
        /// </summary>
        /// <param name="fallbackRoot">参考骨骼缺失时使用的宿主根节点。</param>
        /// <param name="center">返回世界空间中心。</param>
        /// <param name="rotation">返回世界空间旋转。</param>
        /// <param name="size">返回叠加缩放后的世界空间尺寸。</param>
        public void GetWorldPose(Transform fallbackRoot, out Vector3 center, out Quaternion rotation, out Vector3 size)
        {
            if (UseWorldSpace)
            {
                center = Definition.positionOffset;
                rotation = Quaternion.Euler(Definition.rotationOffset);
                size = Vector3.Scale(Definition.size, Definition.scaleOffset);
                return;
            }

            // 解析失败时退回宿主根节点，保持命中区域仍可执行。
            Transform reference = ReferenceTransform != null ? ReferenceTransform : fallbackRoot;
            center = reference.TransformPoint(Definition.positionOffset);
            rotation = reference.rotation * Quaternion.Euler(Definition.rotationOffset);
            size = Vector3.Scale(Definition.size, Vector3.Scale(reference.lossyScale, Definition.scaleOffset));
        }
    }
}
