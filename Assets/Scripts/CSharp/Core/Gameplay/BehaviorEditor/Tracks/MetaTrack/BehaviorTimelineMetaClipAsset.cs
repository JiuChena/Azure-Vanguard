using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [Serializable]
    [MovedFrom("BehaviorCore")]
    public sealed class BehaviorTimelineMetaClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("行为播放完成后的包裹模式")]
        public WrapMode wrapMode = WrapMode.Once;

        [Tooltip("行为全局播放速度倍率")]
        [Min(0.01f)]
        public float speedMultiplier = 1f;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }
}
