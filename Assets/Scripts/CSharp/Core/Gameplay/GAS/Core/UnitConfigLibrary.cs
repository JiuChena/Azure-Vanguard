using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 单位配置运行时库：按单位 ID（= Addressables 地址 = SO.unitId）加载 <see cref="UnitConfig"/> 资产。
    /// 双口径：不传作用域常驻不释放（与 AddressableTableSource 同口径，战斗等全期消费方）；
    /// 传入 <see cref="ResourceScope"/> 则每次调用登记独立租约、随作用域统一释放（面板类调用方传 PanelBase.ResourceScope），不走静态缓存。
    /// 常驻口径缓存命中直接短路；并发加载经 pending 表去重；加载失败的 id 不入缓存，下次自动重试。
    /// </summary>
    public static class UnitConfigLibrary
    {
        // unitId → 已加载配置（失败不缓存）
        private static readonly Dictionary<string, UnitConfig> cache = new Dictionary<string, UnitConfig>();

        // unitId → 进行中的加载任务（并发去重）
        private static readonly Dictionary<string, Task<UnitConfig>> pending = new Dictionary<string, Task<UnitConfig>>();

        /// <summary>
        /// 获取单位配置；缓存命中直接返回，否则从 Addressables 加载（地址 = unitId）。
        /// 传入作用域时走作用域租约（不进静态缓存，随作用域释放）。
        /// </summary>
        /// <param name="unitId">单位 ID（如 CH0221）。</param>
        /// <param name="scope">可选资源作用域：传入则租约随作用域统一释放（面板资源口径），不传则常驻缓存。</param>
        /// <returns>单位配置；id 非法或加载失败返回 null（失败详情由 AddressableManager 报错定位）。</returns>
        public static async Task<UnitConfig> GetAsync(string unitId, ResourceScope scope = null)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                Debug.LogError($"[UnitConfigLibrary] 获取失败：unitId 为空");
                return null;
            }

            //作用域口径：每调用一租约，随作用域统一释放（AddressableManager 按 key 去重并发与共享计数）
            if (scope != null)
            {
                if (scope.IsDisposed) return null;
                try
                {
                    ResourceLease<CharacterUnitConfig> lease = await AddressableManager.Instance
                        .AcquireAssetAsync<CharacterUnitConfig>(unitId, scope);
                    //等待加载期间作用域已被释放（关板竞态）：租约未登记进作用域，手动归还防计数悬挂
                    if (scope.IsDisposed)
                    {
                        lease?.Dispose();
                        return null;
                    }
                    return lease?.Asset;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[UnitConfigLibrary] 配置加载失败：{unitId}（{e.Message}）");
                    return null;
                }
            }

            if (cache.TryGetValue(unitId, out UnitConfig hit)) return hit;
            if (pending.TryGetValue(unitId, out Task<UnitConfig> inflight)) return await inflight;

            Task<UnitConfig> task = LoadAsync(unitId);
            pending[unitId] = task;
            UnitConfig config = await task;
            pending.Remove(unitId);

            if (config != null) cache[unitId] = config;
            return config;
        }

        /// <summary>
        /// 仅查缓存，不触发加载；UI 即时展示等同步场景用，未命中走 GetAsync 兜底。
        /// </summary>
        /// <param name="unitId">单位 ID。</param>
        /// <param name="config">已缓存的配置；未命中为 null。</param>
        /// <returns>命中返回 true。</returns>
        public static bool TryGetCached(string unitId, out UnitConfig config)
        {
            return cache.TryGetValue(unitId, out config) && config != null;
        }

        /// <summary>批量预热：逐个走 GetAsync，命中/失败都继续，适合战斗入场前的整队预载。</summary>
        /// <param name="unitIds">单位 ID 列表。</param>
        public static async Task PreloadAsync(IReadOnlyList<string> unitIds)
        {
            for (int i = 0; i < unitIds.Count; i++)
            {
                await GetAsync(unitIds[i]);
            }
        }

        /// <summary>加载单个配置；CharacterUnitConfig 为现有唯一子类，按子类型加载（缓存键仍为基类口径）。</summary>
        private static async Task<UnitConfig> LoadAsync(string unitId)
        {
            ResourceLease<CharacterUnitConfig> lease = await AddressableManager.Instance
                .AcquirePersistentAssetAsync<CharacterUnitConfig>(unitId);
            return lease?.Asset;
        }
    }
}
