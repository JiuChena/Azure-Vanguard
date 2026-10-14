using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace Core.Gear
{
    /// <summary>
    /// Addressable 资源管理器，通过 Lease/Scope 引用计数机制管理资源的异步加载与生命周期。
    /// </summary>
    public sealed class AddressableManager
    {
        private static readonly AddressableManager instance = new AddressableManager();
        public static AddressableManager Instance => instance;

        // 全局资源释放事件，ReferenceCount 归零时广播，参数为完整 resourceKey
        public static event Action<string> OnResourceReleased;

        // 已加载资源缓存：resourceKey → ResourceEntry
        private readonly Dictionary<string, ResourceEntry> resources = new Dictionary<string, ResourceEntry>(32);

        // 活动 Lease 表：leaseId → ResourceLease，用于释放时查找
        private readonly Dictionary<int, ResourceLease> activeLeases = new Dictionary<int, ResourceLease>(64);

        // Lease ID 自增计数器，接近 int.MaxValue 时回绕
        private int nextLeaseId = 1;

        private AddressableManager() { }

        #region 资源获取

        /// <summary>
        /// 异步获取资源 Lease。相同 key 共享一个底层加载条目，调用方通过释放 Lease 归还引用。
        /// 约定：本接口只用于全项目唯一的地址（面板/SO/视频/图标等）；同址多份的资源（如按标签区分的展示模型）
        /// 必须走 <see cref="AcquireAssetWithLabelsAsync{T}"/>——裸地址查撞名地址会静默取构建目录中的第一条。
        /// </summary>
        /// <param name="key">Addressable 资源 key</param>
        /// <param name="scope">可选的作用域，lease 自动注册到 scope 中</param>
        /// <returns>包含资源实例的租约，释放后资源引用计数递减</returns>
        public async Task<ResourceLease<T>> AcquireAssetAsync<T>(string key, ResourceScope scope = null) where T : Object
        {
            // 参数校验
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Addressable key 不能为空。", nameof(key));
            }

            // 构建资源ID、查询资源是否处于加载状态（正在加载则等待资源加载，否则自己发起加载）、等待并返回加载出的资源
            string resourceKey = BuildResourceKey<T>(key);
            ResourceEntry entry = await GetOrLoadEntryAsync<T>(resourceKey, key);
            
            //查询资源状态
            if (!entry.IsLoaded || entry.Asset == null)
            {
                throw new InvalidOperationException($"Addressable 资源加载失败：{key}({typeof(T).Name})");
            }

            return CreateLease<T>(entry, scope);
        } 

        /// <summary>
        /// 异步获取常驻资源 Lease，适用于 UI 根节点等长期驻留资源，不会随 Scope 释放。
        /// </summary>
        /// <param name="key">Addressable 资源 key</param>
        public Task<ResourceLease<T>> AcquirePersistentAssetAsync<T>(string key) where T : Object
        {
            return AcquireAssetAsync<T>(key, ResourceScope.Persistent);
        }

        /// <summary>
        /// 按标签批量异步获取资源 Lease（多标签并集：挂任一标签的资源都会命中）。
        /// 返回一组 Lease，全部注册进 scope，可整批随 Scope 释放，也可单个提前释放。
        /// </summary>
        /// <param name="labels">标签列表，不可为空</param>
        /// <param name="scope">可选的作用域，整批 Lease 自动注册到 scope 中</param>
        public Task<List<ResourceLease<T>>> AcquireAssetsByLabelsAsync<T>(IList<string> labels, ResourceScope scope = null) where T : Object
        {
            return AcquireAssetsByKeysCoreAsync<T>(ToObjectKeys(labels), Addressables.MergeMode.Union, scope);
        }

        /// <summary>
        /// 按标签批量异步获取资源 Lease（多标签交集：必须同时挂全部标签才命中）。
        /// 返回一组 Lease，全部注册进 scope，可整批随 Scope 释放，也可单个提前释放。
        /// </summary>
        /// <param name="labels">标签列表，不可为空</param>
        /// <param name="scope">可选的作用域，整批 Lease 自动注册到 scope 中</param>
        public Task<List<ResourceLease<T>>> AcquireAssetsByLabelsIntersectAsync<T>(IList<string> labels, ResourceScope scope = null) where T : Object
        {
            return AcquireAssetsByKeysCoreAsync<T>(ToObjectKeys(labels), Addressables.MergeMode.Intersection, scope);
        }

        /// <summary>
        /// 按资源地址 + 标签指定异步获取单个资源 Lease：地址命中的资源必须同时挂有全部指定标签，
        /// 否则解析不出资源位置并抛异常。标签列表为空时等价于 <see cref="AcquireAssetAsync{T}"/>。
        /// 命中结果必须唯一：>1 条 = 标签写错或组标签不规范，加载前直接抛异常。
        /// </summary>
        /// <param name="key">Addressable 资源地址 key</param>
        /// <param name="requiredLabels">地址资源必须携带的标签列表</param>
        /// <param name="scope">可选的作用域，Lease 自动注册到 scope 中</param>
        public async Task<ResourceLease<T>> AcquireAssetWithLabelsAsync<T>(string key, IList<string> requiredLabels, ResourceScope scope = null) where T : Object
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Addressable key 不能为空。", nameof(key));
            }

            // 未指定标签约束时退化为普通按地址加载
            if (requiredLabels == null || requiredLabels.Count == 0)
            {
                return await AcquireAssetAsync<T>(key, scope);
            }

            // 地址 + 全部标签按交集解析：命中空列表即“资源不带指定标签”
            List<object> keys = new List<object>(requiredLabels.Count + 1) { key };
            for (int i = 0; i < requiredLabels.Count; i++) keys.Add(requiredLabels[i]);
            List<ResourceLease<T>> leases = await AcquireAssetsByKeysCoreAsync<T>(keys, Addressables.MergeMode.Intersection, scope, requireSingleResult: true);
            return leases[0];
        }

        /// <summary>
        /// 仅在资源已加载完成时同步获取，不会触发新的加载。
        /// </summary>
        /// <param name="key">Addressable 资源 key</param>
        /// <param name="asset">输出的资源实例</param>
        /// <returns>资源已缓存且加载完成时返回 true</returns>
        public bool TryGetLoadedAsset<T>(string key, out T asset) where T : Object
        {
            string resourceKey = BuildResourceKey<T>(key);
            if (resources.TryGetValue(resourceKey, out ResourceEntry entry) &&
                entry.IsLoaded &&
                entry.Asset is T typedAsset)
            {
                asset = typedAsset;
                return true;
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// 查询资源是否已在缓存中且加载完成。
        /// </summary>
        public bool IsAssetLoaded<T>(string key) where T : Object
        {
            return TryGetEntry<T>(key, out ResourceEntry entry) && entry.IsLoaded && entry.Asset != null;
        }

        /// <summary>
        /// 查询资源的异步加载状态，未缓存时返回 None。
        /// </summary>
        public AsyncOperationStatus GetResourceStatus<T>(string key) where T : Object
        {
            return TryGetEntry<T>(key, out ResourceEntry entry) ? entry.Handle.Status : AsyncOperationStatus.None;
        }

        /// <summary>
        /// 获取资源的调试信息快照，仅用于诊断。
        /// </summary>
        public bool TryGetResourceDebugInfo<T>(string key, out ResourceDebugInfo debugInfo) where T : Object
        {
            if (!TryGetEntry<T>(key, out ResourceEntry entry))
            {
                debugInfo = default;
                return false;
            }

            debugInfo = new ResourceDebugInfo(
                entry.Key,
                entry.Asset,
                entry.ReferenceCount,
                entry.Handle.Status,
                entry.LoadFailed);
            return true;
        }

        /// <summary>
        /// 按完整资源键（lease.ResourceKey，已含类型前缀）精确查询调试信息快照：一次哈希，
        /// 供扫漏等诊断场景使用（带标签加载的条目按 InternalId 入账，须用加载时的同源键查询）。
        /// </summary>
        public bool TryGetDebugInfoByResourceKey(string resourceKey, out ResourceDebugInfo debugInfo)
        {
            if (!string.IsNullOrEmpty(resourceKey) && resources.TryGetValue(resourceKey, out ResourceEntry entry))
            {
                debugInfo = new ResourceDebugInfo(
                    entry.Key,
                    entry.Asset,
                    entry.ReferenceCount,
                    entry.Handle.Status,
                    entry.LoadFailed);
                return true;
            }

            debugInfo = default;
            return false;
        }

        #endregion

        #region 资源释放与清理

        /// <summary>
        /// 释放所有引用计数已归零但仍残留在缓存中的资源条目，作为兜底清理使用。
        /// </summary>
        public void ReleaseUnusedResources()
        {
            if (resources.Count == 0) return;

            // 收集所有引用计数归零的条目
            List<string> pendingRemoval = null;
            foreach (KeyValuePair<string, ResourceEntry> pair in resources)
            {
                ResourceEntry entry = pair.Value;
                if (entry.ReferenceCount > 0) continue;

                entry.ReleaseHandle();
                pendingRemoval ??= new List<string>(4);
                pendingRemoval.Add(pair.Key);
            }

            if (pendingRemoval == null) return;

            // 从缓存中移除
            for (int i = 0; i < pendingRemoval.Count; i++) resources.Remove(pendingRemoval[i]);
        }

        /// <summary>
        /// 强制释放所有缓存资源和活动 Lease。仅限关机、域清理或测试复位使用。
        /// </summary>
        public void ForceReleaseAllResourcesForShutdown()
        {
            // 静默强制释放所有 Lease（不触发引用计数变更）
            if (activeLeases.Count > 0)
            {
                List<ResourceLease> leases = new List<ResourceLease>(activeLeases.Values);
                for (int i = 0; i < leases.Count; i++) leases[i].ForceDisposeSilently();

                activeLeases.Clear();
            }

            // 释放所有底层资源 Handle
            foreach (KeyValuePair<string, ResourceEntry> pair in resources) pair.Value.ReleaseHandle();

            resources.Clear();
        }

        /// <summary>
        /// 释放指定 Lease 对应的资源引用。引用计数减 1，归零时释放底层 Handle 并广播释放事件。
        /// </summary>
        internal void ReleaseLease(int leaseId, string resourceKey)
        {
            // 从活动 Lease 表中移除
            if (!activeLeases.Remove(leaseId, out ResourceLease lease)) return;
            if (!resources.TryGetValue(resourceKey, out ResourceEntry entry)) return;

            // 递减引用计数
            entry.ReferenceCount -= 1;
            if (entry.ReferenceCount < 0)
            {
                entry.ReferenceCount = 0;
                Debug.LogError($"AddressableManager 检测到重复释放：{resourceKey}");
            }

            // 引用计数归零：释放底层 Handle 并广播事件
            if (entry.ReferenceCount == 0)
            {
                entry.ReleaseHandle();
                resources.Remove(resourceKey);
                OnResourceReleased?.Invoke(resourceKey);
            }
        }

        #endregion

        #region Private
        
        /// <summary>
        /// 为已加载条目创建 Lease：引用计数 +1，登记活动 Lease 表并注册进作用域。
        /// </summary>
        private ResourceLease<T> CreateLease<T>(ResourceEntry entry, ResourceScope scope) where T : Object
        {
            // 分配 Lease ID（接近上限时回绕）
            int leaseId = nextLeaseId++;
            if (leaseId == int.MaxValue)
            {
                nextLeaseId = 1;
                leaseId = nextLeaseId++;
            }

            // 增加引用计数，创建 Lease 并注册
            entry.ReferenceCount += 1;
            ResourceLease<T> lease = new ResourceLease<T>(this, leaseId, entry.Key, entry.Asset as T, scope);
            activeLeases.Add(leaseId, lease);
            scope?.Register(lease);
            return lease;
        }

        /// <summary>
        /// 标签/多 key 批量获取共用核心：按合并模式解析确切资源位置（IResourceLocation），
        /// 逐条按位置异步加载并使用唯一 InternalId 构建缓存，统一出 Lease 列表。
        /// 任一条资源加载失败即抛异常（此时尚未创建 Lease，不产生半批引用）。
        /// </summary>
        /// <param name="requireSingleResult">单资产语义（按地址+标签唯一定位）时为 true：
        /// 命中多条即标签写错或组标签不规范，加载前直接抛异常。</param>
        private async Task<List<ResourceLease<T>>> AcquireAssetsByKeysCoreAsync<T>(
            List<object> keys, Addressables.MergeMode mergeMode, ResourceScope scope, bool requireSingleResult = false) where T : Object
        {
            // 解析多 key 命中的资源位置列表（IResourceLocation 包含具体资源路径与元数据）
            List<IResourceLocation> locations = await ResolveLocationsAsync<T>(keys, mergeMode);

            //单资产定位应唯一命中：>1 条说明数据配错（标签写错或组标签不规范）——加载前报错，
            //不产生多余加载也不吞多余 lease；报错信息带全量上下文（key/标签/逐条精确路径与依赖 bundle），便于定位数据问题
            if (requireSingleResult && locations.Count > 1)
            {
                List<string> labels = new List<string>(keys.Count - 1);
                for (int i = 1; i < keys.Count; i++) labels.Add(keys[i].ToString());
                List<string> detailLines = new List<string>(locations.Count);
                for (int i = 0; i < locations.Count; i++)
                {
                    IResourceLocation location = locations[i];
                    List<string> bundleIds = new List<string>();
                    if (location.Dependencies != null)
                    {
                        for (int j = 0; j < location.Dependencies.Count; j++)
                            bundleIds.Add(location.Dependencies[j].InternalId);
                    }
                    detailLines.Add($"PrimaryKey={location.PrimaryKey}，InternalId={location.InternalId}，依赖bundle=[{string.Join(" | ", bundleIds)}]");
                }
                throw new InvalidOperationException(
                    $"[AddressableManager] 按地址+标签加载应唯一命中 1 条，实际命中 {locations.Count} 条（标签写错或组标签不规范）。\n" +
                    $"加载请求：地址 \"{keys[0]}\"，标签 [{string.Join(", ", labels)}]，类型 {typeof(T).Name}。\n" +
                    $"命中明细：\n  {string.Join("\n  ", detailLines)}");
            }

            // 第一遍：全部加载成缓存条目；全部成功后才进入建 Lease 阶段
            List<ResourceEntry> entries = new List<ResourceEntry>(locations.Count);
            for (int i = 0; i < locations.Count; i++)
            {
                IResourceLocation location = locations[i];
                // 优先以 InternalId 作为唯一资源键，防止同名 PrimaryKey（如 CH0221）在不同标签下发生缓存键碰撞
                string uniqueKey = !string.IsNullOrEmpty(location.InternalId) ? location.InternalId : location.PrimaryKey;
                string resourceKey = BuildResourceKey<T>(uniqueKey);
                ResourceEntry entry = await GetOrLoadEntryByLocationAsync<T>(resourceKey, location);
                if (!entry.IsLoaded || entry.Asset == null)
                {
                    throw new InvalidOperationException($"Addressable 资源加载失败：{location.PrimaryKey}({typeof(T).Name})");
                }
                entries.Add(entry);
            }

            // 第二遍：统一建 Lease（引用计数 +1 并注册进 scope）
            List<ResourceLease<T>> leases = new List<ResourceLease<T>>(entries.Count);
            for (int i = 0; i < entries.Count; i++) leases.Add(CreateLease<T>(entries[i], scope));
            return leases;
        }

        /// <summary>
        /// 按多 key + 合并模式解析资源确切位置列表。
        /// location 列表只是临时解析结果，Handle 用完即释放，不驻留底层缓存。
        /// </summary>
        private static async Task<List<IResourceLocation>> ResolveLocationsAsync<T>(List<object> keys, Addressables.MergeMode mergeMode) where T : Object
        {
            // keys 转 IEnumerable 绑定现行重载（IList<object> 重载已标 Obsolete）
            AsyncOperationHandle<IList<IResourceLocation>> handle =
                Addressables.LoadResourceLocationsAsync((IEnumerable)keys, mergeMode, typeof(T));
            try
            {
                IList<IResourceLocation> locations = await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded || locations == null || locations.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Addressable 资源位置解析失败：[{string.Join(", ", keys)}]({typeof(T).Name}) 未命中任何资源位置。");
                }

                return new List<IResourceLocation>(locations);
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        /// <summary>
        /// 校验并转换 key 列表为 object 列表（Addressables 多 key 接口入参）。
        /// </summary>
        private static List<object> ToObjectKeys(IList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                throw new ArgumentException("Addressable key 列表不能为空。", nameof(keys));
            }

            List<object> objectKeys = new List<object>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(keys[i]))
                {
                    throw new ArgumentException($"Addressable key 列表第 {i} 项为空。", nameof(keys));
                }
                objectKeys.Add(keys[i]);
            }
            return objectKeys;
        }

        /// <summary>
        /// 按 key + 类型查找已缓存的资源条目：一次哈希精确匹配，不做任何模糊兜底。
        /// 查询键必须与加载键同模式（裸地址加载查裸地址；带标签加载的条目用 lease.ResourceKey 查）。
        /// </summary>
        private bool TryGetEntry<T>(string key, out ResourceEntry entry) where T : Object
        {
            return resources.TryGetValue(BuildResourceKey<T>(key), out entry);
        }

        /// <summary>
        /// 按确切资源位置（IResourceLocation）获取或异步加载资源条目。若已有缓存则等待其加载完成；否则发起新加载。
        /// </summary>
        /// <param name="resourceKey">完整 resourceKey（含类型前缀与唯一标识）</param>
        /// <param name="location">解析后的确切 ResourceLocation</param>
        private async Task<ResourceEntry> GetOrLoadEntryByLocationAsync<T>(string resourceKey, IResourceLocation location) where T : Object
        {
            // 已有缓存：等待其 Task 完成即返回
            if (resources.TryGetValue(resourceKey, out ResourceEntry existingEntry))
            {
                await existingEntry.Task;
                return existingEntry;
            }

            // 无缓存：按确切位置发起新的 Addressables 异步加载，确保标签与实际资源严格对应
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(location);
            ResourceEntry entry = new ResourceEntry(resourceKey, handle);
            resources.Add(resourceKey, entry);

            // 等待加载完成，失败则清理
            await entry.Task;
            if (!entry.IsLoaded)
            {
                entry.ReleaseHandle();
                resources.Remove(resourceKey);
            }

            return entry;
        }

        /// <summary>
        /// 获取或异步加载资源条目。若已有缓存则等待其加载完成；否则发起新加载。
        /// </summary>
        /// <param name="resourceKey">完整 resourceKey（含类型前缀）</param>
        /// <param name="addressableKey">Addressable 原始 key</param>
        private async Task<ResourceEntry> GetOrLoadEntryAsync<T>(string resourceKey, string addressableKey) where T : Object
        {
            // 已有缓存：等待其 Task 完成即返回
            if (resources.TryGetValue(resourceKey, out ResourceEntry existingEntry))
            {
                await existingEntry.Task;
                return existingEntry;
            }

            // 无缓存：发起新的 Addressables 异步加载
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(addressableKey);
            ResourceEntry entry = new ResourceEntry(resourceKey, handle);
            resources.Add(resourceKey, entry);

            // 等待加载完成，失败则清理
            await entry.Task;
            if (!entry.IsLoaded)
            {
                entry.ReleaseHandle();
                resources.Remove(resourceKey);
            }

            return entry;
        }

        /// <summary>
        /// 构建完整 resourceKey（FullTypeName::key），确保不同类型同名 key 不冲突。
        /// </summary>
        private static string BuildResourceKey<T>(string key) where T : Object
        {
            return typeof(T).FullName + "::" + key;
        }

        #endregion
    }
}
