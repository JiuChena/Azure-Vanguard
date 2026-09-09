using System.Collections.Generic;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为编辑器主窗口的 Timeline 轨道枚举工具。
    /// </summary>
    internal sealed partial class BehaviorEditorWindow
    {
        /// <summary>
        /// 枚举 Timeline 中的全部轨道（递归展开组轨道）。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <returns>轨道序列。</returns>
        private static IEnumerable<TrackAsset> EnumerateTimelineTracks(TimelineAsset timelineAsset)
        {
            if (timelineAsset == null)
                yield break;

            foreach (TrackAsset rootTrack in timelineAsset.GetRootTracks())
            {
                foreach (TrackAsset track in EnumerateTrackRecursive(rootTrack))
                    yield return track;
            }
        }

        /// <summary>
        /// 递归枚举轨道及其子轨道，组轨道本身不产出。
        /// </summary>
        /// <param name="track">起始轨道。</param>
        /// <returns>轨道序列。</returns>
        private static IEnumerable<TrackAsset> EnumerateTrackRecursive(TrackAsset track)
        {
            if (track == null)
                yield break;

            if (track is not GroupTrack)
                yield return track;

            foreach (TrackAsset childTrack in track.GetChildTracks())
            {
                foreach (TrackAsset nestedTrack in EnumerateTrackRecursive(childTrack))
                    yield return nestedTrack;
            }
        }
    }
}
