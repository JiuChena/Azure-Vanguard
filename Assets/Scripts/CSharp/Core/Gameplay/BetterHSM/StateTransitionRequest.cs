namespace Core.Gear
{
    /// <summary>
    /// 保存当前帧仲裁胜出的状态转换结果。
    /// </summary>
    public readonly struct StateTransitionRequest<TContext>
    {
        // 胜出后将成为当前状态的既有实例。
        private readonly StateBase<TContext> _targetState;
        // 胜出边的优先级。
        private readonly int _priority;
        // 胜出边的稳定注入顺序。
        private readonly int _registrationOrder;

        /// <summary>
        /// 创建一次有效的状态转换请求。
        /// </summary>
        /// <param name="targetState">条件成立后进入的目标状态。</param>
        /// <param name="priority">胜出边的优先级。</param>
        /// <param name="registrationOrder">胜出边的稳定注入顺序。</param>
        internal StateTransitionRequest(StateBase<TContext> targetState, int priority, int registrationOrder)
        {
            _targetState = targetState;
            _priority = priority;
            _registrationOrder = registrationOrder;
        }

        /// <summary>
        /// 获取条件成立后将进入的目标状态。
        /// </summary>
        public StateBase<TContext> TargetState => _targetState;

        /// <summary>
        /// 获取胜出边的优先级。
        /// </summary>
        public int Priority => _priority;

        /// <summary>
        /// 获取同优先级仲裁使用的注入顺序。
        /// </summary>
        public int RegistrationOrder => _registrationOrder;

        /// <summary>
        /// 获取该请求是否包含有效目标状态。
        /// </summary>
        public bool IsValid => _targetState != null;
    }
}
