using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 单次 Timeline 编译期间共享的输出数据与编辑器环境。
    /// </summary>
    internal sealed class BehaviorExportContext
    {
        // 轨道数据类型 → 本次编译的唯一输出实例，不包含播放配置。
        private readonly Dictionary<Type, BehaviorTrackData> trackDataByType = new Dictionary<Type, BehaviorTrackData>();

        // 本次编译收集的可显示警告。
        private readonly List<string> warnings = new List<string>();

        // 导出状态类型 → 具体轨道在本次编译中维护的临时收集状态。
        private readonly Dictionary<Type, IBehaviorTrackExportState> exportStatesByType =
            new Dictionary<Type, IBehaviorTrackExportState>();

        /// <summary>正在编译的 Timeline 资产。</summary>
        public TimelineAsset Timeline { get; }
        /// <summary>Timeline 预览或绑定查询使用的 Director。</summary>
        public PlayableDirector Director { get; }
        /// <summary>骨骼与场景对象路径计算的参考根节点。</summary>
        public Transform ReferenceRoot { get; }
        /// <summary>当前导出的全局播放配置。</summary>
        public BehaviorPlaybackSettings PlaybackSettings { get; }
        /// <summary>全部轨道片段中的最大结束时间。</summary>
        public double MaxEndTime { get; private set; }
        /// <summary>本次编译产生的诊断警告。</summary>
        public IReadOnlyList<string> Warnings => warnings;

        /// <summary>
        /// 创建单次 Timeline 编译上下文。
        /// </summary>
        /// <param name="timeline">待编译的 Timeline 资产。</param>
        /// <param name="director">预览与绑定查询使用的 Director，可为 null。</param>
        /// <param name="referenceRoot">骨骼路径基准，可为 null。</param>
        /// <param name="fallbackSettings">缺少 Meta 轨道时的回退播放配置。</param>
        public BehaviorExportContext(TimelineAsset timeline, PlayableDirector director, Transform referenceRoot,
            BehaviorPlaybackSettings fallbackSettings)
        {
            Timeline = timeline;
            Director = director;
            ReferenceRoot = referenceRoot;
            BehaviorPlaybackSettings fallback = fallbackSettings ?? new BehaviorPlaybackSettings();
            PlaybackSettings = new BehaviorPlaybackSettings
            {
                duration = Mathf.Max(0.01f, fallback.duration),
                wrapMode = fallback.wrapMode,
                speedMultiplier = Mathf.Max(0.01f, fallback.speedMultiplier)
            };
        }

        /// <summary>
        /// 获取或创建本次编译中指定类型的轨道数据。
        /// </summary>
        /// <typeparam name="TTrackData">需要获取的轨道数据类型。</typeparam>
        /// <returns>当前编译的唯一轨道数据实例。</returns>
        public TTrackData GetOrCreateTrackData<TTrackData>() where TTrackData : BehaviorTrackData, new()
        {
            if (trackDataByType.TryGetValue(typeof(TTrackData), out BehaviorTrackData existing))
                return (TTrackData)existing;

            TTrackData created = new TTrackData();
            trackDataByType.Add(typeof(TTrackData), created);
            return created;
        }

        /// <summary>
        /// 获取或创建当前导出中指定轨道的临时收集状态。
        /// </summary>
        /// <typeparam name="TExportState">轨道私有导出状态类型。</typeparam>
        /// <returns>当前导出的唯一临时状态实例。</returns>
        public TExportState GetOrCreateExportState<TExportState>()
            where TExportState : class, IBehaviorTrackExportState, new()
        {
            if (exportStatesByType.TryGetValue(typeof(TExportState), out IBehaviorTrackExportState existing))
                return (TExportState)existing;

            TExportState created = new TExportState();
            exportStatesByType.Add(typeof(TExportState), created);
            return created;
        }

        /// <summary>
        /// 设置本次导出的行为结束时间上界。
        /// </summary>
        /// <param name="endTime">候选结束时间，负数会被忽略。</param>
        public void ConsiderEndTime(double endTime)
        {
            MaxEndTime = endTime > MaxEndTime ? endTime : MaxEndTime;
        }

        /// <summary>
        /// 记录一次可显示的导出警告。
        /// </summary>
        /// <param name="warning">警告文本；为空时忽略。</param>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
                warnings.Add(warning);
        }

        /// <summary>
        /// 将本次收集的多态轨道数据提交到目标 BehaviorClip。
        /// </summary>
        /// <param name="target">需要写入的目标行为资产。</param>
        public void CommitTo(BehaviorClip target)
        {
            if (target == null)
                return;

            // 各轨道自行收尾其临时数据并写入对应的多态轨道数据。
            foreach (IBehaviorTrackExportState exportState in exportStatesByType.Values)
                exportState.Commit(this);
            PlaybackSettings.duration = Mathf.Max(
                0.01f,
                (float)Math.Max(MaxEndTime, Timeline != null ? Timeline.duration : 0d));

            // 整体替换导出结果，避免作者期删除轨道后保留过期运行时数据。
            target.ReplaceTrackData(trackDataByType.Values);
            target.playbackSettings = PlaybackSettings;
        }
    }
}
