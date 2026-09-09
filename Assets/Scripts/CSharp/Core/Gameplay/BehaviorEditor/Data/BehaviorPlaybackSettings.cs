using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 保存行为播放头的全局运行时配置。
    /// </summary>
    [Serializable]
    public sealed class BehaviorPlaybackSettings
    {
        // 行为在未缩放时间轴中的总时长。
        [Tooltip("行为在未缩放时间轴中的总时长，单位：秒。")]
        [Min(0.01f)]
        public float duration = 1f;

        // 行为播放结束后的包裹模式。
        [Tooltip("行为播放完成后的包裹模式。")]
        public WrapMode wrapMode = WrapMode.Once;

        // 行为全局播放速度倍率。
        [Tooltip("行为全局播放速度倍率。")]
        [Min(0.01f)]
        public float speedMultiplier = 1f;
    }
}
