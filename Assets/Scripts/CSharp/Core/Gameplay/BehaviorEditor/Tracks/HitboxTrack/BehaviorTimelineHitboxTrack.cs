using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [TrackColor(0.95f, 0.35f, 0.35f)]
    [TrackClipType(typeof(BehaviorTimelineHitboxClipAsset))]
    [MovedFrom("BehaviorCore")]
    public sealed class BehaviorTimelineHitboxTrack : TrackAsset { }
}
