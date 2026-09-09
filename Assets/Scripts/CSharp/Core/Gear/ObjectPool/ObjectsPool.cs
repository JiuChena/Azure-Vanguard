using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 对象池模块。缓存已实例化的 GameObject 避免重复 Instantiate/Destroy，支持基于资源依赖的自动清理。
    /// </summary>
    public class ObjectsPool : MonoBehaviour
    {
        private static ObjectsPool instance;
        private static readonly object _lock = new();

        public static ObjectsPool Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (_lock)
                    {
                        if(instance != null) return instance;
                        
                        GameObject obj = new GameObject("ObjectsPool");
                        instance = obj.AddComponent<ObjectsPool>();
                        DontDestroyOnLoad(obj);
                    }
                }

                return instance;
            }
        }

        // 缓存池本体：poolKey → 待复用的 GameObject 实例队列
        private readonly Dictionary<string, Queue<GameObject>> objectPool = new Dictionary<string, Queue<GameObject>>();

        // Addressable 预制体资源租约：poolKey → ResourceLease，池自行加载时持有，Clear 时释放
        private readonly Dictionary<string, ResourceLease<GameObject>> prefabLeases = new Dictionary<string, ResourceLease<GameObject>>();

        // 资源依赖映射：poolKey → 依赖的 Addressable 资源 key 集合，一个池可绑定多个资源依赖
        private readonly Dictionary<string, HashSet<string>> dependencies = new Dictionary<string, HashSet<string>>();

        // 延迟回池缓冲队列：存放等待延迟时间到期的回池对象
        private readonly Queue<BufferReturnObjectInfo> bufferQueue = new Queue<BufferReturnObjectInfo>();

        private void Awake()
        {
            // 单例防重：若已存在同类型实例则销毁自身
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // 订阅全局资源释放事件，依赖资源释放时自动清池
            AddressableManager.OnResourceReleased -= OnDependencyReleased;
            AddressableManager.OnResourceReleased += OnDependencyReleased;
        }

        private void OnDestroy()
        {
            // 取消事件订阅，防止静态事件持有已销毁实例的引用
            AddressableManager.OnResourceReleased -= OnDependencyReleased;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            // 缓冲队列为空时跳过，避免每帧无效轮询
            if (bufferQueue.Count == 0) return;

            // 逐帧倒计时处理延迟回池队列：到期则正常回池，未到期则重新入队等待
            int count = bufferQueue.Count;
            for (int i = 0; i < count; i++)
            {
                BufferReturnObjectInfo temp = bufferQueue.Dequeue();
                temp.delayTime -= Time.deltaTime;
                if (temp.delayTime <= 0f) Put(temp.obj);
                else bufferQueue.Enqueue(temp);
            }
        }

        #region Get

        /// <summary>
        /// 从对象池获取对象（Addressable 加载路径）。
        /// 池自行通过 Addressables 加载预制体，自身即依赖。
        /// </summary>
        public async void Get(string addressableKey, Transform parent = null, UnityAction<GameObject> callback = null)
        {
            // 尝试从相应缓存池中取出可用实例并激活
            if (TryDequeuePooledObject(addressableKey, parent, callback, out GameObject pooledObject))
            {
                pooledObject.SetActive(true);
                return;
            }

            // 池中无可用实例，通过 Addressables 异步加载预制体并绑定资源依赖
            ResourceLease<GameObject> prefabLease = await GetOrAcquirePrefabLeaseAsync(addressableKey);
            if (prefabLease == null || prefabLease.Asset == null)
            {
                Debug.LogError($"对象池加载失败：{addressableKey}");
                return;
            }

            // 实例化预制体，设置父节点并执行回调
            InstantiateAndDeliver(prefabLease.Asset, addressableKey, parent, callback);
        }

        /// <summary>
        /// 从对象池获取对象（prefab + 资源依赖路径）。
        /// 将池的生命周期绑定到 dependencyKey 对应的 Addressable 资源上，
        /// 该资源释放时自动清理此池的所有缓存实例。
        /// </summary>
        public GameObject Get(GameObject prefab, string dependencyKey, Transform parent = null, UnityAction<GameObject> callback = null)
        {
            if (prefab == null) return null;

            string nameKey = prefab.name;

            // 注册资源依赖：将池 key 与 Addressable 资源 key 绑定，资源释放时自动清池
            if (!string.IsNullOrWhiteSpace(dependencyKey)) RegisterDependency(nameKey, dependencyKey);

            // 从缓存池取或即时实例化新对象
            return GetInternal(prefab, nameKey, parent, callback);
        }

        /// <summary>
        /// 从对象池获取对象（prefab 无依赖路径）。
        /// 适用于永久性资源（伤害跳字、UI 通用控件等），不会随资源释放自动清理。
        /// </summary>
        public GameObject Get(GameObject prefab, Transform parent = null, UnityAction<GameObject> callback = null)
        {
            // 委托到依赖版 Get，传入 null dependencyKey 表示无依赖
            return Get(prefab, null, parent, callback);
        }

        #endregion

        #region Put

        /// <summary>
        /// 将对象返还对象池。对象会被挂载到池根节点并置为非激活状态。
        /// </summary>
        public void Put(GameObject obj, UnityAction<GameObject> callback = null)
        {
            if (obj == null) return;

            // 回池前执行回调（通常用于重置组件状态）
            callback?.Invoke(obj);

            // 按对象名查找缓存队列，不存在则创建
            if (!objectPool.TryGetValue(obj.name, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                objectPool.Add(obj.name, queue);
            }

            // 对象入队，挂到池根节点下并禁用
            queue.Enqueue(obj);
            obj.transform.SetParent(transform, true);
            obj.SetActive(false);
        }

        /// <summary>
        /// 延迟返还对象到对象池，delayTime 秒后自动回池。
        /// </summary>
        public void TimerPut(GameObject obj, float delayTime)
        {
            if (obj == null) return;

            if (delayTime <= 0f)
            {
                Put(obj);
                return;
            }

            // 加入延迟缓冲队列，由 Update 逐帧倒计时到期后自动回池
            bufferQueue.Enqueue(new BufferReturnObjectInfo(obj, delayTime));
        }

        #endregion

        #region Clear

        /// <summary>
        /// 清空指定 key 的缓存池。销毁所有缓存实例，并释放池自行持有的 prefab lease。
        /// </summary>
        public void Clear(string key)
        {
            // 销毁该 key 下所有缓存的 GameObject 实例
            DestroyPooledInstances(key);

            // 移除资源依赖映射
            dependencies.Remove(key);

            // 释放池自行持有的 Addressable 资源租约
            if (prefabLeases.TryGetValue(key, out ResourceLease<GameObject> prefabLease))
            {
                prefabLease.Dispose();
                prefabLeases.Remove(key);
            }
        }

        /// <summary>
        /// 清空所有对象池缓存，销毁全部实例并释放所有内部持有的 Addressable prefab lease。
        /// </summary>
        public void ClearAll()
        {
            // 遍历所有池，逐个销毁缓存的 GameObject 实例
            foreach (KeyValuePair<string, Queue<GameObject>> pair in objectPool)
            {
                Queue<GameObject> queue = pair.Value;
                while (queue.Count > 0)
                {
                    GameObject pooledObject = queue.Dequeue();
                    if (pooledObject != null) Destroy(pooledObject);
                }
            }

            objectPool.Clear();
            dependencies.Clear();

            // 释放所有 Addressable 资源租约
            foreach (KeyValuePair<string, ResourceLease<GameObject>> pair in prefabLeases)
                pair.Value?.Dispose();

            prefabLeases.Clear();
        }

        #endregion

        #region Private

        /// <summary>
        /// 全局资源释放回调。AddressableManager 释放资源时触发，
        /// 遍历所有依赖映射，清理绑定到该资源的池。
        /// </summary>
        private void OnDependencyReleased(string resourceKey)
        {
            // 快速退出：无效 key 或无依赖记录
            if (string.IsNullOrWhiteSpace(resourceKey) || dependencies.Count == 0)
                return;

            // 扫描依赖映射，找出绑定到此资源的所有池 key
            // resourceKey 格式为 "FullTypeName::key"，dependencyKey 为简写 key，用 EndsWith 后缀匹配
            var keysToClear = new List<string>();
            foreach (KeyValuePair<string, HashSet<string>> pair in dependencies)
            {
                foreach (string depKey in pair.Value)
                {
                    if (resourceKey.EndsWith("::" + depKey))
                    {
                        keysToClear.Add(pair.Key);
                        break;
                    }
                }
            }

            // 逐池清理：销毁缓存实例并移除依赖记录
            foreach (string key in keysToClear)
            {
                DestroyPooledInstances(key);
                dependencies.Remove(key);
            }
        }

        /// <summary>
        /// 注册资源依赖绑定。将 poolKey 与 dependencyKey 关联（幂等，重复绑定不产生副作用）。
        /// </summary>
        private void RegisterDependency(string poolKey, string dependencyKey)
        {
            // 获取或创建该池的依赖集合
            if (!dependencies.TryGetValue(poolKey, out HashSet<string> depSet))
            {
                depSet = new HashSet<string>();
                dependencies[poolKey] = depSet;
            }

            // 将 dependencyKey 加入集合（HashSet 自动去重）
            depSet.Add(dependencyKey);
        }

        /// <summary>
        /// 获取对象的内部实现：优先从缓存池出队，池空则即时实例化。
        /// </summary>
        private GameObject GetInternal(GameObject prefab, string nameKey, Transform parent, UnityAction<GameObject> callback)
        {
            // 尝试从缓存池取出可用对象
            if (TryDequeuePooledObject(nameKey, parent, callback, out GameObject pooledObject))
            {
                pooledObject.SetActive(true);
                return pooledObject;
            }

            // 池空，即时实例化新对象并交付
            return InstantiateAndDeliver(prefab, nameKey, parent, callback);
        }

        /// <summary>
        /// 实例化对象并完成交付：命名、设置父节点、执行回调、激活。
        /// </summary>
        private GameObject InstantiateAndDeliver(GameObject source, string name, Transform parent, UnityAction<GameObject> callback)
        {
            // 实例化预制体
            GameObject instance = Instantiate(source);
            instance.name = name;
            instance.transform.SetParent(parent, true);

            // 回调用于外部对实例进行配置（如设置伤害数值、绑定目标等）
            callback?.Invoke(instance);
            instance.SetActive(true);
            return instance;
        }

        /// <summary>
        /// 销毁指定 key 下所有缓存的池实例，并移除该池记录。
        /// </summary>
        private void DestroyPooledInstances(string key)
        {
            if (!objectPool.TryGetValue(key, out Queue<GameObject> queue)) return;

            // 逐个出队并销毁
            while (queue.Count > 0)
            {
                GameObject obj = queue.Dequeue();
                if (obj != null) Destroy(obj);
            }

            objectPool.Remove(key);
        }

        /// <summary>
        /// 尝试从缓存池出队一个对象。成功返回 true 并执行回调；失败返回 false。
        /// </summary>
        private bool TryDequeuePooledObject(string objectName, Transform parent, UnityAction<GameObject> callback, out GameObject pooledObject)
        {
            pooledObject = null;

            // 检查该名称的池是否存在且有缓存实例
            if (!objectPool.TryGetValue(objectName, out Queue<GameObject> queue) || queue.Count == 0)
                return false;

            // 出队一个实例
            pooledObject = queue.Dequeue();
            if (pooledObject == null) return false;

            // 设置父节点并执行回调（如重新定位、重置状态）
            pooledObject.transform.SetParent(parent, true);
            callback?.Invoke(pooledObject);
            return true;
        }

        /// <summary>
        /// 获取或加载预制体的 Addressable 资源租约。
        /// 优先复用缓存的 lease；若缓存不存在或已失效则通过 Addressables 重新加载。
        /// </summary>
        private async Task<ResourceLease<GameObject>> GetOrAcquirePrefabLeaseAsync(string objectName)
        {
            // 优先复用缓存的 lease（未释放且资源有效）
            if (prefabLeases.TryGetValue(objectName, out ResourceLease<GameObject> cachedLease) &&
                cachedLease != null &&
                !cachedLease.IsReleased &&
                cachedLease.Asset != null)
            {
                return cachedLease;
            }

            // 缓存不存在或已失效，通过 Addressables 重新加载并缓存
            ResourceLease<GameObject> prefabLease = await AddressableManager.Instance.AcquireAssetAsync<GameObject>(objectName);
            prefabLeases[objectName] = prefabLease;
            return prefabLease;
        }

        #endregion
    }
}
