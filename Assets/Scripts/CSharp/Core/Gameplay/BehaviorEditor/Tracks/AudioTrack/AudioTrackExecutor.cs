using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using AudioType = Core.Gear.AudioType;

namespace BehaviorEditor
{
    /// <summary>经 AudioManager 池播音频片段：进入片段窗口起播，循环片段随窗口退出或停止回收。</summary>
    internal sealed class AudioTrackExecutor : IBehaviorTrackExecutor
    {
        // 本次播放的配置和宿主。
        private readonly AudioTrackData data;
        private readonly BehaviorExecutionContext context;
        // 窗口内已触发的片段：退出后重进会再次触发，如循环行为每圈重新起音。
        private readonly HashSet<long> windowed = new HashSet<long>();
        // 循环片段的池化源：窗口退出或行为停止时回收。
        private readonly Dictionary<long, AudioSource> looping = new Dictionary<long, AudioSource>();
        // 已提示过简化播放的片段。
        private readonly HashSet<long> warned = new HashSet<long>();
        /// <summary>获取轨道调度顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        #region 生命周期
        /// <summary>记录播放配置。</summary>
        /// <param name="data">音频轨道数据。</param>
        /// <param name="context">行为宿主上下文，可为空。</param>
        public AudioTrackExecutor(AudioTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>进入行为即对时间零求值，命中窗口的片段立即起播。</summary>
        public void Begin()
        {
            Stop();
            Tick(0f);
        }

        /// <summary>按片段窗口触发池播，退出窗口回收循环片段。</summary>
        /// <param name="elapsedTime">当前行为时间，单位：秒。</param>
        public void Tick(float elapsedTime)
        {
            if (data.tracks == null) return;
            AudioData settings = AudioDataManager.Instance.Data;
            for (int i = 0; i < data.tracks.Length; i++)
            {
                AudioTrackData.Track track = data.tracks[i];
                if (track?.segments == null) continue;
                for (int j = 0; j < track.segments.Length; j++)
                {
                    AudioTrackData.Segment segment = track.segments[j];
                    if (segment?.clip == null) continue;
                    long key = ((long)i << 32) | (uint)j;
                    bool inWindow = elapsedTime >= segment.start && elapsedTime < segment.start + segment.duration;
                    if (!inWindow)
                    {
                        // 循环片段窗口退出即回收；一次性片段不追停，播完自动回池。
                        if (windowed.Remove(key) && looping.TryGetValue(key, out AudioSource finished))
                        {
                            looping.Remove(key);
                            AudioManager.Instance.AudioStop(finished);
                        }
                        continue;
                    }

                    // BE 音频统一归 AudioManager 池：音量、静音与回收走管理器体系，单位无需挂 AudioSource。
                    if (context?.OwnerTransform == null) continue;
                    if (!windowed.Add(key)) continue;
                    WarnSimplified(key, segment);
                    float weight = Mathf.Clamp01(track.volume * segment.volume);
                    float soundVolume = settings.soundVolume;
                    double speed = segment.timeScale * context.PlaybackSpeed;
                    double clipIn = segment.clipIn;
                    float spatialBlend = Mathf.Clamp01(track.spatialBlend);
                    float stereoPan = Mathf.Clamp(track.stereoPan, -1f, 1f);
                    bool loop = segment.loop;
                    long captured = key;
                    AudioManager.Instance.AudioPlay(segment.clip, AudioType.Sound, context.OwnerTransform, source =>
                    {
                        if (source == null) return;
                        source.pitch = Mathf.Clamp((float)speed, 0.1f, 3f);
                        source.time = Mathf.Clamp((float)clipIn, 0f, source.clip != null ? source.clip.length : 0f);
                        source.spatialBlend = spatialBlend;
                        source.panStereo = stereoPan;
                        source.volume = soundVolume * weight;
                        if (loop) looping[captured] = source;
                    }, loop);
                }
            }
        }

        /// <summary>回收循环片段，重复调用安全；一次性片段播完自动回池。</summary>
        public void Stop()
        {
            foreach (KeyValuePair<long, AudioSource> pair in looping)
            {
                if (pair.Value != null) AudioManager.Instance.AudioStop(pair.Value);
            }
            looping.Clear();
            windowed.Clear();
        }
        #endregion

        /// <summary>池播简化语义提示：淡入淡出曲线不实现，每个片段只提示一次。</summary>
        private void WarnSimplified(long key, AudioTrackData.Segment segment)
        {
            if ((segment.mixInDuration > 0d || segment.mixOutDuration > 0d) && warned.Add(key))
                Debug.LogWarning($"BE Audio '{segment.clip.name}' 配置了淡入淡出，池播简化已忽略。", context?.OwnerGameObject);
        }
    }
}
