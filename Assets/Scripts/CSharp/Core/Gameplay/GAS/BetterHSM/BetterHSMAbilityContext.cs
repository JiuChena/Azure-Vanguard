using System;
using Core.Gear;

namespace Framework.Gameplay.Abilities
{
    /// <summary>包装单个实体已经构建完成的 BetterHSM 及其状态上下文。</summary>
    public abstract class BetterHSMAbilityContext
    {
        /// <summary>
        /// 获取当前状态机实际使用的状态上下文实例。
        /// </summary>
        /// <remarks>仅用于读取跨角色联动所需的数据；调用方应先确认实际上下文类型，且不得借此驱动或销毁状态机。</remarks>
        public abstract object StateContext { get; }

        /// <summary>
        /// 获取当前状态机实际使用的状态上下文类型。
        /// </summary>
        public abstract Type StateContextType { get; }

        /// <summary>
        /// 获取当前激活的状态实例；状态机已销毁或尚未进入初始状态时返回 null。
        /// </summary>
        /// <remarks>仅用于状态读取和类型判断；状态切换只能由 BetterHSMAbilityRuntime 驱动。</remarks>
        public abstract object CurrentState { get; }

        /// <summary>
        /// 获取当前激活状态的类型；状态机已销毁或尚未进入初始状态时返回 null。
        /// </summary>
        public abstract Type CurrentStateType { get; }

        /// <summary>
        /// 推进当前实体状态机的一次普通帧更新。
        /// </summary>
        /// <remarks>只允许所属 BetterHSMAbilityRuntime 调用；外部调用会导致同一帧重复仲裁。</remarks>
        internal abstract void Update();

        /// <summary>
        /// 销毁当前实体状态机及其持有的状态实例。
        /// </summary>
        /// <remarks>只允许所属 BetterHSMAbilityRuntime 在能力销毁时调用。</remarks>
        internal abstract void Dispose();
    }

    /// <summary>以强类型方式保存单个实体 BetterHSM 运行时对象的包装结果。</summary>
    /// <typeparam name="TStateContext">当前实体状态图使用的具体状态上下文类型。</typeparam>
    public sealed class BetterHSMAbilityContext<TStateContext> : BetterHSMAbilityContext
    {
        // 当前实体状态图使用的强类型状态上下文。
        private readonly TStateContext _stateContext;
        // 当前实体已经完成构建的强类型状态机。
        private readonly BetterHSM<TStateContext> _betterHsm;

        /// <summary>
        /// 创建一个已完成状态图构建的 BetterHSM 能力运行时包装。
        /// </summary>
        /// <param name="stateContext">当前实体独有的状态机上下文；不允许为 null。</param>
        /// <param name="betterHsm">当前实体独有且已完成 Build 的状态机；不允许为 null。</param>
        /// <exception cref="ArgumentNullException">状态上下文或状态机为空时抛出。</exception>
        public BetterHSMAbilityContext(TStateContext stateContext, BetterHSM<TStateContext> betterHsm)
        {
            // 验证工厂返回的运行时对象完整可用。
            if (ReferenceEquals(stateContext, null)) throw new ArgumentNullException(nameof(stateContext));
            if (betterHsm == null) throw new ArgumentNullException(nameof(betterHsm));

            _stateContext = stateContext;
            _betterHsm = betterHsm;
        }

        /// <summary>
        /// 获取当前实体的强类型状态上下文。
        /// </summary>
        public TStateContext TypedStateContext => _stateContext;

        /// <summary>
        /// 获取当前实体的强类型状态机。
        /// </summary>
        /// <remarks>仅供当前能力运行时和状态图方案在同一程序集内使用；外部应通过统一查询属性读取状态。</remarks>
        internal BetterHSM<TStateContext> BetterHSM => _betterHsm;

        /// <summary>
        /// 以统一类型获取当前实体的状态上下文。
        /// </summary>
        public override object StateContext => _stateContext;

        /// <summary>
        /// 获取当前实体状态上下文的实际类型。
        /// </summary>
        public override Type StateContextType => typeof(TStateContext);

        /// <summary>
        /// 获取当前实体当前激活的状态实例。
        /// </summary>
        public override object CurrentState => _betterHsm.CurrentState;

        /// <summary>
        /// 获取当前实体当前激活状态的实际类型。
        /// </summary>
        public override Type CurrentStateType => _betterHsm.CurrentState?.GetType();

        /// <summary>
        /// 推进当前实体状态机的一次普通帧更新。
        /// </summary>
        internal override void Update()
        {
            // 将帧驱动转交给已构建完成的状态机。
            _betterHsm.Update();
        }

        /// <summary>
        /// 销毁当前实体状态机及其状态实例。
        /// </summary>
        internal override void Dispose()
        {
            // 由状态机统一执行当前状态退出和全部状态销毁生命周期。
            _betterHsm.Dispose();
        }
    }
}
