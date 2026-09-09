using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>创建 CH0221 最小 BetterHSM 测试状态图的固定注册方案。</summary>
    public static class CH0221TestBetterHSMRegistration
    {
        /// <summary>
        /// 为当前 GAS 实体创建 CH0221 测试状态上下文、状态机和转换关系。
        /// </summary>
        /// <param name="ownerContext">当前 CH0221 实体的 GAS 上下文；不允许为 null。</param>
        /// <returns>已进入 Idle 初始状态的 CH0221 BetterHSM 运行时包装。</returns>
        public static BetterHSMAbilityContext Create(AbilityOwnerContext ownerContext)
        {
            // 创建当前实体独有的状态上下文和状态机。
            CH0221TestStateContext stateContext = new CH0221TestStateContext(ownerContext);
            BetterHSM<CH0221TestStateContext> betterHsm = new BetterHSM<CH0221TestStateContext>(stateContext);

            // 创建当前实体独有的状态实例，状态切换后仍持续复用。
            CH0221TestIdleState idleState = new CH0221TestIdleState();
            CH0221TestMoveState moveState = new CH0221TestMoveState();

            // 注册状态节点和双向转换边，然后明确指定初始状态。
            StateGraphBuilder<CH0221TestStateContext> graph = betterHsm.BeginBuild();
            graph.AddState(idleState).AddState(moveState);
            graph.From(idleState).To(moveState, moveState.Interrupt, priority: 10);
            graph.From(moveState).To(idleState, idleState.Interrupt, priority: 10);
            graph.Build(idleState);

            // 只将状态上下文和已构建状态机交回 GAS Runtime 持有。
            return new BetterHSMAbilityContext<CH0221TestStateContext>(stateContext, betterHsm);
        }
    }
}
