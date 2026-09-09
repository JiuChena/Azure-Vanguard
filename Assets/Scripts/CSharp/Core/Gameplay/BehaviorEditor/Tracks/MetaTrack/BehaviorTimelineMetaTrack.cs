using System;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [TrackColor(0.45f, 0.85f, 0.45f)]
    [TrackClipType(typeof(BehaviorTimelineMetaClipAsset))]
    [MovedFrom("BehaviorCore")]
    public sealed class BehaviorTimelineMetaTrack : TrackAsset { }
}
