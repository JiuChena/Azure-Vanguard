using System;

namespace Core.Gear
{
    /// <summary>
    /// 仅在 HSM 初始化阶段使用的状态节点与转换边组装器。
    /// </summary>
    public sealed class StateGraphBuilder<TContext>
    {
        // 当前正在声明出边的来源状态。
        private StateBase<TContext> _sourceState;
        // 本次组装所属的实体状态机。
        private readonly BetterHSM<TContext> _betterHsm;
        // 组装器是否已经完成构建。
        private bool _isBuilt;

        /// <summary>
        /// 创建归属指定状态机的图组装器。
        /// </summary>
        /// <param name="betterHsm">当前实体的状态机实例。</param>
        internal StateGraphBuilder(BetterHSM<TContext> betterHsm)
        {
            _betterHsm = betterHsm;
        }

        /// <summary>
        /// 注册一个实体专属状态实例。
        /// </summary>
        /// <param name="state">要加入当前状态图的状态实例。</param>
        /// <returns>当前组装器，便于连续注册状态。</returns>
        public StateGraphBuilder<TContext> AddState(StateBase<TContext> state)
        {
            // 构建完成后禁止继续改变状态节点。
            ThrowIfBuilt();
            _betterHsm.RegisterState(state);
            return this;
        }

        /// <summary>
        /// 选择后续 To 调用要写入的来源状态。
        /// </summary>
        /// <param name="sourceState">已经注册到当前状态机的来源状态。</param>
        /// <returns>当前组装器，便于连续声明转换边。</returns>
        public StateGraphBuilder<TContext> From(StateBase<TContext> sourceState)
        {
            // 只允许已注册的当前实体状态作为来源。
            ThrowIfBuilt();
            if (!_betterHsm.ContainsState(sourceState)) throw new InvalidOperationException("转换来源状态必须已注册到当前 HSM。");

            _sourceState = sourceState;
            return this;
        }

        /// <summary>
        /// 为当前来源状态注入一条使用任意无参布尔方法的转换边。
        /// </summary>
        /// <param name="targetState">条件成立后进入的已注册目标状态。</param>
        /// <param name="interrupt">每次仲裁时执行的无副作用中断条件；可以来自目标状态、外部实例、静态方法或闭包。</param>
        /// <param name="priority">转换优先级，数值越大越优先。</param>
        /// <returns>当前组装器，便于继续为同一来源状态添加边。</returns>
        public StateGraphBuilder<TContext> To(
            StateBase<TContext> targetState,
            Func<bool> interrupt,
            int priority)
        {
            // 验证当前边的来源、目标和中断条件。
            ThrowIfBuilt();
            if (_sourceState == null) throw new InvalidOperationException("调用 To 前必须先通过 From 指定来源状态。");
            if (!_betterHsm.ContainsState(targetState)) throw new InvalidOperationException("转换目标状态必须已注册到当前 HSM。");
            if (ReferenceEquals(_sourceState, targetState))
                throw new InvalidOperationException("默认不允许状态转换到自身；需要重入时应明确设计专用状态流程。");
            if (interrupt == null) throw new ArgumentNullException(nameof(interrupt));

            // 将方法来源无关的中断条件直接保存为当前来源状态的一条转换边。
            _sourceState.Interrupts.Add(targetState, interrupt, priority);
            return this;
        }

        /// <summary>
        /// 校验并封口状态图，然后进入指定初始状态。
        /// </summary>
        /// <param name="initialState">已经注册到当前状态机的初始状态。</param>
        public void Build(StateBase<TContext> initialState)
        {
            // 将构建封口委托给状态机执行统一初始化。
            ThrowIfBuilt();
            _betterHsm.Build(initialState);
            _isBuilt = true;
        }

        /// <summary>
        /// 阻止构建完成后继续使用当前组装器。
        /// </summary>
        /// <exception cref="InvalidOperationException">图已经构建时抛出。</exception>
        private void ThrowIfBuilt()
        {
            if (_isBuilt) throw new InvalidOperationException("状态图已构建，不能继续修改。");
        }
    }
}
