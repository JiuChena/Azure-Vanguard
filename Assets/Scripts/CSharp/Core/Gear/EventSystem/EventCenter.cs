using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 事件中心，全局发布-订阅系统。支持无参、一参、二参三种事件类型。
    /// 通过 <see cref="EventNames"/> 常量指定事件名，类型安全。
    /// </summary>
    public class EventCenter
    {
        private static readonly EventCenter instance = new EventCenter();
        public static EventCenter Instance => instance;

        private EventCenter() { }

        private readonly Dictionary<string, IEventsContainer> events = new Dictionary<string, IEventsContainer>();

        #region 添加事件监听

        /// <summary>
        /// 添加无参事件监听。
        /// </summary>
        /// <param name="eventName">事件名称，使用 <see cref="EventNames"/> 常量</param>
        /// <param name="action">回调委托</param>
        public void Register(string eventName, UnityAction action)
        {
            if (events.ContainsKey(eventName))
            {
                if (events[eventName] is not EventsContainer container)
                {
                    Debug.LogError($"事件 [{eventName}] 已以不同类型注册，类型不匹配！");
                    return;
                }
                container.eventsContainer += action;
            }
            else
            {
                events.Add(eventName, new EventsContainer(action));
            }
        }

        /// <summary>
        /// 添加一参事件监听。
        /// </summary>
        /// <typeparam name="T">事件参数类型</typeparam>
        /// <param name="eventName">事件名称，使用 <see cref="EventNames"/> 常量</param>
        /// <param name="action">回调委托</param>
        public void Register<T>(string eventName, UnityAction<T> action)
        {
            if (events.ContainsKey(eventName))
            {
                if (events[eventName] is not EventsContainer<T> container)
                {
                    Debug.LogError($"事件 [{eventName}] 已以不同类型注册，类型不匹配！");
                    return;
                }
                container.eventsContainer += action;
            }
            else
            {
                events.Add(eventName, new EventsContainer<T>(action));
            }
        }

        /// <summary>
        /// 添加二参事件监听。
        /// </summary>
        /// <typeparam name="T">第一个参数类型</typeparam>
        /// <typeparam name="K">第二个参数类型</typeparam>
        /// <param name="eventName">事件名称，使用 <see cref="EventNames"/> 常量</param>
        /// <param name="action">回调委托</param>
        public void Register<T, K>(string eventName, UnityAction<T, K> action)
        {
            if (events.ContainsKey(eventName))
            {
                if (events[eventName] is not EventsContainer<T, K> container)
                {
                    Debug.LogError($"事件 [{eventName}] 已以不同类型注册，类型不匹配！");
                    return;
                }
                container.eventsContainer += action;
            }
            else
            {
                events.Add(eventName, new EventsContainer<T, K>(action));
            }
        }

        #endregion

        #region 移除事件监听

        /// <summary>
        /// 移除无参事件监听。
        /// </summary>
        public void Unregister(string eventName, UnityAction action)
        {
            if (events.ContainsKey(eventName) && events[eventName] is EventsContainer container)
                container.eventsContainer -= action;
        }

        /// <summary>
        /// 移除一参事件监听。
        /// </summary>
        public void Unregister<T>(string eventName, UnityAction<T> action)
        {
            if (events.ContainsKey(eventName) && events[eventName] is EventsContainer<T> container)
                container.eventsContainer -= action;
        }

        /// <summary>
        /// 移除二参事件监听。
        /// </summary>
        public void Unregister<T, K>(string eventName, UnityAction<T, K> action)
        {
            if (events.ContainsKey(eventName) && events[eventName] is EventsContainer<T, K> container)
                container.eventsContainer -= action;
        }

        #endregion

        #region 触发事件

        /// <summary>
        /// 触发无参事件。
        /// </summary>
        public void SetEventTrigger(string eventName)
        {
            if (events.ContainsKey(eventName) && events[eventName] is EventsContainer container)
                container.eventsContainer?.Invoke();
        }

        /// <summary>
        /// 触发一参事件。
        /// </summary>
        /// <typeparam name="T">事件参数类型</typeparam>
        /// <param name="eventName">事件名称</param>
        /// <param name="info">事件参数</param>
        public void SetEventTrigger<T>(string eventName, T info)
        {
            if (events.ContainsKey(eventName) && events[eventName] is EventsContainer<T> container)
                container.eventsContainer?.Invoke(info);
        }

        /// <summary>
        /// 触发二参事件。
        /// </summary>
        public void SetEventTrigger<T, K>(string eventName, T info1, K info2)
        {
            if (events.ContainsKey(eventName) && events[eventName] is EventsContainer<T, K> container)
                container.eventsContainer?.Invoke(info1, info2);
        }

        #endregion

        /// <summary>
        /// 清空事件中心中所有事件监听。
        /// </summary>
        public void EventDicClear()
        {
            events.Clear();
        }
    }
}
