using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为内的单个动画片段。
    /// </summary>
    [Serializable]
    public class AnimationSegment
    {
        [Tooltip("作者期来源的 Timeline 轨道名。用于保持导出数据的稳定排序。")]
        public string authoringTrackName;

        [Tooltip("该时间段实际播放的动画资源")]
        public AnimationClip clip;

        [Tooltip("切入该片段时使用的归一化过渡比例，0 表示瞬切，1 表示按当前 Animator 状态完整过渡")]
        [Range(0f, 1f)]
        public float crossFadeDuration = 0.25f;

        [Tooltip("动画播放到的 Animator Layer")]
        [Min(0)]
        public int layer;

        [Tooltip("该片段在行为时间轴中的开始时间。小于 0 表示自动衔接上一段，大于等于 0 表示使用显式时间")]
        public float startTime = -1f;
    }
}
