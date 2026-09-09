using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 动画作者轨道导出的运行时片段集合。
    /// </summary>
    [Serializable]
    public sealed class AnimationTrackData : BehaviorTrackData
    {
        // 当前轨道导出的动画片段集合。
        [Tooltip("按时间顺序播放的动画片段。")]
        public AnimationSegment[] segments = Array.Empty<AnimationSegment>();

        // 是否输出当前轨道的动画片段切换诊断日志。
        [Tooltip("开启后输出动画片段切换和播放器缺失等诊断日志。")]
        public bool logPlayback;

        /// <summary>
        /// 创建包含动画轨道默认调度顺序的数据。
        /// </summary>
        public AnimationTrackData()
        {
            executionOrder = 0;
        }

        /// <summary>
        /// 获取动画轨道的显示名称。
        /// </summary>
        /// <returns>固定的动画轨道名称。</returns>
        public override string DisplayName => "Animation";

        /// <summary>
        /// 创建动画片段执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>用于播放动画片段的执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            if (context?.OwnerGameObject == null) return null;

            // 动画轨道自行解析所需的 Animator 与片段播放适配器。
            Animator animator = context.OwnerGameObject.GetComponentInChildren<Animator>();
            MonoBehaviour[] components = context.OwnerGameObject.GetComponentsInChildren<MonoBehaviour>(true);
            IBehaviorAnimationPlayer animationPlayer = null;
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IBehaviorAnimationPlayer resolvedPlayer)
                {
                    animationPlayer = resolvedPlayer;
                    break;
                }
            }

            return new AnimationTrackExecutor(this, context, animator, animationPlayer);
        }
    }
}
