using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译 Behavior Meta 轨道为行为播放头数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(BehaviorTimelineMetaTrack))]
    internal sealed class MetaTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取 Meta 轨道类型。
        /// </summary>
        /// <returns>BehaviorTimelineMetaTrack 类型。</returns>
        public System.Type TrackType => typeof(BehaviorTimelineMetaTrack);

        /// <summary>
        /// 导出首个 Meta 片段作为行为播放头配置。
        /// </summary>
        /// <param name="track">待导出的 Meta 轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not BehaviorTimelineMetaTrack metaTrack || context == null)
                return;

            BehaviorPlaybackSettings settings = context.PlaybackSettings;
            bool assigned = false;
            foreach (TimelineClip clip in metaTrack.GetClips())
            {
                if (clip?.asset is not BehaviorTimelineMetaClipAsset asset)
                    continue;

                context.ConsiderEndTime(clip.end);
                if (assigned)
                {
                    context.AddWarning($"Meta 轨道 '{metaTrack.name}' 包含多个片段，仅导出第一个片段。");
                    continue;
                }

                settings.wrapMode = asset.wrapMode;
                settings.speedMultiplier = Mathf.Max(0.01f, asset.speedMultiplier);
                assigned = true;
            }
        }
    }
}
