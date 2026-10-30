using Framework.Gameplay.Abilities.BetterHSM.Combat;
using Framework.Gameplay.Abilities.Configuration;
using Framework.Gameplay.Battle;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 区域检测（感知）能力运行时：从 ② 注册的战场面采集全部状态转移所需外部数据
    /// （敌人位置/有无/守点等），按阵营走既有意图策略写 ③ 意图字段，
    /// 再把决策结论镜像成上下文标志（WantAttack/IsMoving/Downed）——标志变化帧经 HSM 门控入口标脏，
    /// BHSM 下帧评估中断。中断切换状态即决策，本能力不做状态仲裁。
    /// 战斗数据未就绪（非战斗 prefab/未出生）时整能力空转。
    /// </summary>
    public sealed class PerceptionAbilityRuntime : AbilityRuntime
    {
        // 本能力在拥有者上下文中的读取键：单位模拟状态 / 战场共享面 / HSM 上下文 / HSM 门控入口。
        private const AbilityRuntimeDataType UnitDataType = AbilityRuntimeDataType.BattleUnit;
        private const AbilityRuntimeDataType WorldDataType = AbilityRuntimeDataType.BattleWorld;
        private const AbilityRuntimeDataType HsmDataType = AbilityRuntimeDataType.BetterHSM;
        private const AbilityRuntimeDataType GateDataType = AbilityRuntimeDataType.HSMGate;

        // 当前能力的静态配置。
        private readonly PerceptionAbilitySO _configuration;
        // 意图策略：角色侧索敌与敌人侧近战意图，按阵营二选一（两实现均无状态可共享）。
        private readonly DefaultTargetingStrategy _characterStrategy = new DefaultTargetingStrategy();
        private readonly EnemyMeleeIntentStrategy _enemyStrategy = new EnemyMeleeIntentStrategy();
        // 意图查询面：战场数据全战斗不变，首次就绪时接线一次。
        private readonly BattleIntentContext _intentContext = new BattleIntentContext();

        // 模拟层控制器（能量/技能 CD 读取入口；② 注册战场面时接线）。
        private BattleController _controller;

        // 当前单位模拟状态；② 注册就绪前为 null。
        private BattleUnitState _unit;
        // 当前单位战斗状态上下文；决策标志的写入目标。
        private CombatStateContext _stateContext;
        // 当前单位 HSM 门控标脏入口；HSM 未启用门控时为 null（未门控层每帧照常评估）。
        private HSMGateRuntimeData _gate;
        // 意图查询面是否已接线。
        private bool _intentContextWired;
        // HSM 上下文类型不符只报一次错（配置缺陷，能力此后停摆）。
        private bool _stateContextTypeLogged;

        /// <summary>创建感知能力运行时并保存配置引用。</summary>
        /// <param name="configuration">感知能力配置资产。</param>
        public PerceptionAbilityRuntime(PerceptionAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>绑定能力拥有者上下文并校验配置。</summary>
        /// <param name="ownerContext">当前单位的能力拥有者上下文。</param>
        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {
            base.AbilityInit(ownerContext);
            if (_configuration == null) throw new System.InvalidOperationException("感知能力配置不能为空。");
        }

        /// <summary>执行能力普通帧逻辑：采数据 → 产意图 → 镜像标志 → 变化才标脏。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            // 依赖惰性就绪：② 注册数据与 HSM 初始化前整能力空转。
            if (_unit == null && !TryAcquireDependencies()) return;
            if (_stateContext == null) return;

            // 意图生产：按阵营走既有策略写 ③ 意图字段（口径与纯意图时期一致）。
            IUnitIntentStrategy strategy = _unit.Camp == BattleCamp.Character
                ? (IUnitIntentStrategy)_characterStrategy
                : _enemyStrategy;
            strategy.UpdateIntent(_unit, _intentContext);

            // 决策标志镜像：只比标志值不比原始数据（位置每帧变，按值比较才保门控生效）。
            float arriveSq = _configuration.MoveArriveDistance * _configuration.MoveArriveDistance;
            bool wantAttack = _unit.HasAttackIntent;
            bool isMoving = (_unit.MoveDestination - _unit.Position).sqrMagnitude > arriveSq;
            bool downed = _unit.IsDowned;

            // 技能意图（角色侧；敌人无技能）：E CD 就绪优先（CH0221 先 E 后 Q，优先级将来可配进表），
            // E 未就绪且 Q 能量满 → Q。Q 无 CD（能量门槛由 Energy 表达，上限 = 角色表 maxEnergy）。
            bool wantSkill = false;
            SkillSlot pendingSkill = SkillSlot.None;
            if (_unit.Camp == BattleCamp.Character && !downed)
            {
                if (_controller != null && _unit.Stats.SkillEId > 0
                    && _controller.Cooldowns.IsReady(_unit, _unit.Stats.SkillEId))
                {
                    wantSkill = true;
                    pendingSkill = SkillSlot.E;
                }
                else if (_unit.Stats.SkillQId > 0 && _unit.MaxEnergy > 0
                    && _unit.Energy >= _unit.MaxEnergy
                    && _controller.Cooldowns.IsReady(_unit, _unit.Stats.SkillQId))
                {
                    wantSkill = true;
                    pendingSkill = SkillSlot.Q;
                }
            }

            if (_stateContext.WantAttack == wantAttack
                && _stateContext.IsMoving == isMoving
                && _stateContext.Downed == downed
                && _stateContext.WantSkill == wantSkill
                && _stateContext.PendingSkillSlot == pendingSkill) return;

            _stateContext.WantAttack = wantAttack;
            _stateContext.IsMoving = isMoving;
            _stateContext.Downed = downed;
            _stateContext.WantSkill = wantSkill;
            _stateContext.PendingSkillSlot = pendingSkill;
            _gate?.MarkGatedDirty();
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            _unit = null;
            _stateContext = null;
            _gate = null;
            base.AbilityDispose();
        }

        // 惰性采集依赖：单位状态/战场面（②注册）与 HSM 上下文（BHSM 能力初始化注册）。
        private bool TryAcquireDependencies()
        {
            if (!OwnerContext.TryGet(UnitDataType, out BattleUnitRuntimeData unitData)) return false;
            if (!OwnerContext.TryGet(WorldDataType, out BattleWorldRuntimeData worldData)) return false;

            // 战场数据全战斗不变，首次接线一次（控制器内单位列表实例稳定）。
            if (!_intentContextWired)
            {
                BattleController controller = worldData.Controller;
                _controller = controller;
                _intentContext.Units = controller.Units;
                _intentContext.GuardPoint = controller.GuardPoint;
                _intentContext.GuardPointPosition = controller.GuardPointPosition;
                _intentContextWired = true;
            }

            _unit = unitData.Unit;

            // HSM 上下文：非战斗上下文属配置缺陷，报错一次后整能力停摆。
            if (!OwnerContext.TryGet(HsmDataType, out BetterHSMAbilityRuntimeData hsmData)) return false;
            if (!(hsmData.AbilityContext.StateContext is CombatStateContext stateContext))
            {
                if (!_stateContextTypeLogged)
                {
                    _stateContextTypeLogged = true;
                    Debug.LogError(
                        $"[感知能力] '{OwnerContext.Owner.name}' 的 HSM 上下文不是 CombatStateContext，感知停摆。",
                        OwnerContext.Owner);
                }
                return false;
            }
            _stateContext = stateContext;

            // 门控入口：注册工厂在建图后注册；未启用门控的 HSM 没有，感知退化为仅写标志。
            OwnerContext.TryGet(GateDataType, out _gate);
            return true;
        }
    }
}
