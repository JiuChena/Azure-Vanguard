using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译原生 AnimationTrack 为运行时动画轨道数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(AnimationTrack))]
    internal sealed class AnimationTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取原生动画轨道类型。
        /// </summary>
        /// <returns>AnimationTrack 类型。</returns>
        public System.Type TrackType => typeof(AnimationTrack);

        /// <summary>
        /// 导出原生动画片段。
        /// </summary>
        /// <param name="track">待导出的动画轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not AnimationTrack animationTrack || context == null) return;

            // 按原生 AnimationTrack 的片段顺序构建独立动画段。
            int layer = ResolveAnimationLayerFromTrackName(animationTrack.name);
            foreach (TimelineClip clip in animationTrack.GetClips())
            {
                if (clip?.asset is not AnimationPlayableAsset playableAsset || playableAsset.clip == null) continue;

                // 记录当前运行时无法精确复现的原生 Timeline 配置。
                if (Math.Abs(clip.clipIn) > 0.0001d)
                    context.AddWarning($"AnimationTrack '{animationTrack.name}' 的片段 '{clip.displayName}' 使用了 Clip In={clip.clipIn:F2}s，当前运行时不会精确复现该裁切。");

                if (Math.Abs(clip.timeScale - 1d) > 0.0001d)
                    context.AddWarning($"AnimationTrack '{animationTrack.name}' 的片段 '{clip.displayName}' 使用了 Time Scale={clip.timeScale:F2}，当前运行时不会精确复现该变速。");

                if (playableAsset.position != Vector3.zero || playableAsset.eulerAngles != Vector3.zero)
                    context.AddWarning($"AnimationTrack '{animationTrack.name}' 的片段 '{clip.displayName}' 配置了位置或旋转偏移，当前运行时不会导出这部分偏移。");

                context.ConsiderEndTime(clip.end);
                AnimationTrackExportState exportState = context.GetOrCreateExportState<AnimationTrackExportState>();
                exportState.Segments.Add(new AnimationSegment
                {
                    authoringTrackName = animationTrack.name,
                    clip = playableAsset.clip,
                    crossFadeDuration = Mathf.Clamp01((float)(Math.Max(clip.blendInDuration, clip.easeInDuration) / Math.Max(0.0001d, clip.duration))),
                    layer = layer,
                    startTime = Mathf.Max(0f, (float)clip.start)
                });
            }
        }

        /// <summary>
        /// 从轨道名解析动画层索引，支持 "L0" 与 "Layer 0" 两种写法。
        /// </summary>
        /// <param name="trackName">需要解析的 Timeline 轨道名。</param>
        /// <returns>解析出的层索引；无法解析时返回 0。</returns>
        private static int ResolveAnimationLayerFromTrackName(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName)) return 0;

            // 逐段读取约定的层编号标记。
            string[] tokens = trackName.Split(new[] { ' ', '_', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token.Length >= 2 && (token[0] == 'L' || token[0] == 'l') &&
                    int.TryParse(token.Substring(1), out int tokenLayer))
                {
                    return Mathf.Max(0, tokenLayer);
                }

                if ((string.Equals(token, "Layer", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "L", StringComparison.OrdinalIgnoreCase)) &&
                    i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int nextLayer))
                {
                    return Mathf.Max(0, nextLayer);
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// 收集并提交单次导出中的动画轨道片段。
    /// </summary>
    internal sealed class AnimationTrackExportState : IBehaviorTrackExportState
    {
        // 本次 Timeline 导出收集到的动画片段。
        public readonly System.Collections.Generic.List<AnimationSegment> Segments = new();

        /// <summary>
        /// 稳定排序动画片段并写入动画轨道数据。
        /// </summary>
        /// <param name="context">当前导出上下文；不得为 null。</param>
        public void Commit(BehaviorExportContext context)
        {
            // 按时间、轨道、层级与动画名建立稳定播放顺序。
            Segments.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;

                int result = left.startTime.CompareTo(right.startTime);
                if (result != 0) return result;
                result = string.Compare(left.authoringTrackName, right.authoringTrackName, StringComparison.Ordinal);
                if (result != 0) return result;
                result = left.layer.CompareTo(right.layer);
                return result != 0 ? result : string.Compare(left.clip != null ? left.clip.name : string.Empty,
                    right.clip != null ? right.clip.name : string.Empty, StringComparison.Ordinal);
            });

            context.GetOrCreateTrackData<AnimationTrackData>().segments = Segments.ToArray();
        }
    }
}
