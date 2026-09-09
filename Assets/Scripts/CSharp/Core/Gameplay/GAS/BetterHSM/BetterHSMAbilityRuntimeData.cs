namespace Framework.Gameplay.Abilities
{
    /// <summary>保存 BetterHSM 能力向其他能力公开的运行时数据。</summary>
    public sealed class BetterHSMAbilityRuntimeData : IAbilityRuntimeData
    {
        // 当前实体 BetterHSM 能力创建并持有的运行时包装。
        private readonly BetterHSMAbilityContext _abilityContext;

        /// <summary>
        /// 创建当前实体 BetterHSM 的公开运行时数据。
        /// </summary>
        /// <param name="abilityContext">当前实体已经构建完成的状态机运行时包装；不允许为 null。</param>
        /// <exception cref="System.ArgumentNullException">状态机运行时包装为空时抛出。</exception>
        public BetterHSMAbilityRuntimeData(BetterHSMAbilityContext abilityContext)
        {
            // 验证运行时数据始终指向有效的当前实体状态机。
            if (abilityContext == null) throw new System.ArgumentNullException(nameof(abilityContext));

            _abilityContext = abilityContext;
        }

        /// <summary>
        /// 获取当前实体 BetterHSM 的运行时包装。
        /// </summary>
        /// <remarks>允许读取状态上下文和当前状态；Update 与 Dispose 只能由 BetterHSMAbilityRuntime 调用。</remarks>
        public BetterHSMAbilityContext AbilityContext => _abilityContext;

        /// <summary>
        /// 保持状态机持久化状态，不在 GAS 的通用数据重置阶段重新构建或切换状态。
        /// </summary>
        public void Reset()
        {
        }
    }
}
