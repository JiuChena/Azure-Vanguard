using System;
using Core.Gear;
using Framework.Gameplay.Battle;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>创建角色战斗状态图的固定注册方案：站姿循环 7 叶 + 门控 + 门控标脏入口注册，含复活回流。
    /// 完成度规则（2026-10-20 拍板）：Idle/Move 可随时中断；分段状态（收势/攻击三段/倒地）必须播完一轮
    /// 才放行转移（SegmentFinished gate），其中循环攻击段按 80% 口径——圈内进度过八成即计一轮完成，
    /// 转出（含收招）不再死等整圈；仅 Downed 立即倒地豁免（priority 50，不读完成度 gate）；
    /// 攻击严格 Start→Attacking（循环）→End，End 播完回行动态、再想打从头重走 Start（无 End→Attacking 直达边）。</summary>
    public static class CharacterCombatRegistration
    {
        /// <summary>
        /// 为当前角色 GAS 实体创建战斗状态上下文、状态机、转换关系和完成度门控。
        /// </summary>
        /// <param name="ownerContext">当前角色单位的 GAS 上下文；不允许为 null。</param>
        /// <returns>已进入 Idle 初始状态的角色 BetterHSM 运行时包装。</returns>
        public static BetterHSMAbilityContext Create(AbilityOwnerContext ownerContext)
        {
            // 创建当前实体独有的状态上下文和状态机。
            CharacterCombatStateContext context = new CharacterCombatStateContext(ownerContext);
            BetterHSM<CharacterCombatStateContext> betterHsm = new BetterHSM<CharacterCombatStateContext>(context);

            // 站姿循环 7 叶：Idle/Move 可中断；收势、攻击三段、倒地为分段状态（完成度 gate）。
            CombatIdleState<CharacterCombatStateContext> idleState = new CombatIdleState<CharacterCombatStateContext>();
            CombatMoveState<CharacterCombatStateContext> moveState = new CombatMoveState<CharacterCombatStateContext>();
            CombatMoveEndState<CharacterCombatStateContext> moveEndState = new CombatMoveEndState<CharacterCombatStateContext>();
            CombatAttackStartState<CharacterCombatStateContext> attackStartState = new CombatAttackStartState<CharacterCombatStateContext>();
            CombatAttackingState<CharacterCombatStateContext> attackingState = new CombatAttackingState<CharacterCombatStateContext>();
            CombatAttackEndState<CharacterCombatStateContext> attackEndState = new CombatAttackEndState<CharacterCombatStateContext>();
            CombatDeathState<CharacterCombatStateContext> deathState = new CombatDeathState<CharacterCombatStateContext>();

            // 技能态（2026-10-27 用户口径：按行为表现有 clip 分配状态——现有技能 clip 即 Skill/EX 单段）
            CombatSkillState<CharacterCombatStateContext> skillState = new CombatSkillState<CharacterCombatStateContext>();

            // 天赋态（2026-11-05）：进场播一次 Talent clip；Once 段，播完才放行，Attack/Skill/Move 不可打断
            CombatTalentState<CharacterCombatStateContext> talentState = new CombatTalentState<CharacterCombatStateContext>();

            // 注册状态节点；转移条件全部是图接线闭包，纯读上下文决策标志与分段完成度。
            StateGraphBuilder<CharacterCombatStateContext> graph = betterHsm.BeginBuild();
            graph.AddState(idleState).AddState(moveState).AddState(moveEndState)
                .AddState(attackStartState).AddState(attackingState).AddState(attackEndState).AddState(deathState)
                .AddState(skillState).AddState(talentState);

            // 倒地豁免完成度：任何状态 Downed 置位立即倒地（priority 50 最高）。
            graph.From(idleState).To(deathState, () => context.Downed, priority: 50);
            graph.From(moveState).To(deathState, () => context.Downed, priority: 50);
            graph.From(moveEndState).To(deathState, () => context.Downed, priority: 50);
            graph.From(attackStartState).To(deathState, () => context.Downed, priority: 50);
            graph.From(attackingState).To(deathState, () => context.Downed, priority: 50);
            graph.From(attackEndState).To(deathState, () => context.Downed, priority: 50);
            graph.From(skillState).To(deathState, () => context.Downed, priority: 50);
            graph.From(talentState).To(deathState, () => context.Downed, priority: 50);

            // 待机⇄移动：可随时中断（可中断状态无完成度 gate），同层平权。
            graph.From(idleState).To(moveState, () => context.IsMoving, priority: 10);
            graph.From(moveState).To(idleState, () => !context.IsMoving, priority: 10);

            // 天赋入口：进场一次性请求，优先级最高（40 > 技能 30 > 攻击 20）；分段的攻击/收势等播完才进（16 > 技能 15）。
            graph.From(idleState).To(talentState, () => context.WantTalent, priority: 40);
            graph.From(moveState).To(talentState, () => context.WantTalent, priority: 40);
            graph.From(moveEndState).To(talentState, () => moveEndState.SegmentFinished && context.WantTalent, priority: 16);
            graph.From(attackStartState).To(talentState, () => attackStartState.SegmentFinished && context.WantTalent, priority: 16);
            graph.From(attackingState).To(talentState, () => attackingState.SegmentFinished && context.WantTalent, priority: 16);
            graph.From(attackEndState).To(talentState, () => attackEndState.SegmentFinished && context.WantTalent, priority: 16);

            // 攻击入口：Idle/Move 可中断直接进起手段（攻击意图优先于移动，20 > 10）。
            graph.From(idleState).To(attackStartState, () => context.WantAttack, priority: 20);
            graph.From(moveState).To(attackStartState, () => context.WantAttack, priority: 20);

            // 技能入口：Idle/Move 技能意图优先于攻击（30 > 20）；技能态 Once 单段播完即回行动态。
            graph.From(idleState).To(skillState, () => context.WantSkill, priority: 30);
            graph.From(moveState).To(skillState, () => context.WantSkill, priority: 30);

            // 优雅中断（"等当前状态走到末尾才允许切下一个状态"，2026-10-27 用户口径）：
            // 技能意图到达时正打/收势 → 当前分段播完（SegmentFinished）才切技能（15 > 回行动态 10），不硬打断。
            graph.From(moveEndState).To(skillState, () => moveEndState.SegmentFinished && context.WantSkill, priority: 15);
            graph.From(attackStartState).To(skillState, () => attackStartState.SegmentFinished && context.WantSkill, priority: 15);
            graph.From(attackingState).To(skillState, () => attackingState.SegmentFinished && context.WantSkill, priority: 15);
            graph.From(attackEndState).To(skillState, () => attackEndState.SegmentFinished && context.WantSkill, priority: 15);

            // 收势段（移动停下过渡）：播完按当时是否在移动回 Move/Idle；收势期间新移动意图等收势完再生效。
            graph.From(moveEndState).To(moveState, () => moveEndState.SegmentFinished && context.IsMoving, priority: 10);
            graph.From(moveEndState).To(idleState, () => moveEndState.SegmentFinished && !context.IsMoving, priority: 10);

            // 起手段播完分流：仍想打进循环段；欲望消失直接收招。
            graph.From(attackStartState).To(attackingState, () => attackStartState.SegmentFinished && context.WantAttack, priority: 10);
            graph.From(attackStartState).To(attackEndState, () => attackStartState.SegmentFinished && !context.WantAttack, priority: 10);

            // 循环段一轮播完且无欲望才收招；仍想打由状态续播留守（不出边）。
            graph.From(attackingState).To(attackEndState, () => attackingState.SegmentFinished && !context.WantAttack, priority: 10);

            // 收招段播完回行动态（严格重走 Start，无 End→Attacking 直达边）；按是否在移动分流。
            graph.From(attackEndState).To(idleState, () => attackEndState.SegmentFinished && !context.IsMoving, priority: 10);
            graph.From(attackEndState).To(moveState, () => attackEndState.SegmentFinished && context.IsMoving, priority: 10);

            // 技能态：Once 单段播完回行动态（按是否移动分流）；技能意图再评估连放（idle 的 WantSkill 边 30 直切）。
            graph.From(skillState).To(idleState, () => skillState.SegmentFinished && !context.IsMoving, priority: 10);
            graph.From(skillState).To(moveState, () => skillState.SegmentFinished && context.IsMoving, priority: 10);

            // 天赋态：Once 单段播完回行动态（按是否移动分流）；播放期间无 Attack/Skill/Move 出边，不可打断。
            graph.From(talentState).To(idleState, () => talentState.SegmentFinished && !context.IsMoving, priority: 10);
            graph.From(talentState).To(moveState, () => talentState.SegmentFinished && context.IsMoving, priority: 10);

            // 复活回流：倒地动画播完且 Downed 解除才回 Idle，重走正常行动循环。
            graph.From(deathState).To(idleState, () => deathState.SegmentFinished && !context.Downed, priority: 10);

            // 启用八叶中断门控：感知能力写标志并标脏、分段状态完成自标脏后，下帧才评估本层中断。
            graph.EnableInterruptGating(idleState, moveState, moveEndState,
                attackStartState, attackingState, attackEndState, deathState, skillState, talentState);
            graph.Build(idleState);

            // 门控标脏入口：感知能力写完决策标志后经此触发各门控层 MarkDirty（闭包绕开泛型擦除，随 HSM 能力销毁）。
            ownerContext.Register(
                AbilityRuntimeDataType.HSMGate,
                new HSMGateRuntimeData(new Action[]
                {
                    () => idleState.MarkDirty(),
                    () => moveState.MarkDirty(),
                    () => moveEndState.MarkDirty(),
                    () => attackStartState.MarkDirty(),
                    () => attackingState.MarkDirty(),
                    () => attackEndState.MarkDirty(),
                    () => deathState.MarkDirty(),
                    () => skillState.MarkDirty(),
                    () => talentState.MarkDirty(),
                }));

            return new BetterHSMAbilityContext<CharacterCombatStateContext>(context, betterHsm);
        }
    }
}
