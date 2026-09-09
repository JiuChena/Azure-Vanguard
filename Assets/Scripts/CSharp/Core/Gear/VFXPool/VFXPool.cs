using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 特效对象池：按组名和预制体名双层分组缓存特效实例，出池登记、回池注销，定时清理孤儿。
    /// </summary>
    public class VFXPool
    {
        // 孤儿检查间隔（秒）
        private static readonly float CheckInterval = 5f;

        private static readonly VFXPool instance = new VFXPool();
        public static VFXPool Instance => instance;

        // 空闲特效缓存：组名 → 预制体名 → 实例队列
        private readonly Dictionary<string, Dictionary<string, Queue<GameObject>>> _cachedVfx
            = new Dictionary<string, Dictionary<string, Queue<GameObject>>>();

        // 出池登记表：实例 → (组名, 预制体名)，供 VFXRecycle 反查与孤儿检查
        private readonly Dictionary<GameObject, (string Group, string Name)> _activeVfx
            = new Dictionary<GameObject, (string Group, string Name)>();

        // 池化特效的父节点
        private Transform _root;

        // 驱动孤儿检查的宿主组件
        private VFXPoolHost _host;

        private VFXPool() { }

        /// <summary>
        /// 出池或实例化特效并播放，向 GOTimer 注入自动回池回调。
        /// </summary>
        /// <param name="group">特效分组名（如敌人受击、环境特效）。</param>
        /// <param name="prefabName">预制体名称，作为组内池的唯一标识。</param>
        /// <param name="prefab">特效预制体，仅用于首次实例化；资源加载由调用方负责。</param>
        /// <param name="position">出池位置。</param>
        /// <param name="rotation">出池旋转。</param>
        /// <param name="scale">出池缩放。</param>
        /// <param name="autoRecycleTime">自动回池时长（秒），必须大于 0。</param>
        /// <param name="callback">出池并初始化完成后的回调。</param>
        public void VFXSpawn(string group, string prefabName, GameObject prefab, Vector3 position, Quaternion rotation,
            Vector3 scale, float autoRecycleTime, UnityAction<GameObject> callback = null)
        {
            if (prefab == null) return;
            if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(prefabName)) return;

            // 自动回池时长必须大于 0，否则是调用方配置错误
            if (autoRecycleTime <= 0f)
            {
                Debug.LogWarning($"VFXSpawn 的 autoRecycleTime 必须大于 0（当前 {autoRecycleTime}），已忽略本次生成。");
                return;
            }

            EnsureRoot();
            EnsureHost();

            GameObject vfx = GetOrCreateInstance(group, prefabName, prefab);
            if (vfx == null) return;

            // 复位特效后挂载并登记
            vfx.transform.SetParent(null, true);
            ApplySpawnTransform(vfx.transform, prefab.transform, position, rotation, scale);
            vfx.SetActive(true);
            RestartSpawnedVfx(vfx);
            _activeVfx[vfx] = (group, prefabName);

            // 向 GOTimer 注入自动回池回调，到点后按登记信息回池
            GOTimer timer = vfx.GetComponent<GOTimer>();
            if (timer != null) timer.Register(autoRecycleTime, () => VFXRecycle(vfx));

            callback?.Invoke(vfx);
        }

        /// <summary>
        /// 手动回池指定特效实例；实例未登记或已被销毁时静默返回。
        /// </summary>
        /// <param name="instance">需要回池的特效实例。</param>
        public void VFXRecycle(GameObject instance)
        {
            if (instance == null) return;

            // 未登记或已被孤儿清理的实例直接忽略，表现上与销毁一致
            if (!_activeVfx.Remove(instance, out (string Group, string Name) entry)) return;

            EnsureRoot();
            GOTimer timer = instance.GetComponent<GOTimer>();
            if (timer != null) timer.Clear();

            instance.transform.SetParent(_root, false);
            instance.SetActive(false);

            // 按组/名入对应空闲队列
            if (!_cachedVfx.TryGetValue(entry.Group, out Dictionary<string, Queue<GameObject>> nameMap))
            {
                nameMap = new Dictionary<string, Queue<GameObject>>();
                _cachedVfx.Add(entry.Group, nameMap);
            }

            if (!nameMap.TryGetValue(entry.Name, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                nameMap.Add(entry.Name, queue);
            }

            queue.Enqueue(instance);
        }

        /// <summary>
        /// 释放指定组的全部特效（活跃实例与缓存实例）。
        /// </summary>
        /// <param name="group">要释放的分组名。</param>
        public void ClearGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group)) return;

            DestroyActiveByGroup(group);

            if (!_cachedVfx.TryGetValue(group, out Dictionary<string, Queue<GameObject>> nameMap)) return;

            // 销毁该组全部缓存实例
            foreach (KeyValuePair<string, Queue<GameObject>> pair in nameMap)
            {
                while (pair.Value.Count > 0)
                {
                    GameObject cached = pair.Value.Dequeue();
                    if (cached != null) Object.Destroy(cached);
                }
            }

            _cachedVfx.Remove(group);
        }

        /// <summary>
        /// 释放指定组内指定名称的特效池（活跃实例与缓存实例）。
        /// </summary>
        /// <param name="group">分组名。</param>
        /// <param name="prefabName">预制体名称。</param>
        public void ClearPool(string group, string prefabName)
        {
            if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(prefabName)) return;

            DestroyActiveByPool(group, prefabName);

            if (!_cachedVfx.TryGetValue(group, out Dictionary<string, Queue<GameObject>> nameMap)) return;
            if (!nameMap.TryGetValue(prefabName, out Queue<GameObject> queue)) return;

            while (queue.Count > 0)
            {
                GameObject cached = queue.Dequeue();
                if (cached != null) Object.Destroy(cached);
            }

            nameMap.Remove(prefabName);
            if (nameMap.Count == 0) _cachedVfx.Remove(group);
        }

        /// <summary>
        /// 释放全部特效池（活跃实例与缓存实例）。
        /// </summary>
        public void ClearAll()
        {
            List<string> groups = new List<string>(_cachedVfx.Keys);
            for (int i = 0; i < groups.Count; i++) ClearGroup(groups[i]);
        }

        /// <summary>
        /// 清理登记表中已随父物体销毁的孤儿实例。由宿主组件按固定间隔调用。
        /// </summary>
        internal void CheckOrphaned()
        {
            if (_activeVfx.Count == 0) return;

            // 收集已销毁的实例，避免遍历时修改字典
            List<GameObject> destroyed = null;
            foreach (KeyValuePair<GameObject, (string Group, string Name)> pair in _activeVfx)
            {
                if (pair.Key != null) continue;

                destroyed ??= new List<GameObject>();
                destroyed.Add(pair.Key);
            }

            if (destroyed == null) return;

            for (int i = 0; i < destroyed.Count; i++) _activeVfx.Remove(destroyed[i]);
        }

        /// <summary>
        /// 从组内空闲队列取实例，队列为空或实例已销毁时实例化预制体。
        /// </summary>
        private GameObject GetOrCreateInstance(string group, string prefabName, GameObject prefab)
        {
            if (_cachedVfx.TryGetValue(group, out Dictionary<string, Queue<GameObject>> nameMap)
                && nameMap.TryGetValue(prefabName, out Queue<GameObject> queue))
            {
                while (queue.Count > 0)
                {
                    GameObject cached = queue.Dequeue();
                    if (cached != null) return cached;
                }
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            return instance;
        }

        /// <summary>
        /// 销毁指定组的全部活跃实例并注销登记。
        /// </summary>
        /// <param name="group">分组名。</param>
        private void DestroyActiveByGroup(string group)
        {
            List<GameObject> matches = new List<GameObject>();
            foreach (KeyValuePair<GameObject, (string Group, string Name)> pair in _activeVfx)
            {
                if (pair.Key != null && pair.Value.Group == group) matches.Add(pair.Key);
            }

            for (int i = 0; i < matches.Count; i++)
            {
                _activeVfx.Remove(matches[i]);
                Object.Destroy(matches[i]);
            }
        }

        /// <summary>
        /// 销毁指定组内指定名称的全部活跃实例并注销登记。
        /// </summary>
        /// <param name="group">分组名。</param>
        /// <param name="prefabName">预制体名称。</param>
        private void DestroyActiveByPool(string group, string prefabName)
        {
            List<GameObject> matches = new List<GameObject>();
            foreach (KeyValuePair<GameObject, (string Group, string Name)> pair in _activeVfx)
            {
                if (pair.Key != null && pair.Value.Group == group && pair.Value.Name == prefabName)
                    matches.Add(pair.Key);
            }

            for (int i = 0; i < matches.Count; i++)
            {
                _activeVfx.Remove(matches[i]);
                Object.Destroy(matches[i]);
            }
        }

        /// <summary>
        /// 合成预制体本地偏移与出池变换，应用到实例。
        /// </summary>
        private static void ApplySpawnTransform(Transform instanceTransform, Transform prefabTransform, Vector3 position,
            Quaternion rotation, Vector3 scale)
        {
            if (instanceTransform == null || prefabTransform == null) return;

            Vector3 composedPosition = Matrix4x4.TRS(position, rotation, scale).MultiplyPoint3x4(prefabTransform.localPosition);
            Quaternion composedRotation = rotation * prefabTransform.localRotation;
            Vector3 composedScale = Vector3.Scale(prefabTransform.localScale, scale);

            instanceTransform.position = composedPosition;
            instanceTransform.rotation = composedRotation;
            instanceTransform.localScale = composedScale;
        }

        /// <summary>
        /// 重播实例内全部粒子系统并清除拖尾，保证复用对象播放状态正确。
        /// </summary>
        /// <param name="instance">出池的特效实例。</param>
        private static void RestartSpawnedVfx(GameObject instance)
        {
            if (instance == null) return;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null) continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            TrailRenderer[] trailRenderers = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                TrailRenderer trailRenderer = trailRenderers[i];
                if (trailRenderer == null) continue;

                trailRenderer.Clear();
            }
        }

        /// <summary>
        /// 确保池化特效的父节点存在（DontDestroyOnLoad）。
        /// </summary>
        private void EnsureRoot()
        {
            if (_root != null) return;

            GameObject rootObject = new GameObject("VFXPool");
            Object.DontDestroyOnLoad(rootObject);
            _root = rootObject.transform;
        }

        /// <summary>
        /// 确保孤儿检查宿主组件存在。
        /// </summary>
        private void EnsureHost()
        {
            if (_host != null) return;

            GameObject hostObject = new GameObject("VFXPoolHost");
            Object.DontDestroyOnLoad(hostObject);
            _host = hostObject.AddComponent<VFXPoolHost>();
        }
    }
}