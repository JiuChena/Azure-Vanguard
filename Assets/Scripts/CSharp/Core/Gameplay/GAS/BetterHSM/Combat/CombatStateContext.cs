using Framework.Gameplay.Battle;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>技能槽位（感知层写意图时定；BHSM skill 态按此播 E/Q 前缀 clip；None = 无）。</summary>
    public enum SkillSlot
    {
        None,
        E,
        Q,
    }

    /// <summary>战斗状态上下文基类：包装单位 GAS 上下文，向共享战斗状态提供 BE 播放入口与决策标志
    /// （感知能力写、转移中断纯读；技能等决策位按需加字段）。</summary>
    public class CombatStateContext
    {
        // 当前单位所属的 GAS 上下文。
        private readonly AbilityOwnerContext _ownerContext;

        // 攻击索敌策略（仅 AttackStart/Attacking 经 AcquireAttackTarget 调用；策略读 CharacterTable.targetingPolicy）
        private readonly IUnitIntentStrategy _attackTargetingStrategy = new DefaultTargetingStrategy();

        /// <summary>
        /// 创建战斗状态上下文。
        /// </summary>
        /// <param name="ownerContext">当前单位的 GAS 上下文；不允许为 null。</param>
        /// <exception cref="System.ArgumentNullException">GAS 上下文为空时抛出。</exception>
        public CombatStateContext(AbilityOwnerContext ownerContext)
        {
            if (ownerContext == null) throw new System.ArgumentNullException(nameof(ownerContext));

            _ownerContext = ownerContext;
        }

        /// <summary>获取当前单位所属的 GAS 上下文。</summary>
        public AbilityOwnerContext OwnerContext => _ownerContext;

        /// <summary>是否有攻击意图（含打守点）；感知按 ③ HasAttackIntent 镜像，→Attack 转移据此仲裁。</summary>
        public bool WantAttack;

        /// <summary>是否在移动中；感知按 ③ 意图目的地与当前位置的距离阈值判定，Idle⇄Move 转移据此仲裁。</summary>
        public bool IsMoving;

        /// <summary>是否倒地；感知镜像 ③ Vital，死亡/复活转移据此仲裁。</summary>
        public bool Downed;

        /// <summary>是否有技能意图（感知层镜像：E CD 就绪 / Q 能量满；→Skill 转移据此仲裁，技能优先于攻击）。</summary>
        public bool WantSkill;

        /// <summary>是否请求播放一次天赋（感知层镜像；→Talent 转移据此仲裁，Talent 播放期间不可被 Attack/Skill/Move 打断）。</summary>
        public bool WantTalent;

        /// <summary>待放技能槽位（感知层镜像时写；BHSM skill 态按此选 E/Q 前缀 clip）。</summary>
        public SkillSlot PendingSkillSlot;

        /// <summary>
        /// 按叶子状态键播放当前单位 BE 行为；BE 行为能力未挂载时忽略本次请求。
        /// </summary>
        /// <param name="behaviorKey">叶子状态类中声明的 BehaviorKey 常量。</param>
        public void PlayBehavior(string behaviorKey)
        {
            // 状态不直接依赖 BE 能力：未挂载时视为表现层缺失，仅提示不中断逻辑。
            if (!_ownerContext.TryGet(
                    AbilityRuntimeDataType.Behavior,
                    out BehaviorAbilityRuntimeData behaviorData))
            {
                Debug.LogWarning($"[Combat HSM] 未挂载 BE 行为能力，忽略播放请求 '{behaviorKey}'", _ownerContext.Owner);
                return;
            }

            behaviorData.Play(behaviorKey);
        }

        /// <summary>
        /// 停止当前单位 BE 行为播放；BE 行为能力未挂载时忽略。
        /// </summary>
        public void StopBehavior()
        {
            if (_ownerContext.TryGet(
                    AbilityRuntimeDataType.Behavior,
                    out BehaviorAbilityRuntimeData behaviorData))
            {
                behaviorData.Stop();
            }
        }

        /// <summary>当前 BE 行为是否播放中；BE 未挂载时恒 false（分段状态完成度 gate 立即放行，表现缺失不卡逻辑）。</summary>
        public bool IsBehaviorPlaying =>
            _ownerContext.TryGet(AbilityRuntimeDataType.Behavior, out BehaviorAbilityRuntimeData playingData)
            && playingData.IsPlaying;

        /// <summary>当前 BE 行为归一化进度 0~1；BE 未挂载时恒 1（Loop 段视为已完成轮）。</summary>
        public float BehaviorNormalizedTime =>
            _ownerContext.TryGet(AbilityRuntimeDataType.Behavior, out BehaviorAbilityRuntimeData normalizedData)
                ? normalizedData.NormalizedTime
                : 1f;

        /// <summary>当前 BE 行为配置时长（秒）；BE 未挂载时恒 0（Loop 段表现缺失兜底判据）。</summary>
        public float BehaviorDuration =>
            _ownerContext.TryGet(AbilityRuntimeDataType.Behavior, out BehaviorAbilityRuntimeData durationData)
                ? durationData.Duration
                : 0f;

        /// <summary>
        /// 技能施放结算（模拟层，2026-10-28 表驱动能量经济）：重设该技能 CD（CharacterSkillTable.Cooldown）+
        /// 技能释放完回能（CharacterSkillTable.energyGain，配 0 不回能）+ Q 大招消耗满能量。
        /// </summary>
        /// <param name="slot">本次施放的技能槽位。</param>
        public void StartSkillCast(SkillSlot slot)
        {
            if (!_ownerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData)) return;
            if (!_ownerContext.TryGet(AbilityRuntimeDataType.BattleWorld, out BattleWorldRuntimeData worldData)) return;

            BattleUnitState unit = unitData.Unit;
            int skillId = slot == SkillSlot.Q ? unit.Stats.SkillQId : unit.Stats.SkillEId;
            if (skillId <= 0) return;

            if (BattleDataModule.TryGetSkillCooldown(skillId, out float cooldown))
            {
                worldData.Controller.Cooldowns.StartCooldown(unit, skillId, cooldown);
            }
            // 技能释放完回能（表驱动：技能行 energyGain，E=10 / EX=0 可配——配 0 不回能）
            if (BattleDataModule.TryGetSkillEnergyGain(skillId, out int castGain) && castGain > 0)
            {
                worldData.Controller.Resources.AddEnergy(unit, castGain);
            }
            // Q 大招消耗满能量（塔防常规）
            if (slot == SkillSlot.Q) worldData.Controller.Resources.SpendEnergy(unit, unit.MaxEnergy);

            // 释放技能锁定（效果执行器经此读 effectIds；技能态结束 StopCastingSkill 清除）
            unit.CastingSkillId = skillId;
        }

        /// <summary>
        /// 攻击索敌（2026-11-06）：仅 AttackStart / Attacking 每轮开始时调用。
        /// 走现有 DefaultTargetingStrategy（CharacterTable.targetingPolicy）；敌人策略内部自守卫，不改敌人目标。
        /// </summary>
        public void AcquireAttackTarget()
        {
            if (!_ownerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData)
                || unitData.Unit == null) return;
            // 敌人目标由 EnemyMeleeIntentStrategy 维护，不走角色索敌链路
            if (unitData.Unit.Camp != BattleCamp.Character) return;
            if (!_ownerContext.TryGet(AbilityRuntimeDataType.BattleWorld, out BattleWorldRuntimeData worldData)
                || worldData.Controller == null) return;

            // 表 target 不是 single_target（self/team/AoE）→ 本招式不需要单目标，清掉目标
            if (!SkillTargetPolicy.RequiresTarget(unitData.Unit.Stats.NormalAtkId))
            {
                unitData.Unit.ClearCombatTarget();
                return;
            }

            var intent = new BattleIntentContext { Units = worldData.Controller.Units };
            _attackTargetingStrategy.UpdateIntent(unitData.Unit, intent);
        }

        /// <summary>清空当前动作目标（攻击/技能结束；目标死亡/出表时由控制器统一调用）。</summary>
        public void ClearCombatTarget()
        {
            if (_ownerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData)
                && unitData.Unit != null)
            {
                unitData.Unit.ClearCombatTarget();
            }
        }

        /// <summary>天赋态进入时消耗一次性请求（避免播完后再次进入）。</summary>
        public void ConsumeTalentRequest()
        {
            if (_ownerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData)
                && unitData.Unit != null)
            {
                unitData.Unit.WantTalent = false;
            }
        }

        /// <summary>技能态结束清除释放锁定（效果执行器只在该技能播放窗口读 CastingSkillId）。</summary>
        public void StopCastingSkill()
        {
            if (_ownerContext.TryGet(AbilityRuntimeDataType.BattleUnit, out BattleUnitRuntimeData unitData)
                && unitData.Unit != null)
            {
                unitData.Unit.CastingSkillId = 0;
            }
        }
    }
}
