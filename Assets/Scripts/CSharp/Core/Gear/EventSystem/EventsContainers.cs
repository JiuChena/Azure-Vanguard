using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 事件容器公共标记接口，EventCenter 用它在字典中统一存放不同参数数量的事件容器。
    /// </summary>
    internal interface IEventsContainer { }

    /// <summary>
    /// 无参事件容器：持有无参 UnityAction 委托。
    /// </summary>
    internal class EventsContainer : IEventsContainer
    {
        // 无参事件回调委托。
        public UnityAction eventsContainer;

        /// <summary>
        /// 创建无参事件容器并绑定初始回调。
        /// </summary>
        /// <param name="action">初始回调委托。</param>
        public EventsContainer(UnityAction action) { eventsContainer += action; }
    }

    /// <summary>
    /// 一参事件容器：持有带一个泛型参数的 UnityAction 委托。
    /// </summary>
    /// <typeparam name="T">事件参数类型。</typeparam>
    internal class EventsContainer<T> : IEventsContainer
    {
        // 一参事件回调委托。
        public UnityAction<T> eventsContainer;

        /// <summary>
        /// 创建一参事件容器并绑定初始回调。
        /// </summary>
        /// <param name="action">初始回调委托。</param>
        public EventsContainer(UnityAction<T> action) { eventsContainer += action; }
    }

    /// <summary>
    /// 二参事件容器：持有带两个泛型参数的 UnityAction 委托。
    /// </summary>
    /// <typeparam name="T">第一个参数类型。</typeparam>
    /// <typeparam name="K">第二个参数类型。</typeparam>
    internal class EventsContainer<T, K> : IEventsContainer
    {
        // 二参事件回调委托。
        public UnityAction<T, K> eventsContainer;

        /// <summary>
        /// 创建二参事件容器并绑定初始回调。
        /// </summary>
        /// <param name="action">初始回调委托。</param>
        public EventsContainer(UnityAction<T, K> action) { eventsContainer += action; }
    }
}