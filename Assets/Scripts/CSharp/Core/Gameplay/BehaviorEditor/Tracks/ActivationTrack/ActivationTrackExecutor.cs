using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>根据时间区间控制绑定对象并在停止时应用结束状态。</summary>
    internal sealed class ActivationTrackExecutor : IBehaviorTrackExecutor
    {
        // 本次播放的数据和宿主上下文。
        private readonly ActivationTrackData data;
        private readonly BehaviorExecutionContext context;
        // 按轨道索引缓存目标与原始 activeSelf。
        private GameObject[] targets;
        private bool[] originalStates;
        /// <summary>获取轨道调度顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        #region 生命周期
        /// <summary>缓存本次播放所用配置。</summary>
        /// <param name="data">激活轨道数据。</param>
        /// <param name="context">宿主上下文，可为空。</param>
        public ActivationTrackExecutor(ActivationTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>严格解析目标，捕获原状态并求值零时刻。</summary>
        public void Begin()
        {
            Stop();
            int count = data.tracks?.Length ?? 0;
            targets = new GameObject[count];
            originalStates = new bool[count];
            for (int i = 0; i < count; i++)
            {
                ActivationTrackData.Track track = data.tracks[i];
                if (track == null || track.targetPath == null || context?.OwnerTransform == null) continue;
                Transform target = track.targetPath.Length == 0 ? context.OwnerTransform : context.OwnerTransform.Find(track.targetPath);
                if (target == null)
                {
                    Debug.LogWarning($"BE Activation '{track.trackName}' 找不到路径 '{track.targetPath}'，已跳过。", context.OwnerGameObject);
                    continue;
                }
                targets[i] = target.gameObject;
                originalStates[i] = targets[i].activeSelf;
            }
            Tick(0f);
        }

        /// <summary>按区间并集设置状态，不在每个片段结束时应用轨道结束策略。</summary>
        /// <param name="elapsedTime">行为时间，单位：秒。</param>
        public void Tick(float elapsedTime)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                bool active = false;
                Vector2[] intervals = data.tracks[i].intervals;
                for (int j = 0; intervals != null && j < intervals.Length; j++)
                {
                    if (elapsedTime < intervals[j].x || elapsedTime >= intervals[j].y) continue;
                    active = true;
                    break;
                }
                if (targets[i].activeSelf != active) targets[i].SetActive(active);
            }
        }

        /// <summary>应用轨道结束策略并释放本次播放缓存；重复调用安全。</summary>
        public void Stop()
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                switch (data.tracks[i].postPlaybackState)
                {
                    case ActivationTrack.PostPlaybackState.Active: targets[i].SetActive(true); break;
                    case ActivationTrack.PostPlaybackState.Inactive: targets[i].SetActive(false); break;
                    case ActivationTrack.PostPlaybackState.Revert: targets[i].SetActive(originalStates[i]); break;
                }
            }
            targets = null;
            originalStates = null;
        }
        #endregion
    }
}
