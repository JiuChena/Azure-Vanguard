using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>
    /// 创建 CH0221 分层 BetterHSM 演示状态图的固定注册方案：
    /// locomotion（根，启用门控）包 idle/move，combat（根）包 attack；
    /// locomotion 层挂父层边演示"父层中断先于叶子评估、直接切到子状态"，
    /// 战斗请求边演示跨树切换（整链级联退出/进入）。
    /// </summary>
    public static class CH0221TestHierarchicalRegistration
    {
        /// <summary>
        /// 为当前 GAS 实体创建分层演示状态上下文、状态机和转换关系。
        /// </summary>
        /// <param name="ownerContext">当前 CH0221 实体的 GAS 上下文；不允许为 null。</param>
        /// <returns>已进入 Idle 初始状态（整链 Locomotion→Idle）的分层演示 BetterHSM 运行时包装。</returns>
        public static BetterHSMAbilityContext Create(AbilityOwnerContext ownerContext)
        {
            // 创建当前实体独有的基础上下文（复用输入读取与 BE 行为播放）和分层演示上下文。
            CH0221TestStateContext baseContext = new CH0221TestStateContext(ownerContext);
            CH0221TestHierarchicalStateContext stateContext = new CH0221TestHierarchicalStateContext(baseContext);
            BetterHSM<CH0221TestHierarchicalStateContext> betterHsm = new BetterHSM<CH0221TestHierarchicalStateContext>(stateContext);

            // 创建当前实体独有的状态实例：两个复合根、三个叶子，状态切换后仍持续复用。
            CH0221TestLocomotionState locomotionState = new CH0221TestLocomotionState();
            CH0221TestCombatState combatState = new CH0221TestCombatState();
            CH0221TestHierIdleState idleState = new CH0221TestHierIdleState();
            CH0221TestHierMoveState moveState = new CH0221TestHierMoveState();
            CH0221TestHierAttackState attackState = new CH0221TestHierAttackState();

            // 叶子状态在数据变更处 MarkDirty 父层（事件式门控的业务接线），先注入父复合状态引用。
            idleState.SetLocomotionState(locomotionState);
            moveState.SetLocomotionState(locomotionState);

            // 注册状态节点：先注册根状态，再经 AddChild 挂载子状态（父必须先注册，环结构性不可能）。
            StateGraphBuilder<CH0221TestHierarchicalStateContext> graph = betterHsm.BeginBuild();
            graph.AddState(locomotionState).AddState(combatState);
            graph.AddChild(locomotionState, idleState).AddChild(locomotionState, moveState);
            graph.AddChild(combatState, attackState);

            // 叶子层常规边：输入驱动的待机→移动与移动→待机（locomotion 层干净时叶子自治）。
            graph.From(idleState).To(moveState, moveState.Interrupt, priority: 10);
            graph.From(moveState).To(idleState, idleState.Interrupt, priority: 10);

            // 父层边（核心特性）：locomotion 层先于叶子评估——移动中命中切回待机（父层中断切子状态，
            // 仅 Move.OnExit + Idle.OnEnter，父状态不重入）；待机中命中已激活叶（无操作）。
            graph.From(locomotionState).To(idleState, idleState.Interrupt, priority: 10);

            // 父层战斗请求边：更高优先级，跨树直接切到 combat 分支叶子（整链级联退出/进入）。
            graph.From(locomotionState).To(attackState, stateContext.HasCombatRequest, priority: 50);

            // combat 分支返回边：combat 根未启用门控每帧评估，按输入返回 locomotion 分支。
            graph.From(attackState).To(idleState, idleState.Interrupt, priority: 10);
            graph.From(attackState).To(moveState, moveState.Interrupt, priority: 10);

            // locomotion 启用脏标记门控：数据变更处 MarkDirty 后才评估该层；combat 根保持默认每帧评估。
            graph.EnableInterruptGating(locomotionState);

            // 初始状态必须是叶子：进入时整链自顶向下 OnEnter（Locomotion → Idle）。
            graph.Build(idleState);

            // 只将状态上下文和已构建状态机交回 GAS Runtime 持有。
            return new BetterHSMAbilityContext<CH0221TestHierarchicalStateContext>(stateContext, betterHsm);
        }
    }
}
