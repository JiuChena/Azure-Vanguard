namespace Core.Gear
{
    /// <summary>
    /// 状态的持久运行时基类，保存实体上下文、父子关系和本状态的中断容器。
    /// 状态可声明父状态构成分层图：激活链上的状态参与生命周期，复合状态（有子状态）
    /// 是结构节点，只经激活链执行 OnEnter/OnExit，不接收 OnUpdate，
    /// 也不可作为转换边目标或初始状态（构建期校验）。
    /// </summary>
    public abstract class StateBase<TContext>
    {
        // 该状态所属实体的运行时上下文，在状态注册时绑定。
        private TContext _context;
        // 本状态离开时需要仲裁的中断边容器。
        private readonly StateInterruptContainer<TContext> _interrupts = new StateInterruptContainer<TContext>();
        // 父状态引用，注册期由 HSM 唯一一次写入；根状态为 null。
        private StateBase<TContext> _parent;
        // 已挂载的子状态数量，用于构建期"边目标与初始状态必须是叶子"校验。
        private int _childCount;
        // 是否已启用中断脏标记门控：启用后仅 MarkDirty 之后的帧才评估本层中断。
        private bool _isInterruptGatingEnabled;
        // 脏标记：同帧多次 MarkDirty 置位幂等（天然合并），评估消费即清。
        private bool _isDirty;
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
        /// 获取父状态；根状态返回 null。
        /// </summary>
        internal StateBase<TContext> Parent => _parent;

        /// <summary>
        /// 获取已挂载的子状态数量。
        /// </summary>
        internal int ChildCount => _childCount;

        /// <summary>
        /// 获取状态是否已经绑定到 HSM。
        /// </summary>
        internal bool IsAttached => _isAttached;

        /// <summary>
        /// 获取本层中断是否需要参与本帧仲裁：未启用门控时恒评估（向后兼容）；
        /// 启用门控后仅在 MarkDirty 之后的帧评估。
        /// </summary>
        internal bool ShouldEvaluateInterrupts => !_isInterruptGatingEnabled || _isDirty;

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
        /// 写入父状态并累计父状态的子状态数；注册期由 HSM 调用唯一一次，parent 为 null 表示根状态。
        /// </summary>
        /// <param name="parent">已注册的父状态；根状态传 null。</param>
        internal void SetParent(StateBase<TContext> parent)
        {
            _parent = parent;
            if (parent != null) parent._childCount++;
        }

        /// <summary>
        /// 启用中断脏标记门控：之后仅 MarkDirty 的帧才评估本层中断（由构建器在构建期调用）。
        /// </summary>
        internal void EnableInterruptGating()
        {
            _isInterruptGatingEnabled = true;
        }

        /// <summary>
        /// 业务侧标记本状态关心的数据已变化，下一帧将评估本层中断。
        /// 与状态是否激活无关：数据变更即标，激活后首帧自会评估未消费的脏。
        /// 同帧多次调用合并为一次评估，零分配。
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 消费并清零脏标记；每帧评估前由 HSM 调用，消费即清（防卡脏）。
        /// </summary>
        internal void ConsumeDirty()
        {
            _isDirty = false;
        }

        /// <summary>
        /// 判断当前实体上下文是否允许进入该状态；仅作为注册工厂显式接入的进入条件约定
        /// （框架从不隐式调用）。复合状态永不成为边目标，保持默认实现即可；
        /// 叶子状态应重写以声明进入条件。
        /// </summary>
        /// <returns>当前状态可以成为 HSM 下一状态时返回 true。</returns>
        public virtual bool Interrupt() => false;

        /// <summary>
        /// 在所有状态和中断边注册完成后调用一次，用于缓存长期依赖。
        /// </summary>
        public virtual void OnInitialize() { }

        /// <summary>
        /// 在状态进入激活链时调用（自上而下级联，复合状态经链进入时也会执行），用于开始本轮状态行为。
        /// </summary>
        public virtual void OnEnter() { }

        /// <summary>
        /// 在当前帧未发生中断时由 HSM 调用一次；仅激活链叶子接收本回调，复合状态不接收。
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// 在状态离开激活链时调用（自下而上级联，复合状态经链离开时也会执行），用于清理本轮状态行为。
        /// </summary>
        public virtual void OnExit() { }

        /// <summary>
        /// 在所属 HSM 销毁时调用一次，用于释放长期资源。
        /// </summary>
        public virtual void OnDispose() { }
    }
}
