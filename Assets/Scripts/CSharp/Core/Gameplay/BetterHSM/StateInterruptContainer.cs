using System;
using System.Collections.Generic;

namespace Core.Gear
{
    /// <summary>
    /// 保存单个来源状态的状态条件和外部条件，并在每帧仲裁唯一胜者。
    /// </summary>
    internal sealed class StateInterruptContainer<TContext>
    {
        // 该状态所有已注入的转换边，顺序即同优先级仲裁顺序。
        private readonly List<StateTransitionBinding<TContext>> _bindings = new List<StateTransitionBinding<TContext>>();
        // 转换边是否已在状态机启动前封口。
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
        /// 完整评估全部转换边并返回优先级最高的有效请求。
        /// </summary>
        /// <param name="request">仲裁胜出的转换请求；没有命中时为默认值。</param>
        /// <returns>存在有效转换请求时返回 true。</returns>
        internal bool TryEvaluate(out StateTransitionRequest<TContext> request)
        {
            request = default;

            // 顺序扫描所有边，不在逐帧热路径排序或创建临时集合。
            for (int index = 0; index < _bindings.Count; index++)
            {
                StateTransitionBinding<TContext> binding = _bindings[index];
                if (!binding.Interrupt()) continue;
                if (request.IsValid && binding.Priority <= request.Priority) continue;

                request = new StateTransitionRequest<TContext>(
                    binding.TargetState,
                    binding.Priority,
                    binding.RegistrationOrder);
            }

            return request.IsValid;
        }

        /// <summary>
        /// 封口当前状态的转换边，阻止运行时结构变更。
        /// </summary>
        internal void Seal()
        {
            _isSealed = true;
        }
    }
}
