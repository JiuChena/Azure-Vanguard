using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [MovedFrom("BehaviorCore")]
    public sealed class BehaviorTimelineHitboxClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("该时间片段会导出为一个 HitboxDef，开始时间和持续时间取自时间轴 Clip")]
        public HitboxDef hitboxData = new HitboxDef();

        public ClipCaps clipCaps => ClipCaps.ClipIn;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }
}
