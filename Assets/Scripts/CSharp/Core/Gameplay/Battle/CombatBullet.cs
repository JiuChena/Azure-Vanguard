using System.Collections.Generic;
using Core.Gear;
using Framework.Gameplay.Abilities;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗子弹表现基类：默认直线飞行（轨迹可重写），帧间线段扫描命中检测，命中特效查表生成，回池时机由子类决定并带保底计时。
    /// </summary>
    public abstract class CombatBullet : MonoBehaviour
    {
        /// <summary>
        /// 层级命中特效条目：命中层与掩码有交集时，在命中点生成特效并在时长到后自动回池。
        /// </summary>
        [System.Serializable]
        public class HitFxEntry
        {
            [Tooltip("匹配的目标层级；实际命中层与该掩码有交集即视为匹配")]
            public LayerMask mask;

            [Tooltip("命中特效预制体；走对象池生成，时长到自动回池")]
            public GameObject fxPrefab;

            [Tooltip("特效存活时长，单位：秒，到时自动回池")]
            [Min(0.05f)]
            public float duration = 1f;
        }

        [Header("飞行")]
        [Tooltip("默认飞行速度，单位：米/秒；Init 传入正数速度时覆盖该值")]
        [Min(0.01f)]
        [SerializeField] private float moveSpeed = 10f;

        [Header("回收")]
        [Tooltip("回收保底时长，单位：秒；命中提前回收会取消该计时，超时未回收则自动回池兜底")]
        [Min(0.1f)]
        [SerializeField] private float maxLifetime = 5f;

        [Header("命中")]
        [Tooltip("可命中层级总掩码；线段扫描只检测这些层级上的碰撞体")]
        [SerializeField] private LayerMask hitMask;

        [Tooltip("层级命中特效表；按顺序取第一条与命中层有交集的条目，无匹配则不出特效")]
        [SerializeField] private List<HitFxEntry> hitFxTable = new List<HitFxEntry>();

        // 当前飞行方向（世界空间单位向量），Init 时由生成方写入
        protected Vector3 flightDirection = Vector3.forward;
        // 当前飞行速度，单位：米/秒；Init 时取覆盖值或默认值
        protected float currentSpeed;
        // 发射方碰撞体列表；命中结果过滤这些碰撞体，避免子弹刚出膛即命中发射者
        protected Collider[] ignoredColliders;
        // 发射者单位（命中回能归属；Init 由生成方传入，null = 无攻击者不回能）
        private BattleUnitState attacker;
        // 命中效果 SO（2026-11-04：命中时按实际目标结算伤害/效果；空 = 只做表现）
        private EffectMethodExecuteSO hitEffect;
        // 发射者 GAS 上下文与战场（命中效果执行用；Init 传入）
        private AbilityOwnerContext ownerContext;
        private CombatService controller;

        // 是否已初始化并允许飞行；防止出池激活后 Init 前的空跑
        private bool armed;
        // 防止同一活性周期内重复回池（子类回收与保底计时可能同时到达）
        private bool recycled;
        // 上一帧位置；帧间线段扫描的起点，Init 时对齐出膛点
        private Vector3 prevPosition;
        // 保底回收计时组件；Awake 时确保存在
        private GOTimer gotimer;
        // 线段扫描复用缓冲，避免每帧分配；单帧命中数超过容量时截断（16 个足够）
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        #region Init

        /// <summary>
        /// 初始化子弹并进入飞行状态；由生成方在摆放完位姿后调用。
        /// </summary>
        /// <param name="direction">飞行方向（世界空间）；零向量时回退为正前方。</param>
        /// <param name="ownerColliders">发射方全部碰撞体，命中结果将过滤；可为 null 表示无过滤。</param>
        /// <param name="speedOverride">飞行速度覆盖，单位：米/秒；非正数时使用预制体默认速度。</param>
        /// <param name="shooter">发射者单位（命中回能归属）；可为 null 表示无攻击者。</param>
        public void Init(Vector3 direction, Collider[] ownerColliders, float speedOverride = 0f,
            BattleUnitState shooter = null, EffectMethodExecuteSO hitEffect = null,
            AbilityOwnerContext ownerContext = null, CombatService controller = null)
        {
            // 方向归一化；零向量回退正前方，避免除零与原地不动
            Vector3 normalized = direction.normalized;
            flightDirection = normalized == Vector3.zero ? Vector3.forward : normalized;
            ignoredColliders = ownerColliders;
            currentSpeed = speedOverride > 0f ? speedOverride : moveSpeed;
            attacker = shooter;
            this.hitEffect = hitEffect;
            this.ownerContext = ownerContext;
            this.controller = controller;

            // 防御性复位：覆盖出池可能残留的上一轮状态
            recycled = false;
            armed = true;
            prevPosition = transform.position;

            // 重上保底回收计时；命中提前回收时 Recycle 内部会清掉它
            if (gotimer != null)
            {
                gotimer.Clear();
                gotimer.Register(maxLifetime, Recycle);
            }
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 确保保底回收计时组件存在；子弹基类自身负责回收兜底契约，不依赖预制体手动挂载。
        /// </summary>
        private void Awake()
        {
            gotimer = GetComponent<GOTimer>();
            if (gotimer == null) gotimer = gameObject.AddComponent<GOTimer>();
        }

        /// <summary>
        /// 每帧推进：飞行一步 → 帧间线段扫描命中 → 记录本帧位置作为下帧扫描起点。
        /// </summary>
        private void Update()
        {
            if (!armed) return;

            float deltaTime = Time.deltaTime;
            FlyStep(deltaTime);
            SweepHits();
            prevPosition = transform.position;
        }

        /// <summary>
        /// 失活时清掉计时任务并解除飞行状态，为下次出池留干净状态。
        /// </summary>
        private void OnDisable()
        {
            if (gotimer != null) gotimer.Clear();
            armed = false;
        }

        #endregion

        #region Hit

        /// <summary>
        /// 单个子弹命中时的处理；特效生成、穿透计数与回收时机全部由子类决定。
        /// </summary>
        /// <param name="hit">本次命中的物理结果；按命中距离升序逐个回调。</param>
        protected abstract void OnBulletHit(RaycastHit hit);

        /// <summary>
        /// 帧间线段扫描：从上一帧位置到当前帧位置做射线检测，按距离升序逐个上报命中。
        /// </summary>
        private void SweepHits()
        {
            Vector3 from = prevPosition;
            Vector3 offset = transform.position - from;
            float distance = offset.magnitude;
            if (distance < 1e-5f) return;

            // 用带距离上限的射线等价实现线段检测；起点在碰撞体内部时不命中该碰撞体（射线语义）
            int count = Physics.RaycastNonAlloc(from, offset / distance, hitBuffer, distance, hitMask, QueryTriggerInteraction.Collide);

            // 按命中距离升序排序，保证子类按穿透顺序消费
            if (count > 1) System.Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                // 已回收（不可穿透子类命中即回收）则终止本帧剩余命中的派发
                if (recycled) break;
                if (IsIgnored(hitBuffer[i].collider)) continue;
                BattleUnitState target = ResolveHitUnit(hitBuffer[i]);
                // 命中回能（表驱动 2026-10-28：攻击者普攻技能行 energyGain，配 0 不回能）+ 击杀归属
                if (attacker != null && attacker.IsAlive && attacker.MaxEnergy > 0)
                {
                    int hitGain = attacker.Stats.NormalAtkId > 0
                        && BattleDataModule.TryGetSkillEnergyGain(attacker.Stats.NormalAtkId, out int gain) ? gain : 0;
                    if (hitGain > 0 && controller != null && controller.IsBattleActive)
                        controller.Resources.AddEnergy(attacker, hitGain);
                    if (target != null) target.LastHitBy = attacker.UnitId; // 击杀归属（模拟层 Step 死亡扫描用）
                }
                // 命中效果（2026-11-04）：把实际命中单位作为显式目标结算伤害/效果
                if (hitEffect != null && target != null && attacker != null)
                {
                    hitEffect.ExecuteHit(ownerContext, attacker, controller, target);
                }
                OnBulletHit(hitBuffer[i]);
            }
        }

        /// <summary>
        /// 判断碰撞体是否属于发射方需要过滤的碰撞体。
        /// </summary>
        /// <param name="collider">待判断的碰撞体。</param>
        /// <returns>属于发射方返回 true；未配置过滤列表或不在列表中返回 false。</returns>
        private bool IsIgnored(Collider collider)
        {
            if (collider == null || ignoredColliders == null) return false;

            for (int i = 0; i < ignoredColliders.Length; i++)
            {
                if (ignoredColliders[i] == collider) return true;
            }

            return false;
        }

        /// <summary>
        /// 在命中点生成层级匹配的第一条命中特效，并在配置时长到后自动回池。
        /// </summary>
        /// <param name="hit">本次命中的物理结果；特效挂在命中点、y 轴对齐命中面法线。</param>
        protected void SpawnHitFx(RaycastHit hit)
        {
            // 查表取第一条与命中层有交集的条目
            HitFxEntry entry = null;
            int layerBit = 1 << hit.collider.gameObject.layer;
            for (int i = 0; i < hitFxTable.Count; i++)
            {
                if ((hitFxTable[i].mask.value & layerBit) != 0)
                {
                    entry = hitFxTable[i];
                    break;
                }
            }

            if (entry == null || entry.fxPrefab == null) return;

            GameObject fx = ObjectsPool.Instance.Get(entry.fxPrefab, null, null);
            if (fx == null) return;

            // y 轴对齐命中面法线（最短弧旋转）：命中特效喷发轴一般为 +Y，LookRotation(normal) 只对齐 +Z 会歪
            fx.transform.SetPositionAndRotation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
            ObjectsPool.Instance.TimerPut(fx, entry.duration);
        }

        #endregion

        #region Flight

        /// <summary>
        /// 推进一帧飞行；默认沿当前方向匀速直线，子类可重写实现抛物线、追踪等轨迹。
        /// </summary>
        /// <param name="deltaTime">本帧时长，单位：秒。</param>
        protected virtual void FlyStep(float deltaTime)
        {
            transform.position += flightDirection * (currentSpeed * deltaTime);
        }

        #endregion

        #region Recycle

        /// <summary>
        /// 回收子弹回对象池；只在子类判定该回收时调用（普通弹命中即调，穿透弹累计后调），保底计时兜底遗漏。
        /// </summary>
        protected void Recycle()
        {
            if (recycled) return;

            recycled = true;
            if (gotimer != null) gotimer.Clear();
            ObjectsPool.Instance.Put(gameObject);
        }

        #endregion

        #region Private

        /// <summary>
        /// 命中结果按距离升序排序用的比较器；静态单例避免排序分配。
        /// </summary>
        private sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            /// <summary>
            /// 按命中距离升序比较两个命中结果。
            /// </summary>
            /// <param name="a">左操作数。</param>
            /// <param name="b">右操作数。</param>
            /// <returns>距离差的标准比较结果。</returns>
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        // 命中目标模拟单位（从命中视图上溯 GAS → BattleUnit 键；null = 命中非单位/未注册——击杀归属/回能忽略）
        private BattleUnitState ResolveHitUnit(RaycastHit hit)
        {
            GameplayAbilitySystem gas = hit.collider.gameObject.GetComponentInParent<GameplayAbilitySystem>();
            if (gas != null && gas.OwnerContext != null
                && gas.OwnerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData))
            {
                return unitData.Unit;
            }
            return null;
        }

        #endregion
    }
}
