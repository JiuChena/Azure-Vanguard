using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [TrackColor(0.95f, 0.65f, 0.25f)]
    [TrackClipType(typeof(BehaviorTimelineEventClipAsset))]
    [MovedFrom("BehaviorCore")]
    public sealed class BehaviorTimelineEventTrack : TrackAsset { }
}
