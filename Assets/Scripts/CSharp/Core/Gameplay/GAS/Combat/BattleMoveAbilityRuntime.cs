using System.Collections.Generic;
using Framework.Gameplay.Abilities.BetterHSM.Combat;
using Framework.Gameplay.Battle;
using UnityEngine;
using UnityEngine.AI;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 战斗移动能力运行时：读本单位 HSM 当前状态——Move 态向 ③ 意图目的地推进，
    /// 其余状态（攻击站桩/倒地/待机）停走，走停裁决权在 HSM。每帧把位置回写 ③ Position
    /// （BattleUnitState 上 ② 写 ③ 的唯一豁免口）。战斗数据未就绪时整能力空转。
    /// 角色走 NavMesh 寻路（角色少且不允许堆叠）；敌人路线制手动位移——直线段逐帧逼近路点，
    /// 严格沿折线、无视障碍（NavMesh 避让会绕障偏离路线且破坏聚堆，塔防设计预期）。
    /// </summary>
    public sealed class BattleMoveAbilityRuntime : AbilityRuntime
    {
        // 本能力在拥有者上下文中的读取键：单位模拟状态 / HSM 上下文 / 战场共享面（攻击目标位置解析）。
        private const AbilityRuntimeDataType UnitDataType = AbilityRuntimeDataType.BattleUnit;
        private const AbilityRuntimeDataType HsmDataType = AbilityRuntimeDataType.BetterHSM;
        private const AbilityRuntimeDataType WorldDataType = AbilityRuntimeDataType.BattleWorld;
        // 目的地变更检测阈值（平方距离；低于视为同一点，避免每帧重算路径）。
        private const float DestinationChangeThresholdSq = 0.0001f;

        // 当前单位模拟状态；② 注册就绪前为 null。
        private BattleUnitState _unit;
        // 当前单位 HSM 上下文；Move 态判定来源。
        private BetterHSMAbilityRuntimeData _hsmData;
        // 战场共享面（攻击目标 ③ 位置查找）；未注册时为 null，转向静默停摆。
        private BattleWorldRuntimeData _worldData;
        // 单位寻路代理；取同物体组件，缺代理时为 null（能力静默停摆）。
        private NavMeshAgent _agent;
        // 攻击转向节点（rotateNode 路径解析结果，首次用时缓存）。
        private Transform _facingNode;
        // 转向节点是否已解析（防每帧重复 Find）。
        private bool _facingNodeResolved;
        // 转向节点路径缺失已告警（配置缺陷只报一次）。
        private bool _facingNodeMissingLogged;
        // 是否已有生效目的地（变更检测与停走重下发判定）。
        private bool _hasDestination;
        // 上次下发的目的地。
        private Vector3 _lastDestination;
        // 停走横向偏移向量（停走期间视图叠加在模拟位上；零 = 未偏移）。
        private Vector3 _lateralStopOffset;

        /// <summary>执行能力普通帧逻辑：按 HSM 状态下发走停，并回写位置。</summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            // 依赖惰性就绪：② 注册数据与 HSM 初始化前整能力空转。
            if (_unit == null && !TryAcquireDependencies()) return;

            // 攻击面向（2026-11-06）：仅 AttackStart / Attacking（ICombatAttackFacingState）允许转向；
            // AttackEnd/Skill/Talent/Idle/Move 不转。目标位置 = 守点或攻击单位 ③ 位置；
            // 无目标/目标出表/战场面未注册本帧不转。转速来源 = BattleUnitStats.RotateSpeed（表已不配，代码默认）。
            if (_unit.Stats.RotateSpeed > 0f
                && _hsmData.AbilityContext.CurrentState is ICombatAttackFacingState
                && _unit.HasAttackIntent
                && _worldData != null
                && TryGetAttackTargetPosition(out Vector3 targetPos))
            {
                BattleFacing.StepYawDelta(ResolveFacingNode(), targetPos, _unit.Stats.RotateSpeed, deltaTime);
            }

            // 敌人路线制：直线段手动位移（不经 NavMeshAgent；到达判定归感知 isMoving → HSM 停走）。
            if (_unit.Camp == BattleCamp.Enemy)
            {
                Transform owner = OwnerContext.Owner.transform;
                if (ShouldMove())
                {
                    // 恢复移动：清除停走侧偏（下次停走重新偏移），位置由 MoveTowards 从侧偏点自然走回
                    _lateralStopOffset = Vector3.zero;
                    // 朝行进方向转向（转身体 root；同 BattleFacing 固定转速过渡，攻击转向才走 rotateNode 子节点）——
                    // 只平移不转向，敌人沿路线"贴地滑行"观感差；RotateSpeed>0 才转
                    if (_unit.Stats.RotateSpeed > 0f)
                    {
                        Vector3 to = _unit.MoveDestination - owner.position;
                        to.y = 0f;
                        if (to.sqrMagnitude > 1e-6f)
                        {
                            BattleFacing.StepYawDelta(owner, _unit.MoveDestination, _unit.Stats.RotateSpeed, deltaTime);
                        }
                    }
                    owner.position = Vector3.MoveTowards(owner.position, _unit.MoveDestination,
                        _unit.Stats.MoveSpeed * deltaTime);
                    _unit.Position = owner.position;
                }
                else
                {
                    // 停走首次且带攻击意图：沿朝向垂线随机左右偏。仅视图叠加在模拟位上，③ 不动——
                    // 避免侧偏把模拟位置推出射程门导致停↔走震荡；目标死亡/恢复移动后自然走回
                    if (_lateralStopOffset == Vector3.zero && _unit.HasAttackIntent)
                    {
                        _lateralStopOffset = ResolveStopLateralOffset(_unit, _unit.Stats.StopLateralOffset);
                    }
                    if (_lateralStopOffset != Vector3.zero)
                    {
                        owner.position = _unit.Position + _lateralStopOffset;
                    }
                }
                return;
            }

            if (_agent == null) return;

            if (ShouldMove())
            {
                // 目的地未变更跳过 SetDestination（避免每帧重算路径）。
                if (!_hasDestination
                    || (_unit.MoveDestination - _lastDestination).sqrMagnitude > DestinationChangeThresholdSq)
                {
                    _lastDestination = _unit.MoveDestination;
                    _hasDestination = true;
                    if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
                    {
                        if (_agent.isStopped) _agent.isStopped = false;
                        _agent.SetDestination(_unit.MoveDestination);
                    }
                }
            }
            else
            {
                // 停走并清目的地缓存（回到 Move 态时强制重新下发）。
                _hasDestination = false;
                if (_agent.isActiveAndEnabled && _agent.isOnNavMesh && !_agent.isStopped) _agent.isStopped = true;
            }

            // 代理位置回写 ③（②→③ 唯一豁免口；验证脚本经 Position 断言走位）。
            // 代理未激活/不在网面（部署点在未连通高台区时代理保持禁用）不回写——
            // 此时 nextPosition 是投影/未初始化值，回写会把 ③ 位置拉离部署点。
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _unit.Position = _agent.nextPosition;
            }
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            _unit = null;
            _hsmData = null;
            _worldData = null;
            _facingNode = null;
            _facingNodeResolved = false;
            _agent = null;
            _lateralStopOffset = Vector3.zero;
            base.AbilityDispose();
        }

        // 是否应当移动：存活且 HSM 处于战斗移动状态（状态即走停的唯一裁决，攻击站桩/倒地停走自动成立）。
        private bool ShouldMove()
        {
            return _unit.IsAlive && _hsmData.AbilityContext.CurrentState is ICombatMoveState;
        }

        /// <summary>
        /// 解析当前攻击目标位置：守点/单位目标都优先取 head 世界位（②表现层注册表，锁头转向），
        /// 未登记或 head 缺失回退 ③ 根位置/守点坐标。目标不在表（已死亡出表/未部署）返回 false，本帧不转向。
        /// </summary>
        private bool TryGetAttackTargetPosition(out Vector3 targetPos)
        {
            if (_unit.TargetIsGuardPoint)
            {
                if (FieldUnitRegistry.TryGetHead(FieldUnitRegistry.GuardPointKey, out Transform guardHead))
                {
                    targetPos = guardHead.position;
                    return true;
                }
                targetPos = _worldData.Controller.GuardPointPosition;
                return true;
            }
            // 实例目标优先（敌人同 UnitId 多实例）；仅 id 的旧夹具保留兼容
            if (_unit.TargetUnit != null)
            {
                if (FieldUnitRegistry.TryGetHead(_unit.TargetUnit, out Transform instanceHead))
                {
                    targetPos = instanceHead.position;
                    return true;
                }
                targetPos = _unit.TargetUnit.Position;
                return true;
            }
            if (_unit.TargetUnitId != null)
            {
                if (FieldUnitRegistry.TryGetHead(_unit.TargetUnitId, out Transform targetHead))
                {
                    targetPos = targetHead.position;
                    return true;
                }
                foreach (BattleUnitState candidate in _worldData.Controller.Units)
                {
                    if (candidate.UnitId == _unit.TargetUnitId)
                    {
                        targetPos = candidate.Position;
                        return true;
                    }
                }
            }
            targetPos = default;
            return false;
        }

        /// <summary>
        /// 解析停走横向偏移（纯函数，供本能力停走分支与验证脚本共用）：
        /// 沿单位朝向（目的地 − 当前位置，XZ 平面）的垂线随机偏移，幅度在 ±magnitude 区间连续随机。
        /// </summary>
        /// <param name="unit">单位模拟状态；Position/MoveDestination 供朝向计算。</param>
        /// <param name="magnitude">偏移幅度上限（米）；0 或负值返回零向量。</param>
        /// <returns>水平偏移向量（y=0）；朝向无效（原地驻守/同点）返回 Vector3.zero。</returns>
        public static Vector3 ResolveStopLateralOffset(BattleUnitState unit, float magnitude)
        {
            if (magnitude <= 0f) return Vector3.zero;
            Vector3 facing = unit.MoveDestination - unit.Position;
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f) return Vector3.zero;
            facing.Normalize();
            Vector3 right = new Vector3(-facing.z, 0f, facing.x);
            // 连续区间随机（±magnitude 内任意幅度，非"随机左右 + 固定幅度"二值）：与停走距离抖动同口径，
            // 同种敌人停后横移各异，聚堆点错开更自然
            float offset = UnityEngine.Random.Range(-magnitude, magnitude);
            return right * offset;
        }

        // 解析攻击转向节点：rotateNode 空 = 转自身 root；非空 = transform.Find 相对路径（如坦克炮塔）。
        // 路径缺失（配错/战斗 prefab 尚未带该节点）告警一次后回退 root，转向不中断。
        private Transform ResolveFacingNode()
        {
            if (_facingNodeResolved) return _facingNode;
            _facingNodeResolved = true;

            string path = _unit.Stats.RotateNode;
            Transform owner = OwnerContext.Owner.transform;
            if (string.IsNullOrWhiteSpace(path))
            {
                _facingNode = owner;
                return _facingNode;
            }

            Transform found = owner.Find(path);
            if (found == null)
            {
                if (!_facingNodeMissingLogged)
                {
                    _facingNodeMissingLogged = true;
                    Debug.LogWarning(
                        $"[战斗移动能力] '{owner.name}' 转向节点路径 '{path}' 未找到，回退转 root。",
                        owner);
                }
                _facingNode = owner;
                return _facingNode;
            }
            _facingNode = found;
            return _facingNode;
        }

        // 惰性采集依赖：单位状态与 HSM 上下文，并对齐代理出生位置。
        private bool TryAcquireDependencies()
        {
            if (!OwnerContext.TryGet(UnitDataType, out BattleUnitRuntimeData unitData)) return false;
            if (!OwnerContext.TryGet(HsmDataType, out BetterHSMAbilityRuntimeData hsmData)) return false;

            _unit = unitData.Unit;
            _hsmData = hsmData;
            _hasDestination = false;

            // 战场面软依赖：移动不依赖它，仅攻击转向用；未注册则转向静默停摆（移动照常）。
            _worldData = OwnerContext.TryGet(WorldDataType, out BattleWorldRuntimeData worldData) ? worldData : null;

            // 敌人路线制手动位移不需要代理（HSM 已就绪，后续帧走手动分支）。
            if (_unit.Camp == BattleCamp.Enemy) return true;

            // 代理取同物体组件；缺代理属配置缺陷，报错后能力静默停摆（不再重试）。
            _agent = OwnerContext.Owner.GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                Debug.LogError(
                    $"[战斗移动能力] '{OwnerContext.Owner.name}' 缺 NavMeshAgent，寻路与位置回写停摆。",
                    OwnerContext.Owner);
                return true;
            }

            // 首次就绪：把代理对齐 ③ 出生位置（Instantiate 摆位与代理内部状态同步一次）。
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.Warp(_unit.Position);
            return true;
        }
    }
}
