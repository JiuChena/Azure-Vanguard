using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为播放总调度器，负责推进全局时间轴并驱动轨道执行器。
    /// </summary>
    public sealed class BehaviorExecutor
    {
        // 行为播放宿主对象，用于轨道解析和日志关联。
        private readonly GameObject ownerGameObject;
        // 是否输出行为播放头的开始、停止、完成与循环日志。
        private readonly bool logBehaviorFlow;

        // 当前行为按执行顺序排列的轨道执行器。
        private readonly List<IBehaviorTrackExecutor> trackExecutors = new List<IBehaviorTrackExecutor>();

        /// <summary>当前正在播放的行为数据。</summary>
        public BehaviorClip CurrentClip { get; private set; }

        /// <summary>当前行为的播放配置。</summary>
        public BehaviorPlaybackSettings CurrentPlaybackSettings { get; private set; }

        /// <summary>当前行为已播放的经过时间，单位为秒。</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>当前行为的归一化时间，范围为 0 到 1。</summary>
        public float NormalizedTime { get; private set; }

        /// <summary>当前是否正在播放行为。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>行为以 <see cref="WrapMode.Once"/> 完成播放时触发。</summary>
        public event Action<BehaviorClip> OnCompleted;

        /// <summary>
        /// 创建一个绑定到指定宿主对象的行为播放执行器。
        /// </summary>
        /// <param name="ownerGameObject">行为轨道执行宿主对象；允许为 null。</param>
        /// <param name="logBehaviorFlow">是否输出播放头流程日志。</param>
        public BehaviorExecutor(GameObject ownerGameObject, bool logBehaviorFlow = false)
        {
            this.ownerGameObject = ownerGameObject;
            this.logBehaviorFlow = logBehaviorFlow;
        }

        #region Public

        /// <summary>
        /// 播放指定行为；会先停止旧行为并创建本次播放专用的轨道执行器。
        /// </summary>
        /// <param name="clip">待播放行为；为 null 时停止当前行为。</param>
        public void Play(BehaviorClip clip)
        {
            if (clip == null)
            {
                Stop();
                return;
            }

            // 验证全局播放配置，并跳过同一循环行为的重复请求。
            BehaviorPlaybackSettings playbackSettings = clip.playbackSettings;
            if (playbackSettings == null)
            {
                Debug.LogError($"BehaviorClip '{clip.name}' 缺少 BehaviorPlaybackSettings，无法播放。", clip);
                return;
            }

            if (CurrentClip == clip && IsPlaying && playbackSettings.wrapMode == WrapMode.Loop) return;

            // 建立新的播放头与轨道执行器。
            Stop();
            CurrentClip = clip;
            CurrentPlaybackSettings = playbackSettings;
            ElapsedTime = 0f;
            NormalizedTime = 0f;
            IsPlaying = true;
            BuildTrackExecutors(clip);

            // 每条轨道自行初始化自己的缓存、索引与执行状态。
            for (int index = 0; index < trackExecutors.Count; index++) trackExecutors[index].Begin();

            if (logBehaviorFlow)
                Debug.Log($"[{(ownerGameObject != null ? ownerGameObject.name : nameof(BehaviorExecutor))}] 开始行为：{clip.name} | Duration={playbackSettings.duration:F2}s | Wrap={playbackSettings.wrapMode}", ownerGameObject);

        }

        /// <summary>
        /// 推进全局时间轴并按顺序驱动本次播放的全部轨道。
        /// </summary>
        /// <param name="deltaTime">本帧未缩放时间增量，单位为秒。</param>
        public void Tick(float deltaTime)
        {
            if (!IsPlaying || CurrentClip == null || CurrentPlaybackSettings == null) return;

            // 更新全局播放头并将经过时间交给各轨道。
            ElapsedTime += deltaTime * Mathf.Max(0.01f, CurrentPlaybackSettings.speedMultiplier);
            float totalDuration = Mathf.Max(0.01f, CurrentPlaybackSettings.duration);
            NormalizedTime = Mathf.Clamp01(ElapsedTime / totalDuration);
            for (int index = 0; index < trackExecutors.Count; index++) trackExecutors[index].Tick(ElapsedTime);

            // 仅由总调度器处理播放配置定义的全局播放生命周期。
            if (CurrentPlaybackSettings.wrapMode == WrapMode.Loop)
            {
                if (ElapsedTime >= totalDuration) RestartLoopingClip(totalDuration);
                return;
            }

            if (CurrentPlaybackSettings.wrapMode == WrapMode.ClampForever)
            {
                if (NormalizedTime >= 1f)
                {
                    ElapsedTime = totalDuration;
                    NormalizedTime = 1f;
                }

                return;
            }

            if (NormalizedTime < 1f) return;

            // 保存完成对象后停止，再通知外部监听者。
            BehaviorClip completed = CurrentClip;
            if (logBehaviorFlow)
                Debug.Log($"[{(ownerGameObject != null ? ownerGameObject.name : nameof(BehaviorExecutor))}] 行为完成：{completed.name}", ownerGameObject);
            Stop();
            OnCompleted?.Invoke(completed);
        }

        /// <summary>
        /// 停止当前行为并要求所有轨道清理本次播放的临时状态。
        /// </summary>
        public void Stop()
        {
            if (logBehaviorFlow && IsPlaying && CurrentClip != null)
                Debug.Log($"[{(ownerGameObject != null ? ownerGameObject.name : nameof(BehaviorExecutor))}] 停止行为：{CurrentClip.name}", ownerGameObject);

            // 轨道负责清理自身的列表、数组与索引。
            for (int index = 0; index < trackExecutors.Count; index++)
                trackExecutors[index].Stop();
            trackExecutors.Clear();

            CurrentClip = null;
            CurrentPlaybackSettings = null;
            ElapsedTime = 0f;
            NormalizedTime = 0f;
            IsPlaying = false;
        }

        #endregion

        #region Unity Messages

        /// <summary>
        /// 向声明了 Gizmo 绘制能力的轨道请求当前播放状态的调试图形。
        /// </summary>
        public void DrawGizmos()
        {
            if (CurrentClip == null) return;

            // 总调度器不关心具体轨道类型，只调用可选绘制契约。
            for (int index = 0; index < trackExecutors.Count; index++)
            {
                if (trackExecutors[index] is IBehaviorTrackGizmoDrawer gizmoDrawer)
                    gizmoDrawer.DrawGizmos(ElapsedTime);
            }
        }

        #endregion

        #region Private

        /// <summary>
        /// 根据多态轨道数据创建并按执行顺序排序当前播放的执行器。
        /// </summary>
        /// <param name="clip">当前待播放的行为数据；允许为 null。</param>
        private void BuildTrackExecutors(BehaviorClip clip)
        {
            trackExecutors.Clear();
            if (clip?.trackData == null) return;

            // 上下文只提供各轨道共享的宿主信息与播放速度。
            var context = new BehaviorExecutionContext(
                ownerGameObject,
                CurrentPlaybackSettings != null ? CurrentPlaybackSettings.speedMultiplier : 1f);
            for (int index = 0; index < clip.trackData.Count; index++)
            {
                IBehaviorTrackExecutor trackExecutor = clip.trackData[index]?.CreateExecutor(context);
                if (trackExecutor != null) trackExecutors.Add(trackExecutor);
            }

            trackExecutors.Sort((left, right) => left.ExecutionOrder.CompareTo(right.ExecutionOrder));
        }

        /// <summary>
        /// 回卷全局时间并为下一次循环重建所有轨道的局部状态。
        /// </summary>
        /// <param name="totalDuration">当前行为有效总时长，必须大于零。</param>
        private void RestartLoopingClip(float totalDuration)
        {
            // 先停止上一轮轨道，避免事件索引和命中区域进入下一循环。
            ElapsedTime %= totalDuration;
            NormalizedTime = Mathf.Clamp01(ElapsedTime / totalDuration);
            for (int index = 0; index < trackExecutors.Count; index++) trackExecutors[index].Stop();

            // 创建新的轨道实例并在回卷时间点开始本轮播放。
            BuildTrackExecutors(CurrentClip);
            for (int index = 0; index < trackExecutors.Count; index++) trackExecutors[index].Begin();
        }

        #endregion
    }
}
