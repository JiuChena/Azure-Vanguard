using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>原生音频轨道导出的绑定、混音和片段时间数据。</summary>
    [Serializable]
    public sealed class AudioTrackData : BehaviorTrackData
    {
        // 每条原生轨道拥有独立混音输出。
        [Tooltip("原生音频轨道集合，运行时按单位内 AudioSource 路径解析。")]
        public Track[] tracks = Array.Empty<Track>();
        /// <summary>获取轨道显示名称。</summary>
        public override string DisplayName => "Audio";

        #region 创建
        /// <summary>创建本次行为的音频执行器。</summary>
        /// <param name="context">宿主与全局速度。</param>
        /// <returns>音频执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new AudioTrackExecutor(this, context);
        }
        #endregion

        /// <summary>一条原生音频轨道的静态混音设置。</summary>
        [Serializable]
        public sealed class Track
        {
            // 原始名称及单位内绑定路径。
            [Tooltip("原始 Timeline 轨道名称。")]
            public string trackName;
            [Tooltip("AudioSource 相对于宿主的路径；空字符串表示宿主。")]
            public string targetPath;
            // 原生轨道混音参数。
            [Tooltip("轨道音量，范围 0–1。"), Range(0f, 1f)]
            public float volume = 1f;
            [Tooltip("立体声声像，范围 -1–1。"), Range(-1f, 1f)]
            public float stereoPan;
            [Tooltip("空间混合，0 为二维、1 为三维。"), Range(0f, 1f)]
            public float spatialBlend;
            // 保留重叠片段，运行时同时混音。
            [Tooltip("本轨道音频片段，允许时间重叠。")]
            public Segment[] segments = Array.Empty<Segment>();
        }

        /// <summary>单个音频片段的资源、时间与混合曲线。</summary>
        [Serializable]
        public sealed class Segment
        {
            // 直接持有导出的音频资源。
            [Tooltip("原生片段引用的音频资源。")]
            public AudioClip clip;
            // 时间参数保持双精度以避免长音频精度损失。
            [Header("时间")]
            [Tooltip("片段在行为时间轴上的开始时间，单位：秒。")]
            public double start;
            [Tooltip("片段在行为时间轴上的持续时间，单位：秒。")]
            public double duration;
            [Tooltip("源音频裁切起点，单位：秒。")]
            public double clipIn;
            [Tooltip("源音频相对于行为时间的播放倍率。")]
            public double timeScale = 1d;
            // 循环与权重参数。
            [Header("混音")]
            [Tooltip("源音频播完后是否在本片段区间内循环。")]
            public bool loop;
            [Tooltip("片段音量，范围 0–1。"), Range(0f, 1f)]
            public float volume = 1f;
            [Tooltip("淡入或重叠混入时长，单位：秒。")]
            public double mixInDuration;
            [Tooltip("淡出或重叠混出时长，单位：秒。")]
            public double mixOutDuration;
            [Tooltip("归一化混入时间对应的权重曲线。")]
            public AnimationCurve mixInCurve;
            [Tooltip("归一化混出时间对应的权重曲线。")]
            public AnimationCurve mixOutCurve;
        }
    }
}
