using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 双列表序列化字典：Inspector 以两条平行列表（键/值）配置，运行时懒构建哈希查询。
    /// 用于 Unity 原生 Dictionary 无法序列化的场景，键类型需可序列化（string/枚举/数值）。
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue>
    {
        // 平行键列表，与 values 按下标一一对应。
        [SerializeField] private List<TKey> keys = new List<TKey>();
        // 平行值列表，与 keys 按下标一一对应。
        [SerializeField] private List<TValue> values = new List<TValue>();
        // 运行时查询缓存，首次访问或脏标记置位时重建。
        [NonSerialized] private Dictionary<TKey, TValue> lookup;
        // 查询缓存是否需要重建。
        [NonSerialized] private bool lookupDirty = true;

        /// <summary>获取当前条目数量（以键列表为准）。</summary>
        public int Count => keys.Count;

        /// <summary>
        /// 运行时写入键值对：键已存在时覆盖，否则追加；会标记查询缓存待重建。
        /// </summary>
        /// <param name="key">写入键；不允许为 null。</param>
        /// <param name="value">写入值。</param>
        public void Set(TKey key, TValue value)
        {
            if (EqualityComparer<TKey>.Default.Equals(key, default(TKey))) throw new ArgumentNullException(nameof(key));

            // 先写平行列表保持其为唯一事实来源，再标记缓存重建。
            int index = keys.IndexOf(key);
            if (index >= 0) values[index] = value;
            else
            {
                keys.Add(key);
                values.Add(value);
            }
            lookupDirty = true;
        }

        /// <summary>
        /// 按键查询值。
        /// </summary>
        /// <param name="key">查询键。</param>
        /// <param name="value">命中时返回对应值，未命中返回默认值。</param>
        /// <returns>键存在时返回 true。</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            EnsureLookup();
            return lookup.TryGetValue(key, out value);
        }

        /// <summary>
        /// 按键查询值；未命中时返回类型默认值。
        /// </summary>
        /// <param name="key">查询键。</param>
        /// <returns>命中键的值或类型默认值。</returns>
        public TValue GetOrDefault(TKey key)
        {
            return TryGetValue(key, out TValue value) ? value : default;
        }

        /// <summary>
        /// 判断键是否存在。
        /// </summary>
        /// <param name="key">查询键。</param>
        /// <returns>键存在时返回 true。</returns>
        public bool ContainsKey(TKey key)
        {
            EnsureLookup();
            return lookup.ContainsKey(key);
        }

        /// <summary>
        /// 确保查询缓存可用：首次访问或列表变更后重建；键值数量不一致时记录错误并按较短一侧构建。
        /// </summary>
        private void EnsureLookup()
        {
            if (!lookupDirty && lookup != null) return;

            // 序列化数据可能被外部改动导致两列长度不齐，重建前先校验。
            if (keys.Count != values.Count)
                Debug.LogError($"[SerializableDictionary] 键列表({keys.Count})与值列表({values.Count})数量不一致，请检查 Inspector 配置。");

            lookup = new Dictionary<TKey, TValue>(Math.Min(keys.Count, values.Count));
            for (int index = 0; index < keys.Count && index < values.Count; index++)
            {
                // 重复键按后者覆盖，保持与 Dictionary 语义一致。
                if (lookup.ContainsKey(keys[index]))
                    Debug.LogWarning($"[SerializableDictionary] 检测到重复键 {keys[index]}，后者覆盖前者，请检查 Inspector 配置。");
                lookup[keys[index]] = values[index];
            }
            lookupDirty = false;
        }
    }
}
