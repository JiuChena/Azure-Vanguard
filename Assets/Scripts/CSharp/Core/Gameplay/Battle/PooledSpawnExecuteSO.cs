using System.Collections.Generic;
using BehaviorEditor;
using Core.Gear;
using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 通用对象池生成事件：优先使用拖拽的子弹预制体引用走对象池生成，引用为空时按备用地址串加载，在行为事件位姿生成并初始化。
    /// </summary>
    [CreateAssetMenu(menuName = "AzureVanguard/Battle/PooledSpawnExecuteSO", fileName = "PooledSpawn_New")]
    public class PooledSpawnExecuteSO : BehaviorEventExecuteSO
    {
        [Header("资源来源")]
        [Tooltip("子弹预制体引用；直接拖拽赋值，优先于地址加载，走对象池按预制体名复用")]
        [SerializeField] private GameObject bulletPrefab;

        [Tooltip("备用资源地址：预制体引用为空时生效；单个字符串按地址自载，逗号分隔（中英文逗号均可）时首段为地址，其余为必须包含的标签")]
        [TextArea(1, 3)]
        [SerializeField] private string resourceAddress;

        [Header("子弹")]
        [Tooltip("发射速度覆盖，单位：米/秒；0 表示使用子弹预制体上配置的默认速度")]
        [Min(0f)]
        [SerializeField] private float speedOverride = 0f;

        [Tooltip("锁头发射者当前目标 head（单位/守点），目标点加 BulletSpread 的 XYZ 随机散射；" +
            "关闭则沿事件旋转正前方直线飞（不索敌）")]
        [SerializeField] private bool aimAtTarget = true;

        [Header("命中效果")]
        [Tooltip("子弹命中时执行的效果 SO（如 CH0221_NormalAttack.asset）；空 = 命中只做表现不回血")]
        [SerializeField] private EffectMethodExecuteSO hitEffect;

        // 标签约束路径的租约缓存：资源地址 → 已获取的预制体租约；v1 常驻不释放，保证池内存量实例引用不因资源卸载失效
        private readonly Dictionary<string, ResourceLease<GameObject>> leaseCache = new Dictionary<string, ResourceLease<GameObject>>();

        #region Execute

        /// <summary>
        /// 执行生成事件：解析资源来源串，走地址或地址+标签路径从对象池取出实例并在事件位姿生成子弹。
        /// </summary>
        /// <param name="context">行为事件上下文；位置/旋转/缩放为骨骼解析后的世界位姿。</param>
        public override void Execute(BehaviorEventContext context)
        {
            // 预制体引用优先：直接走对象池 prefab 路径，不绑依赖键（共享资产常驻）
            if (bulletPrefab != null)
            {
                ObjectsPool.Instance.Get(bulletPrefab, null, null, instance => SpawnBullet(instance, context));
                return;
            }

            // 无预制体引用：解析备用地址串（中英文逗号均可分隔，过滤空段）
            List<string> parts = ParseSource(resourceAddress);
            if (parts.Count == 0) return;

            if (parts.Count == 1)
            {
                // 单段 = 纯地址：对象池自载路径，租约由对象池持有
                ObjectsPool.Instance.Get(parts[0], null, instance => SpawnBullet(instance, context));
                return;
            }

            // 多段 = 地址 + 必须标签混合约束；加载为异步，完成后仍走对象池生成
            LoadByAddressWithLabelsAsync(parts[0], parts.GetRange(1, parts.Count - 1), context);
        }

        #endregion

        #region Private

        /// <summary>
        /// 地址+标签混合约束加载预制体并生成子弹；租约按地址缓存复用。
        /// </summary>
        /// <param name="address">资源地址。</param>
        /// <param name="labels">资源必须同时包含的标签列表。</param>
        /// <param name="context">行为事件上下文，透传给生成步骤。</param>
        private async void LoadByAddressWithLabelsAsync(string address, List<string> labels, BehaviorEventContext context)
        {
            // 租约按地址缓存复用；已失效则重新获取
            ResourceLease<GameObject> lease = null;
            if (leaseCache.TryGetValue(address, out ResourceLease<GameObject> cached) && cached != null && !cached.IsReleased && cached.Asset != null)
            {
                lease = cached;
            }
            else
            {
                try
                {
                    lease = await AddressableManager.Instance.AcquireAssetWithLabelsAsync<GameObject>(address, labels);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[PooledSpawnExecuteSO] 地址标签混合约束加载异常：{address} + [{string.Join(",", labels)}] | {exception.Message}", this);
                    return;
                }

                leaseCache[address] = lease;
            }

            if (lease == null || lease.Asset == null)
            {
                Debug.LogError($"[PooledSpawnExecuteSO] 地址标签混合约束加载失败：{address} + [{string.Join(",", labels)}]", this);
                return;
            }

            // 依赖键绑定资源地址：该资源将来被全局释放时，对象池自动清理对应缓存实例
            ObjectsPool.Instance.Get(lease.Asset, address, null, instance => SpawnBullet(instance, context));
        }

        /// <summary>
        /// 在事件位姿摆放实例并初始化子弹；实例缺少子弹组件时报错回池。
        /// </summary>
        /// <param name="instance">从对象池取出的实例。</param>
        /// <param name="context">行为事件上下文；位置/旋转为骨骼解析结果，缩放为事件配置的绝对覆盖。</param>
        private void SpawnBullet(GameObject instance, BehaviorEventContext context)
        {
            if (instance == null) return;

            // 子弹脚本缺失直接报错回池，避免实例滞留场景
            CombatBullet bullet = instance.GetComponent<CombatBullet>();
            if (bullet == null)
            {
                Debug.LogError($"[PooledSpawnExecuteSO] 预制体缺少 CombatBullet 组件：{instance.name}", this);
                ObjectsPool.Instance.Put(instance);
                return;
            }

            // 事件位姿摆放：位置/旋转取骨骼解析结果；缩放按事件配置绝对覆盖（one 表示不改）
            instance.transform.SetPositionAndRotation(context.Position, context.Rotation);
            if (context.Scale != Vector3.one) instance.transform.localScale = context.Scale;

            // 收集发射方全部碰撞体供子弹过滤自伤；飞行方向默认事件正向，aimAtTarget 开启时锁发射者当前目标 head
            Collider[] ownerColliders = context.OwnerGameObject != null
                ? context.OwnerGameObject.GetComponentsInChildren<Collider>(true)
                : null;
            Vector3 flightDirection = aimAtTarget ? ResolveAimDirection(context) : context.Rotation * Vector3.forward;

            // 发射者单位（命中回能归属）：行为事件上下文已注入 GAS 能力上下文（2026-10-27 技能方案第 0 步）
            BattleUnitState shooter = null;
            if (context.OwnerContext != null
                && context.OwnerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData shooterData))
            {
                shooter = shooterData.Unit;
            }
            BattleController controller = context.OwnerContext != null
                && context.OwnerContext.TryGet(AbilityRuntimeDataType.BattleWorld, out BattleWorldRuntimeData worldData)
                    ? worldData.Controller
                    : null;
            bullet.Init(flightDirection, ownerColliders, speedOverride, shooter, hitEffect, context.OwnerContext, controller);
        }

        /// <summary>
        /// 解析锁头发射方向：发射者 GAS → 单位模拟状态 → 当前目标（单位 id / 守点）→ 注册表取 head
        /// 世界位 → 目标点加 XYZ 散射（BulletSpread）→ 方向 = 目标点 − 枪口；目标缺失/未注册回退事件正向。
        /// </summary>
        /// <param name="context">行为事件上下文；OwnerGameObject 供取发射者 GAS，Position 即枪口世界位。</param>
        /// <returns>发射方向（单位向量）。</returns>
        private Vector3 ResolveAimDirection(BehaviorEventContext context)
        {
            if (context.OwnerGameObject != null)
            {
                GameplayAbilitySystem gas = context.OwnerGameObject.GetComponent<GameplayAbilitySystem>();
                if (gas != null && gas.OwnerContext != null
                    && gas.OwnerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData))
                {
                    BattleUnitState unit = unitData.Unit;
                    if (unit.TargetIsGuardPoint
                        && FieldUnitRegistry.TryGetHead(FieldUnitRegistry.GuardPointKey, out Transform guardHead))
                    {
                        return BattleAimMath.ResolveDirection(context.Position, guardHead.position, unit.Stats.BulletSpread);
                    }
                    // 实例目标优先（敌人同 UnitId 多实例）；仅 id 的旧夹具保留兼容
                    if (unit.TargetUnit != null
                        && FieldUnitRegistry.TryGetHead(unit.TargetUnit, out Transform instanceHead))
                    {
                        return BattleAimMath.ResolveDirection(context.Position, instanceHead.position, unit.Stats.BulletSpread);
                    }
                    if (unit.TargetUnitId != null
                        && FieldUnitRegistry.TryGetHead(unit.TargetUnitId, out Transform targetHead))
                    {
                        return BattleAimMath.ResolveDirection(context.Position, targetHead.position, unit.Stats.BulletSpread);
                    }
                }
            }
            return context.Rotation * Vector3.forward;
        }

        /// <summary>
        /// 解析备用地址串：按中英文逗号分隔，逐段去空白后保留非空段。
        /// </summary>
        /// <param name="source">配置的备用地址串；null 或空白返回空列表。</param>
        /// <returns>分隔后的非空段列表，首段为地址，其余（若有）为必须标签。</returns>
        private static List<string> ParseSource(string source)
        {
            List<string> parts = new List<string>();
            if (string.IsNullOrWhiteSpace(source)) return parts;

            // 中英文逗号统一按分隔符处理，逐段 trim 后保留非空段
            string[] segments = source.Split(new[] { ',', '，' });
            foreach (string segment in segments)
            {
                string part = segment.Trim();
                if (!string.IsNullOrEmpty(part)) parts.Add(part);
            }

            return parts;
        }

        #endregion
    }
}
