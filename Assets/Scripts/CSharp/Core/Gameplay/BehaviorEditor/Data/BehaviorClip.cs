using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// 单个行为的多态运行时轨道数据容器。
    /// </summary>
    [MovedFrom("BehaviorCore")]
    [CreateAssetMenu(fileName = "BehaviorClip", menuName = "Framework/BehaviorEditor/Authoring/Behavior Clip")]
    public sealed class BehaviorClip : ScriptableObject
    {
        // 轨道数据类型 → 当前资产对应实例的运行时查询缓存。
        [NonSerialized]
        private readonly Dictionary<Type, BehaviorTrackData> trackDataLookup = new Dictionary<Type, BehaviorTrackData>();

        // 多态轨道数据查询缓存是否需要重建。
        [NonSerialized]
        private bool trackDataLookupDirty = true;

        [Header("轨道数据")]
        [Tooltip("Timeline 编译后的多态轨道数据。新增独立轨道无需修改 BehaviorClip 字段。")]
        [SerializeReference]
        public List<BehaviorTrackData> trackData = new List<BehaviorTrackData>();

        [Header("播放配置")]
        [Tooltip("行为播放头使用的全局时长、包裹模式和速度配置。")]
        public BehaviorPlaybackSettings playbackSettings = new BehaviorPlaybackSettings();

        /// <summary>
        /// 获取指定类型的运行时轨道数据。
        /// </summary>
        /// <typeparam name="TTrackData">需要获取的轨道数据类型。</typeparam>
        /// <returns>找到时返回对应数据；不存在时返回 null。</returns>
        public TTrackData GetTrackData<TTrackData>() where TTrackData : BehaviorTrackData
        {
            EnsureTrackDataLookup();
            return trackDataLookup.TryGetValue(typeof(TTrackData), out BehaviorTrackData data)
                ? data as TTrackData
                : null;
        }

        /// <summary>
        /// 用指定轨道数据替换同类型旧数据，供 Timeline 编译器提交导出结果。
        /// </summary>
        /// <param name="data">需要写入的轨道数据；传入 null 时不执行修改。</param>
        public void SetTrackData(BehaviorTrackData data)
        {
            if (data == null)
                return;

            // 删除同类型旧数据，确保每种运行时数据只有一个权威出口。
            trackData ??= new List<BehaviorTrackData>();
            for (int i = trackData.Count - 1; i >= 0; i--)
            {
                BehaviorTrackData existing = trackData[i];
                if (existing == null || existing.GetType() == data.GetType())
                    trackData.RemoveAt(i);
            }

            trackData.Add(data);
            trackDataLookupDirty = true;
        }

        /// <summary>
        /// 用一次完整导出的轨道集合替换当前全部运行时轨道数据。
        /// </summary>
        /// <param name="dataCollection">本次导出的轨道数据集合；为空时写入空集合。</param>
        public void ReplaceTrackData(IEnumerable<BehaviorTrackData> dataCollection)
        {
            // 重建唯一轨道集合，跳过空引用和重复类型。
            trackData ??= new List<BehaviorTrackData>();
            trackData.Clear();
            if (dataCollection != null)
            {
                foreach (BehaviorTrackData data in dataCollection)
                {
                    if (data == null || GetTrackDataIndex(data.GetType()) >= 0)
                        continue;

                    trackData.Add(data);
                }
            }

            // 让后续查询从本次导出的权威数据重建索引。
            trackDataLookupDirty = true;
        }

        /// <summary>
        /// 校验行为是否具备有效的播放配置和轨道条目。
        /// </summary>
        /// <param name="logWarnings">是否将发现的问题输出到 Unity Console。</param>
        /// <returns>不存在校验问题时返回 true。</returns>
        public bool ValidateData(bool logWarnings = true)
        {
            List<string> issues = new List<string>();
            CollectValidationIssues(issues);
            if (logWarnings)
            {
                for (int i = 0; i < issues.Count; i++)
                    Debug.LogWarning($"[{name}] {issues[i]}", this);
            }

            return issues.Count == 0;
        }

        /// <summary>
        /// 将行为数据的校验问题追加到指定列表。
        /// </summary>
        /// <param name="issues">接收校验问题的列表；传入 null 时不执行校验。</param>
        /// <returns>本次追加的问题数量。</returns>
        public int CollectValidationIssues(List<string> issues)
        {
            if (issues == null)
                return 0;

            int initialCount = issues.Count;
            EnsureTrackDataLookup();
            if (playbackSettings == null)
                issues.Add("缺少 BehaviorPlaybackSettings，无法确定行为时长、速度和包裹模式。");
            else if (playbackSettings.duration <= 0f)
                issues.Add("BehaviorPlaybackSettings.duration 必须大于 0。");

            if (trackData == null || trackData.Count == 0)
                issues.Add("trackData 为空，行为没有任何可执行轨道。");
            else
            {
                for (int i = 0; i < trackData.Count; i++)
                {
                    if (trackData[i] == null)
                        issues.Add($"trackData[{i}] 为空引用。");
                }
            }

            return issues.Count - initialCount;
        }

        /// <summary>
        /// 资源载入后使多态轨道查询缓存失效。
        /// </summary>
        private void OnEnable()
        {
            trackDataLookupDirty = true;
        }

        /// <summary>
        /// Inspector 修改后使多态轨道查询缓存失效并输出校验结果。
        /// </summary>
        private void OnValidate()
        {
            trackDataLookupDirty = true;
            ValidateData();
        }

        /// <summary>
        /// 重建多态轨道数据查询缓存，忽略空引用和重复类型的后续数据。
        /// </summary>
        private void EnsureTrackDataLookup()
        {
            if (!trackDataLookupDirty)
                return;

            // 以资产序列化顺序建立类型索引，首个同类型数据保持权威。
            trackDataLookup.Clear();
            if (trackData != null)
            {
                for (int i = 0; i < trackData.Count; i++)
                {
                    BehaviorTrackData data = trackData[i];
                    if (data == null || trackDataLookup.ContainsKey(data.GetType()))
                        continue;

                    trackDataLookup.Add(data.GetType(), data);
                }
            }

            trackDataLookupDirty = false;
        }

        /// <summary>
        /// 查找指定运行时轨道类型在当前集合中的索引。
        /// </summary>
        /// <param name="trackDataType">需要匹配的精确轨道数据类型。</param>
        /// <returns>找到时返回索引；未找到时返回 -1。</returns>
        private int GetTrackDataIndex(Type trackDataType)
        {
            for (int i = 0; i < trackData.Count; i++)
            {
                BehaviorTrackData data = trackData[i];
                if (data != null && data.GetType() == trackDataType)
                    return i;
            }

            return -1;
        }
    }
}
