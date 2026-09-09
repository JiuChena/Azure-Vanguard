using System;

namespace Core.Gear
{
    /// <summary>
    /// 表示一条从当前状态指向目标状态的中断条件转换边。
    /// </summary>
    internal readonly struct StateTransitionBinding<TContext>
    {
        // 条件满足后将进入的既有状态实例。
        internal readonly StateBase<TContext> TargetState;
        // 由目标状态实例提供的无副作用进入条件。
        internal readonly Func<bool> Interrupt;
        // 本边参与仲裁时使用的优先级。
        internal readonly int Priority;
        // 本边加入容器时的稳定顺序。
        internal readonly int RegistrationOrder;

        /// <summary>
        /// 创建一条已完成校验的状态转换边。
        /// </summary>
        /// <param name="targetState">条件成立后进入的目标状态。</param>
        /// <param name="interrupt">目标状态实例的进入条件。</param>
        /// <param name="priority">转换优先级，数值越大越优先。</param>
        /// <param name="registrationOrder">同优先级时使用的稳定注入顺序。</param>
        internal StateTransitionBinding(
            StateBase<TContext> targetState,
            Func<bool> interrupt,
            int priority,
            int registrationOrder)
        {
            TargetState = targetState;
            Interrupt = interrupt;
            Priority = priority;
            RegistrationOrder = registrationOrder;
        }
    }
}
