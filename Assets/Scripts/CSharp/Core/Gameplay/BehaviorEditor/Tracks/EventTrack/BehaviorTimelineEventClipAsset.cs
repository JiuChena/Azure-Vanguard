using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [Serializable]
    [MovedFrom("BehaviorCore")]
    public sealed class BehaviorTimelineEventClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("该时间点会导出为一个 BehaviorEvent")]
        [HideInInspector]
        public BehaviorEvent eventData = new BehaviorEvent();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }
}
