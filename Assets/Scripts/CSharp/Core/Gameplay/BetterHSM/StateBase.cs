namespace Core.Gear
{
    /// <summary>
    /// 状态的持久运行时基类，保存实体上下文和本状态的中断容器。
    /// </summary>
    public abstract class StateBase<TContext>
    {
        // 该状态所属实体的运行时上下文，在状态注册时绑定。
        private TContext _context;
        // 本状态离开时需要仲裁的中断边容器。
        private readonly StateInterruptContainer<TContext> _interrupts = new StateInterruptContainer<TContext>();
        // 状态是否已经被某个 HSM 绑定。
        private bool _isAttached;

        /// <summary>
        /// 获取所属实体的运行时上下文；只能在状态注册后访问。
        /// </summary>
        protected TContext Context => _context;

        /// <summary>
        /// 获取当前状态持有的中断容器。
        /// </summary>
        internal StateInterruptContainer<TContext> Interrupts => _interrupts;

        /// <summary>
        /// 获取状态是否已经绑定到 HSM。
        /// </summary>
        internal bool IsAttached => _isAttached;

        /// <summary>
        /// 绑定状态所属实体的运行时上下文。
        /// </summary>
        /// <param name="context">当前 HSM 绑定的实体上下文。</param>
        internal void Attach(TContext context)
        {
            // 状态实例只能归属一个实体状态机。
            _context = context;
            _isAttached = true;
        }

        /// <summary>
        /// 判断当前实体上下文是否允许进入该状态；仅由已构建 HSM 的中断容器调用。
        /// </summary>
        /// <returns>当前状态可以成为 HSM 下一状态时返回 true。</returns>
        public abstract bool Interrupt();

        /// <summary>
        /// 在所有状态和中断边注册完成后调用一次，用于缓存长期依赖。
        /// </summary>
        public virtual void OnInitialize() { }

        /// <summary>
        /// 在状态成为当前状态时调用，用于开始本轮状态行为。
        /// </summary>
        public virtual void OnEnter() { }

        /// <summary>
        /// 在当前状态未发生中断时由 HSM 每帧调用一次。
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// 在状态离开时调用，用于清理本轮临时状态。
        /// </summary>
        public virtual void OnExit() { }

        /// <summary>
        /// 在所属 HSM 销毁时调用一次，用于释放长期资源。
        /// </summary>
        public virtual void OnDispose() { }
    }
}
