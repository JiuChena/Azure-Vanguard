using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// AnimationTrack 的 Animator 状态槽绑定。
    /// </summary>
    [Serializable]
    [MovedFrom("BehaviorCore")]
    public class AnimatorSegmentSlotBinding
    {
        [Tooltip("该槽位所属的 Animator Layer")]
        [Min(0)]
        public int layer;

        [Tooltip("该槽位在同一 Layer 中的顺序索引，通常对应行为片段索引")]
        [Min(0)]
        public int slotIndex;

        [Tooltip("Animator Controller 中用于播放该槽位的状态名")]
        public string stateName;

        [Tooltip("AnimatorOverrideController 中要被替换的占位动画名")]
        public string placeholderClipName;
    }
}
