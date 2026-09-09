using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 提供 Hitbox 轨道导出所需的数据复制工具。
    /// </summary>
    internal static class HitboxTrackExportUtility
    {
        /// <summary>
        /// 克隆 Hitbox 定义并注入 Timeline 片段确定的时间窗与来源轨道名。
        /// </summary>
        /// <param name="source">作者期 Hitbox 定义；为 null 时创建默认定义。</param>
        /// <param name="timelineStartTime">Timeline 片段开始时间，单位为秒。</param>
        /// <param name="timelineDuration">Timeline 片段持续时间，单位为秒。</param>
        /// <param name="trackName">来源 Timeline 轨道名；为空时保留源定义名称。</param>
        /// <returns>独立的运行时 Hitbox 定义。</returns>
        public static HitboxDef CloneDefinition(HitboxDef source, float timelineStartTime, float timelineDuration,
            string trackName = null)
        {
            // 复制作者定义，避免导出过程改写 Timeline 资产。
            var cloned = new HitboxDef();
            if (source != null)
            {
                cloned.authoringTrackName = !string.IsNullOrWhiteSpace(trackName)
                    ? trackName
                    : source.authoringTrackName;
                cloned.name = source.name;
                cloned.shape = source.shape;
                cloned.referenceBone = source.referenceBone;
                cloned.positionOffset = source.positionOffset;
                cloned.rotationOffset = source.rotationOffset;
                cloned.scaleOffset = source.scaleOffset;
                cloned.size = source.size;
                cloned.execute = source.execute;
            }

            cloned.startTime = Mathf.Max(0f, timelineStartTime);
            cloned.duration = Mathf.Max(0f, timelineDuration);
            return cloned;
        }
    }
}
