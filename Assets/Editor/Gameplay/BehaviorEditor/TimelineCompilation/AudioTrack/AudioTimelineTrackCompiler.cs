using System;
using System.Collections.Generic;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>将原生音频轨道编译为可复用的音频混合数据。</summary>
    [BehaviorTrackCompiler(typeof(AudioTrack))]
    internal sealed class AudioTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>获取支持的原生轨道类型。</summary>
        public Type TrackType => typeof(AudioTrack);
        #region 导出
        /// <summary>导出音频绑定、静态混音参数和片段时间信息。</summary>
        /// <param name="track">原生轨道。</param>
        /// <param name="context">当前导出环境。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not AudioTrack audio || context == null) return;
            // 池播模型下作者绑定仅决定挂载位置：未绑定或绑定无效时回退单位根，不再跳过整条轨道。
            if (!NativeTrackBindingExportUtility.TryGetPath(track, context, true, out string path)) path = string.Empty;
            if (audio.hasCurves)
                context.AddWarning($"AudioTrack '{track.name}' 使用动画化混音参数，BE 当前仅导出静态音量、声像和空间混合。");
            var segments = new List<AudioTrackData.Segment>();
            foreach (TimelineClip clip in audio.GetClips())
            {
                if (clip.asset is not AudioPlayableAsset asset || asset.clip == null) continue;
                if (clip.hasCurves)
                    context.AddWarning($"AudioTrack '{track.name}' 片段 '{clip.displayName}' 使用动画化参数，BE 仅导出静态片段音量。");
                segments.Add(new AudioTrackData.Segment
                {
                    clip = asset.clip, start = clip.start, duration = clip.duration,
                    clipIn = clip.clipIn, timeScale = clip.timeScale, loop = asset.loop,
                    volume = NativeTrackBindingExportUtility.ReadFloat(asset, "m_ClipProperties.volume", 1f, context),
                    mixInDuration = clip.mixInDuration, mixOutDuration = clip.mixOutDuration,
                    mixInCurve = clip.mixInCurve, mixOutCurve = clip.mixOutCurve
                });
                context.ConsiderEndTime(clip.end);
            }
            segments.Sort((left, right) => left.start.CompareTo(right.start));
            context.GetOrCreateExportState<AudioTrackExportState>().Tracks.Add(new AudioTrackData.Track
            {
                trackName = track.name, targetPath = path, segments = segments.ToArray(),
                volume = NativeTrackBindingExportUtility.ReadFloat(audio, "m_TrackProperties.volume", 1f, context),
                stereoPan = NativeTrackBindingExportUtility.ReadFloat(audio, "m_TrackProperties.stereoPan", 0f, context),
                spatialBlend = NativeTrackBindingExportUtility.ReadFloat(audio, "m_TrackProperties.spatialBlend", 0f, context)
            });
        }
        #endregion
    }

    /// <summary>汇总本次导出的原生音频轨道。</summary>
    internal sealed class AudioTrackExportState : IBehaviorTrackExportState
    {
        // 各原生轨道保留独立的绑定与混音输出。
        public readonly List<AudioTrackData.Track> Tracks = new();
        #region 提交
        /// <summary>写入唯一的音频轨道数据容器。</summary>
        /// <param name="context">当前编译环境。</param>
        public void Commit(BehaviorExportContext context)
        {
            context.GetOrCreateTrackData<AudioTrackData>().tracks = Tracks.ToArray();
        }
        #endregion
    }
}
