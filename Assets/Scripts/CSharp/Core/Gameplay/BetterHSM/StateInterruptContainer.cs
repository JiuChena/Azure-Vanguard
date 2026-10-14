using System;
using System.Collections.Generic;

namespace Core.Gear
{
    /// <summary>
    /// 保存单个来源状态的中断边，并在每帧仲裁唯一胜者。
    /// Seal 时按 优先级降序、同优先级按注册序升序 一次性排序，此后运行期只做首真即停扫描——
    /// 首个真边即旧"全量打擂台"语义（取最高优先级、同级先注册赢）下的同一胜者，
    /// 评估次数只减不增；前提是中断条件无副作用（既有契约）。
    /// </summary>
    internal sealed class StateInterruptContainer<TContext>
    {
        // 排序比较器：构建期 Seal 时使用一次，静态缓存避免委托分配。
        private static readonly Comparison<StateTransitionBinding<TContext>> PriorityOrderComparison =
            CompareByPriorityDescending;

        // 该状态所有已注入的转换边，Seal 后按仲裁顺序排列。
        private readonly List<StateTransitionBinding<TContext>> _bindings = new List<StateTransitionBinding<TContext>>();
        // 容器所属的状态实例，评估命中时随请求带出，用于定位来源层。
        private StateBase<TContext> _owner;
        // 转换边是否已在状态机启动前封口并完成排序。
        private bool _isSealed;

        /// <summary>
        /// 向当前状态加入一条转换边。
        /// </summary>
        /// <param name="targetState">条件成立后进入的目标状态。</param>
        /// <param name="interrupt">状态自身或外部提供的无副作用中断方法。</param>
        /// <param name="priority">转换优先级，数值越大越优先。</param>
        /// <exception cref="InvalidOperationException">容器已封口或重复注入同一边时抛出。</exception>
        internal void Add(StateBase<TContext> targetState, Func<bool> interrupt, int priority)
        {
            // 运行期不允许变更转换结构。
            if (_isSealed) throw new InvalidOperationException("状态中断容器已封口，不能继续注入转换边。");
            if (targetState == null) throw new ArgumentNullException(nameof(targetState));
            if (interrupt == null) throw new ArgumentNullException(nameof(interrupt));
            // 拒绝同一来源上的重复边，避免重复判断和隐式优先级冲突。
            for (int index = 0; index < _bindings.Count; index++)
            {
                StateTransitionBinding<TContext> binding = _bindings[index];
                if (binding.TargetState != targetState || binding.Interrupt != interrupt || binding.Priority != priority)
                    continue;

                throw new InvalidOperationException("同一状态不能重复注入相同的目标状态、进入条件和优先级。");
            }

            _bindings.Add(new StateTransitionBinding<TContext>(targetState, interrupt, priority, _bindings.Count));
        }

        /// <summary>
        /// 按仲裁顺序扫描转换边并返回首个真边：预排序保证它就是优先级最高的有效请求
        /// （同优先级取先注册者），命中即止，排名靠后的边不再执行。
        /// </summary>
        /// <param name="request">仲裁胜出的转换请求；没有命中时为默认值。</param>
        /// <returns>存在有效转换请求时返回 true。</returns>
        internal bool TryEvaluate(out StateTransitionRequest<TContext> request)
        {
            for (int index = 0; index < _bindings.Count; index++)
            {
                StateTransitionBinding<TContext> binding = _bindings[index];
                if (!binding.Interrupt()) continue;

                request = new StateTransitionRequest<TContext>(
                    binding.TargetState,
                    binding.Priority,
                    binding.RegistrationOrder,
                    _owner);
                return true;
            }

            request = default;
            return false;
        }

        /// <summary>
        /// 封口当前状态的转换边：先按 优先级降序、同优先级按注册序升序 完成一次性排序
        /// （RegistrationOrder 唯一 → 全序，结果确定），再阻止运行时结构变更。
        /// </summary>
        internal void Seal()
        {
            // 显式大小比较而非减法，规避 int 溢出。
            _bindings.Sort(PriorityOrderComparison);
            _isSealed = true;
        }

        /// <summary>
        /// 记录容器所属的状态实例（注册期调用一次）。
        /// </summary>
        internal void SetOwner(StateBase<TContext> owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 获取已注入的转换边数量（构建期校验用）。
        /// </summary>
        internal int BindingCount => _bindings.Count;

        /// <summary>
        /// 获取指定下标转换边的目标状态（构建期校验用）。
        /// </summary>
        internal StateBase<TContext> GetBindingTarget(int index)
        {
            return _bindings[index].TargetState;
        }

        /// <summary>
        /// 优先级降序、同优先级按注册序升序的比较器。
        /// </summary>
        private static int CompareByPriorityDescending(
            StateTransitionBinding<TContext> left,
            StateTransitionBinding<TContext> right)
        {
            if (left.Priority != right.Priority) return left.Priority > right.Priority ? -1 : 1;
            return left.RegistrationOrder < right.RegistrationOrder ? -1 : 1;
        }
    }
}
