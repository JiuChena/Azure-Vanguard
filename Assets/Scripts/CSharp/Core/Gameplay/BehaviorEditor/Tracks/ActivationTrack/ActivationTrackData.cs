using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>原生激活轨道导出的对象路径和激活区间。</summary>
    [Serializable]
    public sealed class ActivationTrackData : BehaviorTrackData
    {
        // 保持各原生轨道的绑定与结束策略独立。
        [Tooltip("各对象的激活轨道；路径相对于行为宿主，空路径表示宿主本身。")]
        public Track[] tracks = Array.Empty<Track>();

        /// <summary>获取轨道显示名称。</summary>
        public override string DisplayName => "Activation";

        #region 创建
        /// <summary>建立激活轨道并使其先于其他轨道执行。</summary>
        public ActivationTrackData() { executionOrder = -100; }

        /// <summary>创建本次播放的激活执行器。</summary>
        /// <param name="context">行为宿主上下文。</param>
        /// <returns>激活执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new ActivationTrackExecutor(this, context);
        }
        #endregion

        /// <summary>一个绑定对象的激活轨道。</summary>
        [Serializable]
        public sealed class Track
        {
            // 原始轨道名用于诊断。
            [Tooltip("原始 Timeline 轨道名称。")]
            public string trackName;
            // 仅保存单位内路径，不持有场景对象。
            [Tooltip("相对于行为宿主的对象路径；空字符串表示宿主。")]
            public string targetPath;
            // 停止整个行为时才应用结束策略。
            [Tooltip("整个行为停止时的对象状态，与原生 ActivationTrack 一致。")]
            public ActivationTrack.PostPlaybackState postPlaybackState;
            // 同轨片段采用并集判断。
            [Tooltip("激活区间，单位：秒；重叠区间按并集执行。")]
            public Vector2[] intervals = Array.Empty<Vector2>();
        }
    }
}
