using System;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译自定义 Hitbox 轨道为运行时命中判定数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineHitboxTrack))]
    internal sealed class HitboxTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取自定义 Hitbox 轨道类型。
        /// </summary>
        /// <returns>BehaviorTimelineHitboxTrack 类型。</returns>
        public System.Type TrackType => typeof(BehaviorTimelineHitboxTrack);

        /// <summary>
        /// 导出全部 Hitbox 片段为带有时间窗的独立数据。
        /// </summary>
        /// <param name="track">待导出的 Hitbox 轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineHitboxTrack hitboxTrack || context == null)
                return;

            // 以 Timeline 的起止时间覆盖作者期定义，形成独立运行时命中窗口。
            foreach (TimelineClip clip in hitboxTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineHitboxClipAsset asset)
                    continue;

                context.ConsiderEndTime(clip.end);
                HitboxTrackExportState exportState = context.GetOrCreateExportState<HitboxTrackExportState>();
                exportState.Hitboxes.Add(HitboxTrackExportUtility.CloneDefinition(asset.hitboxData, (float)clip.start,
                    (float)clip.duration, hitboxTrack.name));
            }
        }
    }

    /// <summary>
    /// 收集并提交单次导出中的 Hitbox 定义。
    /// </summary>
    internal sealed class HitboxTrackExportState : IBehaviorTrackExportState
    {
        // 本次 Timeline 导出收集到的命中区域定义。
        public readonly System.Collections.Generic.List<HitboxDef> Hitboxes =
            new System.Collections.Generic.List<HitboxDef>();

        /// <summary>
        /// 稳定排序 Hitbox 定义并写入命中轨道数据。
        /// </summary>
        /// <param name="context">当前导出上下文；不得为 null。</param>
        public void Commit(BehaviorExportContext context)
        {
            // 按生效时间、作者轨道与定义名建立稳定物理查询顺序。
            Hitboxes.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;

                int result = left.startTime.CompareTo(right.startTime);
                if (result != 0) return result;
                result = string.Compare(left.authoringTrackName, right.authoringTrackName, StringComparison.Ordinal);
                return result != 0 ? result : string.Compare(left.name, right.name, StringComparison.Ordinal);
            });

            context.GetOrCreateTrackData<HitboxTrackData>().hitboxes = Hitboxes.ToArray();
        }
    }
}
