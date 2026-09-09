using System;
using System.Collections.Generic;

namespace Core.Gear
{
    /// <summary>
    /// 为单个实体持有状态实例并执行中断仲裁的纯 C# 状态机。
    /// </summary>
    public sealed class BetterHSM<TContext> : IDisposable
    {
        // 状态运行时读取的实体专属上下文。
        private readonly TContext _context;
        // 状态类型到该实体状态实例的注册表。
        private readonly Dictionary<Type, StateBase<TContext>> _states = new Dictionary<Type, StateBase<TContext>>();
        // 用于稳定初始化和销毁顺序的状态列表。
        private readonly List<StateBase<TContext>> _stateList = new List<StateBase<TContext>>();
        // 当前处于激活状态的实例。
        private StateBase<TContext> _currentState;
        // 状态图是否已经完成构建并封口。
        private bool _isBuilt;
        // 状态机是否已经销毁。
        private bool _isDisposed;

        /// <summary>
        /// 创建绑定一个实体上下文的状态机。
        /// </summary>
        /// <param name="context">该实体专属的运行时上下文。</param>
        public BetterHSM(TContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取该状态机绑定的实体上下文。
        /// </summary>
        public TContext Context => _context;

        /// <summary>
        /// 获取当前激活的状态；构建前或尚未进入初始状态时返回 null。
        /// </summary>
        public StateBase<TContext> CurrentState => _currentState;

        /// <summary>
        /// 开始为该状态机构建状态节点和中断边。
        /// </summary>
        /// <returns>只能用于本次初始化的状态图组装器。</returns>
        /// <exception cref="InvalidOperationException">状态图已构建或状态机已销毁时抛出。</exception>
        public StateGraphBuilder<TContext> BeginBuild()
        {
            // 阻止销毁后的任何运行时访问。
            ThrowIfDisposed();
            if (_isBuilt) throw new InvalidOperationException("HSM 状态图已构建，不能再次开始构建。");

            return new StateGraphBuilder<TContext>(this);
        }

        /// <summary>
        /// 推进当前实体状态的一次帧更新，并最多执行一次状态切换。
        /// </summary>
        /// <exception cref="InvalidOperationException">状态图尚未构建时抛出。</exception>
        public void Update()
        {
            // 状态图未封口时不允许进入热路径。
            ThrowIfDisposed();
            if (!_isBuilt) throw new InvalidOperationException("HSM 必须在状态图构建完成后才能调用 Update。");
            if (_currentState == null) return;

            // 先完整仲裁当前状态的所有边，避免条件书写顺序决定结果。
            if (!_currentState.Interrupts.TryEvaluate(out StateTransitionRequest<TContext> request))
            {
                _currentState.OnUpdate();
                return;
            }

            // 只由状态机执行生命周期切换，本帧不再更新目标状态。
            _currentState.OnExit();
            _currentState = request.TargetState;
            _currentState.OnEnter();
        }

        /// <summary>
        /// 销毁状态机，并释放所有持久状态持有的资源。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            // 只有完整初始化过的状态才接收销毁生命周期。
            if (_isBuilt)
            {
                _currentState?.OnExit();
                for (int index = 0; index < _stateList.Count; index++) _stateList[index].OnDispose();
            }

            _currentState = null;
            _states.Clear();
            _stateList.Clear();
            _isDisposed = true;
        }

        /// <summary>
        /// 将状态绑定到本状态机并加入初始化列表。
        /// </summary>
        /// <param name="state">需要注册的实体专属状态实例。</param>
        /// <exception cref="ArgumentNullException">状态为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">同类型状态重复注册或状态已绑定其他状态机时抛出。</exception>
        internal void RegisterState(StateBase<TContext> state)
        {
            // 验证状态节点可安全归属到当前图。
            ThrowIfDisposed();
            if (_isBuilt) throw new InvalidOperationException("HSM 状态图已构建，不能继续注册状态。");
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_states.ContainsKey(state.GetType()))
                throw new InvalidOperationException($"HSM 不允许重复注册状态类型：{state.GetType().FullName}。");
            if (state.IsAttached)
                throw new InvalidOperationException($"状态 {state.GetType().FullName} 已经绑定到其他 HSM。");

            // 绑定上下文后记录该实体专属状态实例。
            state.Attach(_context);
            _states.Add(state.GetType(), state);
            _stateList.Add(state);
        }

        /// <summary>
        /// 判断状态是否属于当前状态机。
        /// </summary>
        /// <param name="state">待检查的状态实例。</param>
        /// <returns>状态已注册到当前状态机时返回 true。</returns>
        internal bool ContainsState(StateBase<TContext> state)
        {
            return state != null && _states.TryGetValue(state.GetType(), out StateBase<TContext> registeredState) &&
                ReferenceEquals(registeredState, state);
        }

        /// <summary>
        /// 完成状态图初始化、封口所有边并进入初始状态。
        /// </summary>
        /// <param name="initialState">已注册到当前状态机的初始状态。</param>
        /// <exception cref="InvalidOperationException">初始状态无效或状态图已构建时抛出。</exception>
        internal void Build(StateBase<TContext> initialState)
        {
            // 验证图具备一个属于当前实体的初始状态。
            ThrowIfDisposed();
            if (_isBuilt) throw new InvalidOperationException("HSM 状态图已构建，不能重复构建。");
            if (!ContainsState(initialState)) throw new InvalidOperationException("初始状态必须已注册到当前 HSM。");

            // 先完成全部状态的一次性初始化，再封口中断容器。
            for (int index = 0; index < _stateList.Count; index++) _stateList[index].OnInitialize();
            for (int index = 0; index < _stateList.Count; index++) _stateList[index].Interrupts.Seal();

            // 只有初始化完全完成后才允许进入运行时更新。
            _isBuilt = true;
            _currentState = initialState;
            _currentState.OnEnter();
        }

        /// <summary>
        /// 在状态机销毁后阻止继续访问其状态图。
        /// </summary>
        /// <exception cref="ObjectDisposedException">状态机已经销毁时抛出。</exception>
        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(BetterHSM<TContext>));
        }
    }
}
