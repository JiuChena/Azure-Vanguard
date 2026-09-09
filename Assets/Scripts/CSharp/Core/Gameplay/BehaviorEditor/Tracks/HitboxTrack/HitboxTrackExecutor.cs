using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 驱动 Hitbox 时间窗、物理查询与调试绘制的轨道执行器。
    /// </summary>
    internal sealed class HitboxTrackExecutor : IBehaviorTrackExecutor, IBehaviorTrackGizmoDrawer
    {
        // 当前轨道导出的静态 Hitbox 数据。
        private readonly HitboxTrackData data;
        // 当前播放的宿主依赖与环境配置。
        private readonly BehaviorExecutionContext context;
        // 本次播放中已解析参考骨骼的 Hitbox 列表。
        private readonly List<ActiveHitbox> activeHitboxes = new List<ActiveHitbox>();
        // HitExecute 调用期间复用的可写命中上下文。
        private readonly HitContext hitContext = new HitContext();
        // PhysX NonAlloc 查询结果缓冲区。
        private Collider[] overlapResults = Array.Empty<Collider>();

        /// <summary>Hitbox 轨道的执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建 Hitbox 轨道执行器。
        /// </summary>
        /// <param name="data">当前轨道导出数据；不得为 null。</param>
        /// <param name="context">当前播放执行上下文；不得为 null。</param>
        public HitboxTrackExecutor(HitboxTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>
        /// 预解析 Hitbox 骨骼引用并创建本次播放的查询缓冲区。
        /// </summary>
        public void Begin()
        {
            // 每次播放重新解析参考骨骼，避免沿用旧宿主或旧骨架引用。
            activeHitboxes.Clear();
            HitboxDef[] hitboxes = data.hitboxes;
            if (hitboxes != null)
            {
                for (int index = 0; index < hitboxes.Length; index++)
                {
                    HitboxDef definition = hitboxes[index];
                    if (definition != null)
                        activeHitboxes.Add(new ActiveHitbox(definition, context.TransformResolver.Resolve(definition.referenceBone)));
                }
            }

            int resultCapacity = Mathf.Max(1, data.maxOverlapResults);
            if (overlapResults.Length != resultCapacity)
                overlapResults = new Collider[resultCapacity];
        }

        /// <summary>
        /// 对当前时间窗内的 Hitbox 执行 NonAlloc 物理命中查询。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        public void Tick(float elapsedTime)
        {
            if (context.OwnerTransform == null || overlapResults.Length == 0) return;

            // 查询所有生效区域，并将命中列表交给其执行配置。
            for (int index = 0; index < activeHitboxes.Count; index++)
            {
                ActiveHitbox activeHitbox = activeHitboxes[index];
                HitExecuteSO execute = activeHitbox.Definition.execute;
                if (!activeHitbox.IsActive(elapsedTime) || execute == null) continue;

                activeHitbox.GetWorldPose(context.OwnerTransform, out Vector3 center, out Quaternion rotation, out Vector3 size);
                int hitCount = QueryOverlap(activeHitbox.Definition, center, rotation, size);
                if (hitCount <= 0) continue;

                // 框架只构建普通对象列表，不做目标过滤、去重或玩法判定。
                hitContext.GameObjects.Clear();
                hitContext.GameObjects.Add(context.OwnerGameObject);
                for (int resultIndex = 0; resultIndex < hitCount; resultIndex++)
                {
                    Collider collider = overlapResults[resultIndex];
                    if (collider != null) hitContext.GameObjects.Add(collider.gameObject);
                }

                execute.Execute(hitContext);
                if (data.logHitResults)
                    Debug.Log($"[{context.OwnerGameObject.name}] 执行 HitExecute：Hitbox={GetDisplayName(activeHitbox.Definition)} | Objects={hitContext.GameObjects.Count - 1}", context.OwnerGameObject);
            }
        }

        /// <summary>
        /// 清理本次播放的 Hitbox 引用和命中上下文。
        /// </summary>
        public void Stop()
        {
            activeHitboxes.Clear();
            hitContext.GameObjects.Clear();
        }

        /// <summary>
        /// 绘制本轨道当前已解析的 Hitbox 线框。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        public void DrawGizmos(float elapsedTime)
        {
            if (!data.drawGizmos || context.OwnerTransform == null) return;
            for (int index = 0; index < activeHitboxes.Count; index++)
            {
                ActiveHitbox hitbox = activeHitboxes[index];
                hitbox.GetWorldPose(context.OwnerTransform, out Vector3 center, out Quaternion rotation, out Vector3 size);
                Gizmos.color = hitbox.IsActive(elapsedTime) ? Color.red : Color.yellow;
                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                if (hitbox.Definition.shape == HitboxShape.Sphere) Gizmos.DrawWireSphere(Vector3.zero, size.x);
                else if (hitbox.Definition.shape == HitboxShape.Capsule) Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x * 2f, size.y, size.x * 2f));
                else Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = previousMatrix;
            }
        }

        /// <summary>
        /// 根据 Hitbox 形状执行对应 PhysX NonAlloc 重叠查询。
        /// </summary>
        /// <param name="definition">当前 Hitbox 几何定义。</param>
        /// <param name="center">世界空间查询中心。</param>
        /// <param name="rotation">世界空间查询旋转。</param>
        /// <param name="size">已叠加缩放后的世界空间尺寸。</param>
        /// <returns>写入结果缓冲区的碰撞体数量。</returns>
        private int QueryOverlap(HitboxDef definition, Vector3 center, Quaternion rotation, Vector3 size)
        {
            if (definition.shape == HitboxShape.Sphere)
                return Physics.OverlapSphereNonAlloc(center, Mathf.Abs(size.x), overlapResults, data.targetLayerMask);
            if (definition.shape == HitboxShape.Capsule)
            {
                float radius = Mathf.Abs(size.x);
                float cylinderHeight = Mathf.Max(0f, Mathf.Abs(size.y) - radius * 2f);
                Vector3 halfOffset = rotation * Vector3.up * (cylinderHeight * 0.5f);
                return Physics.OverlapCapsuleNonAlloc(center + halfOffset, center - halfOffset, radius, overlapResults, data.targetLayerMask);
            }

            return Physics.OverlapBoxNonAlloc(center, size * 0.5f, overlapResults, rotation, data.targetLayerMask);
        }

        /// <summary>
        /// 获取日志使用的 Hitbox 显示名称。
        /// </summary>
        /// <param name="definition">待显示名称的定义；允许为 null。</param>
        /// <returns>定义名称或诊断占位符。</returns>
        private static string GetDisplayName(HitboxDef definition)
        {
            if (definition == null) return "<Null>";
            return string.IsNullOrWhiteSpace(definition.name) ? "<UnnamedHitbox>" : definition.name;
        }
    }
}
