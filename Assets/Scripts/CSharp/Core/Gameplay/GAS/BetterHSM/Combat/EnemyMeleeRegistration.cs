using System;
using Core.Gear;
using Framework.Gameplay.Battle;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>创建敌人战斗状态图的固定注册方案：站姿循环 7 叶 + 门控 + 门控标脏入口注册，含复活回流。
    /// 与角色侧 <see cref="CharacterCombatRegistration"/> 同构（同一组泛型共享状态，仅上下文类型不同）；
    /// 完成度规则同角色（2026-10-20 拍板）：Idle/Move 可随时中断；分段状态（收势/攻击三段/倒地）必须
    /// 播完一轮才放行转移（SegmentFinished gate），其中循环攻击段按 80% 口径——圈内进度过八成即计一轮
    /// 完成，转出（含收招）不再死等整圈；仅 Downed 立即倒地豁免（priority 50，不读完成度 gate）；
    /// 攻击严格 Start→Attacking（循环）→End，End 播完回行动态、再想打从头重走 Start（无 End→Attacking 直达边）。
    /// 敌人当前死亡即被 ③ 清理回池，Death 叶与复活回流按角色同构保留（敌人可倒地机制将来接入直接可用）。</summary>
    public static class EnemyMeleeRegistration
    {
        /// <summary>
        /// 为当前敌人 GAS 实体创建战斗状态上下文、状态机、转换关系和完成度门控。
        /// </summary>
        /// <param name="ownerContext">当前敌人单位的 GAS 上下文；不允许为 null。</param>
        /// <returns>已进入 Move 初始状态的敌人 BetterHSM 运行时包装（敌人出生即机动语义）。</returns>
        public static BetterHSMAbilityContext Create(AbilityOwnerContext ownerContext)
        {
            // 创建当前实体独有的状态上下文和状态机。
            EnemyMeleeStateContext context = new EnemyMeleeStateContext(ownerContext);
            BetterHSM<EnemyMeleeStateContext> betterHsm = new BetterHSM<EnemyMeleeStateContext>(context);

            // 站姿循环 7 叶：Idle/Move 可中断；收势、攻击三段、倒地为分段状态（完成度 gate）。
            CombatIdleState<EnemyMeleeStateContext> idleState = new CombatIdleState<EnemyMeleeStateContext>();
            CombatMoveState<EnemyMeleeStateContext> moveState = new CombatMoveState<EnemyMeleeStateContext>();
            CombatMoveEndState<EnemyMeleeStateContext> moveEndState = new CombatMoveEndState<EnemyMeleeStateContext>();
            CombatAttackStartState<EnemyMeleeStateContext> attackStartState = new CombatAttackStartState<EnemyMeleeStateContext>();
            CombatAttackingState<EnemyMeleeStateContext> attackingState = new CombatAttackingState<EnemyMeleeStateContext>();
            CombatAttackEndState<EnemyMeleeStateContext> attackEndState = new CombatAttackEndState<EnemyMeleeStateContext>();
            CombatDeathState<EnemyMeleeStateContext> deathState = new CombatDeathState<EnemyMeleeStateContext>();

            // 注册状态节点；转移条件全部是图接线闭包，纯读上下文决策标志与分段完成度。
            StateGraphBuilder<EnemyMeleeStateContext> graph = betterHsm.BeginBuild();
            graph.AddState(idleState).AddState(moveState).AddState(moveEndState)
                .AddState(attackStartState).AddState(attackingState).AddState(attackEndState).AddState(deathState);

            // 倒地豁免完成度：任何状态 Downed 置位立即倒地（priority 50 最高）。
            graph.From(idleState).To(deathState, () => context.Downed, priority: 50);
            graph.From(moveState).To(deathState, () => context.Downed, priority: 50);
            graph.From(moveEndState).To(deathState, () => context.Downed, priority: 50);
            graph.From(attackStartState).To(deathState, () => context.Downed, priority: 50);
            graph.From(attackingState).To(deathState, () => context.Downed, priority: 50);
            graph.From(attackEndState).To(deathState, () => context.Downed, priority: 50);

            // 待机⇄移动：可随时中断（可中断状态无完成度 gate），同层平权。
            graph.From(idleState).To(moveState, () => context.IsMoving, priority: 10);
            graph.From(moveState).To(idleState, () => !context.IsMoving, priority: 10);

            // 攻击入口：Idle/Move 可中断直接进起手段（攻击意图优先于移动，20 > 10）。
            graph.From(idleState).To(attackStartState, () => context.WantAttack, priority: 20);
            graph.From(moveState).To(attackStartState, () => context.WantAttack, priority: 20);

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

            // 复活回流：倒地动画播完且 Downed 解除才回 Idle，重走正常行动循环。
            graph.From(deathState).To(idleState, () => deathState.SegmentFinished && !context.Downed, priority: 10);

            // 启用七叶中断门控：感知能力写标志并标脏、分段状态完成自标脏后，下帧才评估本层中断。
            graph.EnableInterruptGating(idleState, moveState, moveEndState,
                attackStartState, attackingState, attackEndState, deathState);
            graph.Build(moveState);

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
                }));

            return new BetterHSMAbilityContext<EnemyMeleeStateContext>(context, betterHsm);
        }
    }
}
