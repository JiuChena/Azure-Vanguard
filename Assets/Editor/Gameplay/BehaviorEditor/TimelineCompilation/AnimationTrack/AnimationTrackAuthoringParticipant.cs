using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 为 AnimationTrack 作者期预览准备 Animator 组件与轨道绑定。
    /// </summary>
    internal sealed class AnimationTrackAuthoringParticipant : IBehaviorAuthoringParticipant
    {
        // 当前会话中由本参与者自动创建的 Animator。
        private Animator createdAnimator;

        /// <summary>
        /// 解析 AnimationTrack 预览 Animator，并绑定 Timeline 中的全部动画轨道。
        /// </summary>
        /// <param name="context">当前作者期会话上下文；不得为 null。</param>
        public void BeginAuthoring(BehaviorAuthoringSessionContext context)
        {
            if (context?.Director == null || context.Timeline == null || context.ReferenceRoot == null) return;
            if (!ContainsAnimationTrack(context.Timeline)) return;

            // 优先复用角色已有 Animator，缺失时仅为本次预览补齐。
            Animator animator = context.ReferenceRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = UnityEditor.Undo.AddComponent<Animator>(context.ReferenceRoot);
                createdAnimator = animator;
                Debug.LogWarning($"角色对象 '{context.ReferenceRoot.name}' 缺少 Animator，AnimationTrack 预览已自动补齐一个组件。", context.ReferenceRoot);
            }

            // 只向原生 AnimationTrack 写入预览绑定。
            foreach (TrackAsset track in EnumerateTracks(context.Timeline))
            {
                if (track is AnimationTrack && context.Director.GetGenericBinding(track) != animator)
                    context.Director.SetGenericBinding(track, animator);
            }
        }

        /// <summary>
        /// 移除本参与者为预览临时创建的 Animator。
        /// </summary>
        /// <param name="context">当前作者期会话上下文；允许为 null。</param>
        public void EndAuthoring(BehaviorAuthoringSessionContext context)
        {
            if (createdAnimator == null) return;

            // 只清理自身创建的组件，不触碰用户原有 Animator。
            UnityEditor.Undo.DestroyObjectImmediate(createdAnimator);
            createdAnimator = null;
        }

        /// <summary>
        /// 判断 Timeline 是否含有至少一个原生 AnimationTrack。
        /// </summary>
        /// <param name="timeline">待检查的 Timeline 资产；允许为 null。</param>
        /// <returns>存在 AnimationTrack 时返回 true。</returns>
        private static bool ContainsAnimationTrack(TimelineAsset timeline)
        {
            if (timeline == null) return false;
            foreach (TrackAsset track in EnumerateTracks(timeline))
            {
                if (track is AnimationTrack) return true;
            }

            return false;
        }

        /// <summary>
        /// 递归枚举 Timeline 中的轨道，支持嵌套组轨道。
        /// </summary>
        /// <param name="timeline">待枚举的 Timeline 资产；允许为 null。</param>
        /// <returns>全部非组轨道。</returns>
        private static System.Collections.Generic.IEnumerable<TrackAsset> EnumerateTracks(TimelineAsset timeline)
        {
            if (timeline == null) yield break;

            foreach (TrackAsset rootTrack in timeline.GetRootTracks())
            {
                foreach (TrackAsset track in EnumerateTrack(rootTrack))
                    yield return track;
            }
        }

        /// <summary>
        /// 递归枚举指定轨道及其子轨道。
        /// </summary>
        /// <param name="track">枚举起点轨道；允许为 null。</param>
        /// <returns>全部非组轨道。</returns>
        private static System.Collections.Generic.IEnumerable<TrackAsset> EnumerateTrack(TrackAsset track)
        {
            if (track == null) yield break;
            if (track is not GroupTrack) yield return track;

            foreach (TrackAsset childTrack in track.GetChildTracks())
            {
                foreach (TrackAsset nestedTrack in EnumerateTrack(childTrack))
                    yield return nestedTrack;
            }
        }
    }
}
