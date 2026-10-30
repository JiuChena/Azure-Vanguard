using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>将原生激活轨道编译为独立的运行时对象区间。</summary>
    [BehaviorTrackCompiler(typeof(ActivationTrack))]
    internal sealed class ActivationTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>获取支持的原生轨道类型。</summary>
        public Type TrackType => typeof(ActivationTrack);
        #region 导出
        /// <summary>导出激活轨道的绑定、区间和停止策略。</summary>
        /// <param name="track">待导出原生轨道。</param>
        /// <param name="context">当前编译上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not ActivationTrack activation || context == null) return;
            if (!NativeTrackBindingExportUtility.TryGetPath(track, context, false, out string path)) return;
            ActivationTrackExportState state = context.GetOrCreateExportState<ActivationTrackExportState>();
            for (int i = 0; i < state.Tracks.Count; i++)
            {
                if (state.Tracks[i].targetPath != path) continue;
                context.AddWarning($"ActivationTrack '{track.name}' 与 '{state.Tracks[i].trackName}' 绑定同一对象，结束策略有歧义，保留第一条轨道并跳过当前轨道。");
                return;
            }

            // 空轨也保留，使绑定对象在整个行为期间保持关闭。
            var intervals = new List<Vector2>();
            foreach (TimelineClip clip in activation.GetClips())
            {
                intervals.Add(new Vector2((float)clip.start, (float)clip.end));
                context.ConsiderEndTime(clip.end);
            }
            intervals.Sort((left, right) => left.x.CompareTo(right.x));
            state.Tracks.Add(new ActivationTrackData.Track
            {
                trackName = track.name, targetPath = path,
                postPlaybackState = activation.postPlaybackState, intervals = intervals.ToArray()
            });
        }
        #endregion
    }

    /// <summary>汇总本次导出的原生激活轨道。</summary>
    internal sealed class ActivationTrackExportState : IBehaviorTrackExportState
    {
        // 按 Timeline 遍历顺序保留对象轨道。
        public readonly List<ActivationTrackData.Track> Tracks = new();
        #region 提交
        /// <summary>写入唯一的激活轨道数据容器。</summary>
        /// <param name="context">当前编译上下文。</param>
        public void Commit(BehaviorExportContext context)
        {
            context.GetOrCreateTrackData<ActivationTrackData>().tracks = Tracks.ToArray();
        }
        #endregion
    }
}
