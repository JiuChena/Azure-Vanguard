using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译自定义事件轨道为运行时行为事件。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineEventTrack))]
    internal sealed class EventTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取自定义事件轨道类型。
        /// </summary>
        /// <returns>BehaviorTimelineEventTrack 类型。</returns>
        public System.Type TrackType => typeof(BehaviorTimelineEventTrack);

        /// <summary>
        /// 导出全部有效的自定义行为事件。
        /// </summary>
        /// <param name="track">待导出的事件轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineEventTrack eventTrack || context == null)
                return;

            // 每个 Timeline 片段独立构建运行时事件，作者资产不参与运行时修改。
            foreach (TimelineClip clip in eventTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineEventClipAsset asset)
                    continue;

                context.ConsiderEndTime(clip.end);
                if (asset.eventData?.execute == null)
                {
                    context.AddWarning($"Behavior Events 轨道中的片段 '{clip.displayName}' 未配置执行资产，已跳过。");
                    continue;
                }

                EventTrackExportState exportState = context.GetOrCreateExportState<EventTrackExportState>();
                exportState.Events.Add(new BehaviorEvent
                {
                    authoringTrackName = eventTrack.name,
                    time = Mathf.Max(0f, (float)clip.start),
                    referenceBone = asset.eventData.referenceBone,
                    positionOffset = asset.eventData.positionOffset,
                    rotationOffset = asset.eventData.rotationOffset,
                    scaleOffset = asset.eventData.scaleOffset,
                    execute = asset.eventData.execute
                });
            }
        }
    }

    /// <summary>
    /// 收集并提交单次导出中的定时行为事件。
    /// </summary>
    internal sealed class EventTrackExportState : IBehaviorTrackExportState
    {
        // 本次 Timeline 导出收集到的行为事件。
        public readonly System.Collections.Generic.List<BehaviorEvent> Events =
            new System.Collections.Generic.List<BehaviorEvent>();

        /// <summary>
        /// 稳定排序行为事件并写入事件轨道数据。
        /// </summary>
        /// <param name="context">当前导出上下文；不得为 null。</param>
        public void Commit(BehaviorExportContext context)
        {
            // 保持同一触发时刻的作者顺序与命名诊断稳定。
            Events.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;

                int result = left.time.CompareTo(right.time);
                if (result != 0) return result;
                result = string.Compare(left.authoringTrackName, right.authoringTrackName, StringComparison.Ordinal);
                if (result != 0) return result;
                result = string.Compare(left.referenceBone, right.referenceBone, StringComparison.Ordinal);
                return result != 0 ? result : string.Compare(
                    left.execute != null ? left.execute.name : string.Empty,
                    right.execute != null ? right.execute.name : string.Empty,
                    StringComparison.Ordinal);
            });

            context.GetOrCreateTrackData<EventTrackData>().events = Events.ToArray();
        }
    }
}
