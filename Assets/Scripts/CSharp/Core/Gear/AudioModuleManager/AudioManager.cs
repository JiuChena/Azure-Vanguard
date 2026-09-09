using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 音频管理器：集中管理音频池与音量事件，播放计时由各音频物体上的 GOTimer 注入回池回调完成。
    /// </summary>
    public class AudioManager
    {
        private static readonly AudioManager instance = new AudioManager();
        public static AudioManager Instance => instance;

        // 空闲 AudioSource 池
        private readonly Queue<AudioSource> _idleSources = new Queue<AudioSource>();

        // 活跃播放信息表：AudioSource → 播放信息
        private readonly Dictionary<AudioSource, ActiveAudioInfo> _activeAudios = new Dictionary<AudioSource, ActiveAudioInfo>();

        // 所有池化 AudioSource 的父节点
        private Transform _root;

        private AudioManager()
        {
            // 集中订阅一次设置变更，音量刷新统一由本管理器遍历活跃表完成
            AudioDataManager.Instance.SettingsChanged += OnAudioSettingsChanged;
        }

        /// <summary>
        /// 播放已由调用方加载并持有的 AudioClip。
        /// AudioManager 只管理播放实例；资源加载、缓存与释放由调用方负责。
        /// </summary>
        /// <param name="clip">已加载的 AudioClip。</param>
        /// <param name="type">音频类型（音乐/音效）。</param>
        /// <param name="parent">挂载的父对象 Transform；null 时挂在音频管理器根节点。</param>
        /// <param name="callback">播放初始化完成后的回调。</param>
        /// <param name="loop">是否循环播放；循环时调用方需通过 AudioStop 手动回收。</param>
        public void AudioPlay(AudioClip clip, AudioType type, Transform parent, Action<AudioSource> callback = null, bool loop = false)
        {
            if (clip == null) return;

            AudioSource source = GetPooledSource();
            if (parent != null)
            {
                source.transform.SetParent(parent, false);
                source.transform.localPosition = Vector3.zero;
            }

            source.clip = clip;
            source.loop = loop;
            source.gameObject.SetActive(true);
            ApplyTypeSettings(source, type);
            _activeAudios[source] = new ActiveAudioInfo(type);

            // 非循环：向 GOTimer 注入回池回调，到点自动回收；循环由调用方手动 AudioStop
            if (!loop)
            {
                GOTimer timer = source.GetComponent<GOTimer>();
                if (timer != null) timer.Register(clip.length + 0.5f, () => ReleaseSource(source));
            }

            source.Play();
            callback?.Invoke(source);
        }

        /// <summary>
        /// 停止并回收指定 AudioSource 所属的池化播放实例。
        /// </summary>
        /// <param name="source">正在播放的 AudioSource。</param>
        public void AudioStop(AudioSource source)
        {
            if (source == null) return;

            ReleaseSource(source);
        }

        /// <summary>
        /// 强制回收全部在外播放实例。用于场景切换或全局音频释放。
        /// </summary>
        public void ReleaseAll()
        {
            RemoveDestroyedActiveSources();

            // 先快照活跃源，回收会修改 _activeAudios，不能边遍历边改
            List<AudioSource> sources = new List<AudioSource>(_activeAudios.Keys);
            for (int index = 0; index < sources.Count; index++)
            {
                if (sources[index] != null) ReleaseSource(sources[index]);
            }

            _activeAudios.Clear();

        }

        /// <summary>
        /// 从池获取空闲 AudioSource，池空则创建挂 AudioSource 与 GOTimer 的新实例。
        /// </summary>
        private AudioSource GetPooledSource()
        {
            EnsureRoot();
            RemoveDestroyedActiveSources();

            // 优先复用空闲 AudioSource
            while (_idleSources.Count > 0)
            {
                AudioSource cached = _idleSources.Dequeue();
                if (cached != null) return cached;
            }

            // 池空：创建新的 AudioSource 物体（音频物体只挂 AudioSource 与 GOTimer）
            GameObject sourceObject = new GameObject("PooledAudioSource");
            sourceObject.transform.SetParent(_root, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sourceObject.AddComponent<GOTimer>();
            return source;
        }

        /// <summary>
        /// 回收指定 AudioSource：停止播放、清除状态、清空 GOTimer 计时并归还空闲池。
        /// </summary>
        /// <param name="source">需要回收的 AudioSource。</param>
        private void ReleaseSource(AudioSource source)
        {
            if (source == null) return;

            // 已不在活跃表说明已被回收，防 GOTimer 回调与外部 AudioStop 双重回收
            if (!_activeAudios.Remove(source)) return;

            // 清空 GOTimer 上的待执行计时回调
            GOTimer timer = source.GetComponent<GOTimer>();
            if (timer != null) timer.Clear();

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.mute = false;
            source.transform.SetParent(_root, false);
            source.transform.localPosition = Vector3.zero;
            source.gameObject.SetActive(false);
            _idleSources.Enqueue(source);
        }

        /// <summary>
        /// 按音频类型对 AudioSource 应用当前全局音量与静音设置。
        /// </summary>
        /// <param name="source">目标 AudioSource。</param>
        /// <param name="type">音频类型。</param>
        private void ApplyTypeSettings(AudioSource source, AudioType type)
        {
            if (source == null) return;

            AudioData data = AudioDataManager.Instance.Data;
            switch (type)
            {
                case AudioType.Music:
                    source.mute = !data.musicEnabled;
                    source.volume = data.musicVolume;
                    break;

                case AudioType.Sound:
                default:
                    source.mute = !data.soundEnabled;
                    source.volume = data.soundVolume;
                    break;
            }
        }

        /// <summary>
        /// 设置变更事件处理：统一刷新所有活跃音频的音量与静音状态。
        /// </summary>
        /// <param name="data">最新音频设置数据。</param>
        private void OnAudioSettingsChanged(AudioData data)
        {
            RemoveDestroyedActiveSources();
            if (_activeAudios.Count == 0) return;

            foreach (KeyValuePair<AudioSource, ActiveAudioInfo> pair in _activeAudios)
            {
                if (pair.Key != null) ApplyTypeSettings(pair.Key, pair.Value.Type);
            }
        }

        /// <summary>
        /// 丢弃被外部父对象连带销毁的播放实例记录。
        /// </summary>
        private void RemoveDestroyedActiveSources()
        {
            if (_activeAudios.Count == 0) return;

            List<AudioSource> destroyedSources = null;
            foreach (KeyValuePair<AudioSource, ActiveAudioInfo> pair in _activeAudios)
            {
                if (pair.Key != null) continue;

                destroyedSources ??= new List<AudioSource>();
                destroyedSources.Add(pair.Key);
            }

            if (destroyedSources == null) return;

            for (int index = 0; index < destroyedSources.Count; index++)
                _activeAudios.Remove(destroyedSources[index]);
        }

        /// <summary>
        /// 确保池化音频的父节点存在（DontDestroyOnLoad）。
        /// </summary>
        private void EnsureRoot()
        {
            if (_root != null) return;

            GameObject rootObject = new GameObject("AudioManager");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
            _root = rootObject.transform;
        }
    }

    /// <summary>
    /// 活跃音频播放信息，由 AudioManager 统一持有。
    /// </summary>
    internal sealed class ActiveAudioInfo
    {
        // 音频类型（音乐/音效）
        public AudioType Type { get; }

        /// <summary>
        /// 构建活跃音频信息快照。
        /// </summary>
        /// <param name="type">音频类型。</param>
        public ActiveAudioInfo(AudioType type)
        {
            Type = type;
        }
    }
}
