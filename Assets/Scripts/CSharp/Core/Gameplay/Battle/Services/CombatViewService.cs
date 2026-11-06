using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Abilities.Buff;
using UnityEngine;
using UnityEngine.AI;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗视图桥子服务：按单位 id 加载战斗视图 prefab、实例化/销毁、head 节点解析、
    /// GAS 战场数据注册、NavMesh 预置、天赋展示同步；只消费模拟事件，不反向写模拟状态。
    /// </summary>
    public sealed class CombatViewService : ICombatSubservice
    {
        // 单位视图 prefab 的 Addressables 标签通道（id 即地址；Combat 标签交集）。
        private static readonly string[] UnitViewLabels = { "Combat" };

        // 出生点网面判定半径（NavMesh.SamplePosition 水平找网面；大于此距离视为网面外，代理保持禁用）。
        private const float SpawnNavMeshSampleRadius = 1f;

        // 所属战斗服务（单位查询 / 事件订阅 / 天赋效果挂展示）。
        private CombatService service;

        // 视图父节点；null = 不挂父级。
        private Transform viewRoot;

        // 战场共享数据入口（每个单位注册同一实例）。
        private BattleWorldRuntimeData worldData;

        // 当前视图代际：Begin/Reset 自增，作废在途异步加载。
        private int viewEpoch;

        // 单位 → 视图实例（敌人出表销毁；角色倒地留场）。
        private readonly Dictionary<BattleUnitState, GameObject> views =
            new Dictionary<BattleUnitState, GameObject>();

        // 单位 id → prefab 租约（战斗生命周期共享，同 id 多实例共用一份）。
        private readonly Dictionary<string, ResourceLease<GameObject>> viewPrefabLeases =
            new Dictionary<string, ResourceLease<GameObject>>();

        // 已出表但视图仍在首载的单位（视图就位后立即销毁，防幽灵尸体）。
        private readonly HashSet<BattleUnitState> removedWithoutView = new HashSet<BattleUnitState>();

        // 天赋展示桥：等待 GAS 的 Buff/Behavior 能力就绪后挂展示 buff / 播天赋 clip。
        private readonly Dictionary<BattleUnitState, GameplayAbilitySystem> pendingTalentViews =
            new Dictionary<BattleUnitState, GameplayAbilitySystem>();
        private readonly Dictionary<BattleUnitState, GameplayAbilitySystem> pendingTalentClips =
            new Dictionary<BattleUnitState, GameplayAbilitySystem>();
        private readonly HashSet<BattleUnitState> talentClipPlayed = new HashSet<BattleUnitState>();

        /// <summary>绑定战斗服务与视图父节点，开始接收出生/出表事件（重复调用先解绑）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        /// <param name="root">视图父节点；可为 null。</param>
        public void Begin(CombatService combatService, Transform root)
        {
            if (combatService == null) throw new ArgumentNullException(nameof(combatService));

            Reset();
            service = combatService;
            viewRoot = root;
            worldData = new BattleWorldRuntimeData(service);
            viewEpoch++;
            service.UnitSpawned += HandleUnitSpawned;
            service.EnemyRemoved += HandleEnemyRemoved;
        }

        /// <summary>退订事件、销毁全部视图、释放 prefab 租约并清空挂起状态。</summary>
        public void Reset()
        {
            viewEpoch++;
            if (service != null)
            {
                service.UnitSpawned -= HandleUnitSpawned;
                service.EnemyRemoved -= HandleEnemyRemoved;
            }

            foreach (KeyValuePair<BattleUnitState, GameObject> pair in views)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            views.Clear();

            foreach (ResourceLease<GameObject> lease in viewPrefabLeases.Values)
            {
                try
                {
                    lease?.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[CombatView] prefab 租约释放异常：{exception.Message}");
                }
            }
            viewPrefabLeases.Clear();
            removedWithoutView.Clear();
            pendingTalentViews.Clear();
            pendingTalentClips.Clear();
            talentClipPlayed.Clear();

            service = null;
            viewRoot = null;
            worldData = null;
        }

        /// <summary>每帧同步天赋展示桥（暂停/胜负已定也调用）。</summary>
        public void Tick()
        {
            TrySyncTalentViews();
        }

        /// <summary>
        /// 视图实例反查单位（调试面板点击选中用）；命中碰撞体可能在子骨骼，沿父链上溯。
        /// </summary>
        /// <param name="view">点击命中的视图对象。</param>
        /// <param name="unit">反查到的单位；未命中为 null。</param>
        /// <returns>命中返回 true。</returns>
        public bool TryGetUnitByView(GameObject view, out BattleUnitState unit)
        {
            unit = null;
            for (GameObject node = view; node != null;
                 node = node.transform.parent != null ? node.transform.parent.gameObject : null)
            {
                foreach (KeyValuePair<BattleUnitState, GameObject> pair in views)
                {
                    if (pair.Value == node)
                    {
                        unit = pair.Key;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 按 id 加载战斗视图 prefab（id 即 Addressables 地址）；仅运行时加载。
        /// </summary>
        /// <param name="unitId">单位 id。</param>
        /// <returns>加载到的 prefab；编辑态/失败返回 null。</returns>
        public async Task<GameObject> LoadViewPrefabAsync(string unitId)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"[CombatView] '{unitId}' 视图仅在运行时按 id 加载（EditMode 不拉 Addressables），无表现。");
                return null;
            }
            if (viewPrefabLeases.TryGetValue(unitId, out ResourceLease<GameObject> cached))
            {
                return cached.Asset;
            }

            int epoch = viewEpoch;
            try
            {
                ResourceLease<GameObject> lease = await AddressableManager.Instance
                    .AcquireAssetWithLabelsAsync<GameObject>(unitId, UnitViewLabels);
                // Reset/Begin 已发生：迟到租约不入缓存，立即释放防泄漏
                if (epoch != viewEpoch)
                {
                    lease?.Dispose();
                    return null;
                }
                viewPrefabLeases[unitId] = lease;
                return lease.Asset;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatView] 战斗视图 prefab 加载失败：'{unitId}'" +
                    $"（Combat 组未登记该 id 或标签缺失）。{ex.Message}");
                return null;
            }
        }

        // 单位出生：加载视图、落地序列、GAS 数据注册、天赋展示待挂。
        private async void HandleUnitSpawned(BattleUnitState unit)
        {
            if (service == null || unit == null) return;
            int epoch = viewEpoch;

            GameObject prefab = await LoadViewPrefabAsync(unit.UnitId);
            if (prefab == null || epoch != viewEpoch) return;

            // 首载等待期已出表：直接丢弃，不落幽灵尸体
            if (removedWithoutView.Remove(unit)) return;

            // 落地序列：先激活实例化、随即冻结纠正 → 摆回部署点 → 网面预置 → 再激活
            GameObject view = UnityEngine.Object.Instantiate(prefab, unit.Position, Quaternion.identity, viewRoot);
            view.name = $"{prefab.name}_{unit.UnitId}";
            view.SetActive(false);
            view.transform.position = unit.Position;
            PrefitSpawnNavAgent(view, unit.Position);
            view.SetActive(true);

            // 场内注册表：登记 head 节点，供子弹锁头/攻击转向取目标 head 世界位
            FieldUnitRegistry.Register(unit.UnitId, unit, ResolveHeadNode(view, unit.Stats.HeadNode));

            GameplayAbilitySystem gas = view.GetComponent<GameplayAbilitySystem>();
            if (gas == null || gas.OwnerContext == null)
            {
                Debug.LogError($"[CombatView] '{view.name}' 缺 GameplayAbilitySystem，能力数据不注册（感知/寻路停摆）。", view);
                views[unit] = view;
                return;
            }

            gas.OwnerContext.Register(AbilityRuntimeDataType.BattleUnit, new BattleUnitRuntimeData(unit));
            gas.OwnerContext.Register(AbilityRuntimeDataType.BattleWorld, worldData);
            pendingTalentViews[unit] = gas;
            pendingTalentClips[unit] = gas;
            talentClipPlayed.Remove(unit);
            views[unit] = view;
        }

        // 敌人死亡出表：销毁视图 + 注销场内注册表（角色 Downed 不走此事件，留场）。
        private void HandleEnemyRemoved(BattleUnitState unit)
        {
            FieldUnitRegistry.Unregister(unit);
            if (!views.TryGetValue(unit, out GameObject view))
            {
                removedWithoutView.Add(unit);
                return;
            }
            views.Remove(unit);
            if (view != null) UnityEngine.Object.Destroy(view);
        }

        // 解析头部骨骼：路径优先，未命中按名全扫；失败返回 null（锁头回退根位置）。
        private static Transform ResolveHeadNode(GameObject view, string headNode)
        {
            if (string.IsNullOrWhiteSpace(headNode)) return null;
            Transform found = view.transform.Find(headNode);
            if (found != null) return found;
            foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == headNode) return child;
            }
            return null;
        }

        // NavMeshAgent 网面预置：出生点在网面上才启用，否则保持禁用防被 snap 到地面。
        private static void PrefitSpawnNavAgent(GameObject view, Vector3 spawnPoint)
        {
            NavMeshAgent agent = view.GetComponent<NavMeshAgent>();
            if (agent == null) return;

            if (NavMesh.SamplePosition(spawnPoint, out NavMeshHit hit, SpawnNavMeshSampleRadius, NavMesh.AllAreas))
            {
                agent.enabled = true;
                return;
            }
            agent.enabled = false;
            Debug.LogWarning($"[CombatView] '{view.name}' 出生点不在 NavMesh 网面上（高台未连通？），" +
                "NavMeshAgent 保持禁用、单位原地站稳。", view);
        }

        // 天赋视图就绪同步：Buff 就绪挂展示 buff；Behavior 就绪且 CH0221 播一次天赋 clip。
        private void TrySyncTalentViews()
        {
            if (service == null) return;

            List<BattleUnitState> done = null;
            foreach (KeyValuePair<BattleUnitState, GameplayAbilitySystem> pair in pendingTalentViews)
            {
                BattleUnitState unit = pair.Key;
                GameplayAbilitySystem gas = pair.Value;
                if (unit == null || gas == null || gas.OwnerContext == null)
                {
                    (done ??= new List<BattleUnitState>()).Add(unit);
                    continue;
                }

                if (gas.OwnerContext.TryGet(AbilityRuntimeDataType.Buff, out BuffRuntimeData buffData))
                {
                    service.Effects?.AttachView(unit, buffData);
                    (done ??= new List<BattleUnitState>()).Add(unit);
                }
            }
            if (done != null)
            {
                for (int i = 0; i < done.Count; i++) pendingTalentViews.Remove(done[i]);
            }

            done = null;
            foreach (KeyValuePair<BattleUnitState, GameplayAbilitySystem> pair in pendingTalentClips)
            {
                BattleUnitState unit = pair.Key;
                GameplayAbilitySystem gas = pair.Value;
                if (unit == null || gas == null || gas.OwnerContext == null || talentClipPlayed.Contains(unit))
                {
                    (done ??= new List<BattleUnitState>()).Add(unit);
                    continue;
                }
                if (unit.UnitId != "CH0221") continue;
                // Behavior 就绪后置一次性请求，由 HSM Talent 态播放并消费
                if (!gas.OwnerContext.TryGet(AbilityRuntimeDataType.Behavior, out BehaviorAbilityRuntimeData _)) continue;

                talentClipPlayed.Add(unit);
                unit.WantTalent = true;
                (done ??= new List<BattleUnitState>()).Add(unit);
            }
            if (done == null) return;
            for (int i = 0; i < done.Count; i++) pendingTalentClips.Remove(done[i]);
        }
    }
}
