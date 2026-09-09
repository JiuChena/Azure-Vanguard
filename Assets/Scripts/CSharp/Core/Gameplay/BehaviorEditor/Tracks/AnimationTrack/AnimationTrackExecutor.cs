using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 驱动动画片段时间表与片段切换的轨道执行器。
    /// </summary>
    internal sealed class AnimationTrackExecutor : IBehaviorTrackExecutor
    {
        // 当前轨道导出的动画片段数据。
        private readonly AnimationTrackData data;
        // 当前播放的共享宿主上下文。
        private readonly BehaviorExecutionContext context;
        // 动画轨道自行解析的 Animator 组件。
        private readonly Animator animator;
        // 动画轨道自行解析的片段播放适配器。
        private readonly IBehaviorAnimationPlayer animationPlayer;
        // 当前播放中各片段的起始时间表。
        private float[] segmentStartTimes = Array.Empty<float>();
        // 当前已经播放的片段索引。
        private int currentSegmentIndex;

        /// <summary>动画轨道的执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建动画轨道执行器。
        /// </summary>
        /// <param name="data">当前动画轨道导出数据；不得为 null。</param>
        /// <param name="context">当前播放执行上下文；不得为 null。</param>
        /// <param name="animator">动画轨道使用的 Animator；允许为 null。</param>
        /// <param name="animationPlayer">动画片段播放适配器；允许为 null。</param>
        public AnimationTrackExecutor(AnimationTrackData data, BehaviorExecutionContext context, Animator animator,
            IBehaviorAnimationPlayer animationPlayer)
        {
            this.data = data;
            this.context = context;
            this.animator = animator;
            this.animationPlayer = animationPlayer;
        }

        /// <summary>
        /// 构建本轨道的片段时间表并尝试播放首段动画。
        /// </summary>
        public void Begin()
        {
            AnimationSegment[] segments = data.segments;
            currentSegmentIndex = 0;
            if (segments == null || segments.Length == 0)
            {
                segmentStartTimes = Array.Empty<float>();
                return;
            }

            // 预计算隐式连接或显式配置的片段起始时间。
            segmentStartTimes = new float[segments.Length];
            float cursor = 0f;
            for (int index = 0; index < segments.Length; index++)
            {
                AnimationSegment segment = segments[index];
                float startTime = segment != null && segment.startTime >= 0f ? segment.startTime : cursor;
                segmentStartTimes[index] = Mathf.Max(0f, startTime);
                if (segment?.clip != null) cursor = Mathf.Max(cursor, segmentStartTimes[index] + segment.clip.length);
            }

            if (animator == null || animationPlayer == null || !animationPlayer.Initialize(animator))
            {
                if (data.logPlayback)
                    Debug.LogWarning($"[{context.OwnerGameObject.name}] AnimationTrack 缺少 Animator 或 IBehaviorAnimationPlayer，已跳过动画播放。", context.OwnerGameObject);
                return;
            }

            // 动画轨道自行应用行为播放速度并播放首段。
            animator.speed = context.PlaybackSpeed;
            PlaySegment(0);
        }

        /// <summary>
        /// 根据经过时间切换已跨入的后续动画段。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        public void Tick(float elapsedTime)
        {
            if (animator == null || animationPlayer == null || segmentStartTimes.Length == 0) return;
            while (currentSegmentIndex + 1 < segmentStartTimes.Length && elapsedTime >= segmentStartTimes[currentSegmentIndex + 1])
            {
                currentSegmentIndex++;
                PlaySegment(currentSegmentIndex);
            }
        }

        /// <summary>
        /// 清理本次播放的时间表和片段索引。
        /// </summary>
        public void Stop()
        {
            segmentStartTimes = Array.Empty<float>();
            currentSegmentIndex = 0;
            if (animator != null) animator.speed = 1f;
        }

        /// <summary>
        /// 请求动画适配器播放指定片段。
        /// </summary>
        /// <param name="index">轨道片段索引。</param>
        private void PlaySegment(int index)
        {
            AnimationSegment[] segments = data.segments;
            if (segments == null || index < 0 || index >= segments.Length) return;
            AnimationSegment segment = segments[index];
            if (segment?.clip == null) return;

            // 由动画轨道适配器完成状态槽选择和 CrossFade。
            if (animationPlayer.TryPlaySegment(segment, index, -1f, out string stateName))
            {
                if (data.logPlayback)
                    Debug.Log($"[{context.OwnerGameObject.name}] 切换动画片段：Clip={segment.clip.name} | Layer={segment.layer} | Slot={index} | State={stateName}", context.OwnerGameObject);
                return;
            }

            if (data.logPlayback)
                Debug.LogWarning($"AnimationTrack 无法播放动画片段 {segment.clip.name}。请确认动画播放器已初始化且存在 Layer {segment.layer} 的可用槽位 {index}。", context.OwnerGameObject);
        }
    }
}
