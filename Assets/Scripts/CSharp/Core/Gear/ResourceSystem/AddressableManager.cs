using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

            // 分配 Lease ID（接近上限时回绕）
            int leaseId = nextLeaseId++;
            if (leaseId == int.MaxValue)
            {
                nextLeaseId = 1;
                leaseId = nextLeaseId++;
            }

            // 增加引用计数，创建 Lease 并注册
            entry.ReferenceCount += 1;
            ResourceLease<T> lease = new ResourceLease<T>(this, leaseId, resourceKey, entry.Asset as T, scope);
            activeLeases.Add(leaseId, lease);
            scope?.Register(lease);
            return lease;
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
        /// 按 key + 类型查找已缓存的资源条目。
        /// </summary>
        private bool TryGetEntry<T>(string key, out ResourceEntry entry) where T : Object
        {
            return resources.TryGetValue(BuildResourceKey<T>(key), out entry);
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
