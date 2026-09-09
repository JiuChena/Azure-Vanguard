using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// Hitbox 作者轨道导出的运行时命中判定集合。
    /// </summary>
    [Serializable]
    public sealed class HitboxTrackData : BehaviorTrackData
    {
        // 当前轨道导出的命中判定定义集合。
        [Tooltip("行为播放期间参与命中检测的区域定义。")]
        public HitboxDef[] hitboxes = Array.Empty<HitboxDef>();

        // 当前轨道物理查询使用的目标层过滤。
        [Header("物理查询")]
        [Tooltip("命中检测会查询的目标碰撞体层。")]
        public LayerMask targetLayerMask = ~0;

        // 单次物理查询写入的最大碰撞体数量。
        [Tooltip("单次命中查询最多写入多少个碰撞体结果。")]
        [Min(1)]
        public int maxOverlapResults = 16;

        // 是否绘制当前轨道在运行时的 Scene Gizmo。
        [Header("调试")]
        [Tooltip("开启后在 Scene 视图中绘制当前轨道的命中区域。")]
        public bool drawGizmos = true;

        // 是否输出当前轨道的命中执行诊断日志。
        [Tooltip("开启后输出 HitExecute 调用日志，包括命中区域名称和对象数量。")]
        public bool logHitResults;

        /// <summary>
        /// 创建包含 Hitbox 轨道默认调度顺序的数据。
        /// </summary>
        public HitboxTrackData()
        {
            executionOrder = 20;
        }

        /// <summary>
        /// 获取 Hitbox 轨道的显示名称。
        /// </summary>
        /// <returns>固定的 Hitbox 轨道名称。</returns>
        public override string DisplayName => "Hitbox";

        /// <summary>
        /// 创建命中判定执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>用于更新 Hitbox 的执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new HitboxTrackExecutor(this, context);
        }
    }
}
