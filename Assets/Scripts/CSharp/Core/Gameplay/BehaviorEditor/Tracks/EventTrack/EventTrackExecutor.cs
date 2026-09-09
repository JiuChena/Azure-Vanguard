using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 驱动定时行为事件排序与分发的轨道执行器。
    /// </summary>
    internal sealed class EventTrackExecutor : IBehaviorTrackExecutor
    {
        // 当前轨道导出的静态事件数据。
        private readonly EventTrackData data;
        // 当前播放的宿主依赖与环境配置。
        private readonly BehaviorExecutionContext context;
        // 本次播放按触发时间排序后的有效事件表。
        private BehaviorEvent[] sortedEvents = Array.Empty<BehaviorEvent>();
        // 下一个尚未执行事件的索引。
        private int nextEventIndex;
        // 事件执行期间复用的可写上下文。
        private readonly BehaviorEventContext eventContext = new BehaviorEventContext();

        /// <summary>事件轨道的执行顺序。</summary>
        public int ExecutionOrder => data.executionOrder;

        /// <summary>
        /// 创建事件轨道执行器。
        /// </summary>
        /// <param name="data">当前轨道导出数据；不得为 null。</param>
        /// <param name="context">当前播放执行上下文；不得为 null。</param>
        public EventTrackExecutor(EventTrackData data, BehaviorExecutionContext context)
        {
            this.data = data;
            this.context = context;
        }

        /// <summary>
        /// 建立本次播放的稳定事件时间表。
        /// </summary>
        public void Begin()
        {
            BehaviorEvent[] sourceEvents = data.events;
            if (sourceEvents == null || sourceEvents.Length == 0)
            {
                sortedEvents = Array.Empty<BehaviorEvent>();
                nextEventIndex = 0;
                return;
            }

            // 只保留可执行事件，再以稳定排序保持同一时刻的作者顺序。
            var validEvents = new List<BehaviorEvent>(sourceEvents.Length);
            for (int index = 0; index < sourceEvents.Length; index++)
            {
                BehaviorEvent behaviorEvent = sourceEvents[index];
                if (behaviorEvent != null && behaviorEvent.execute != null) validEvents.Add(behaviorEvent);
            }

            for (int index = 1; index < validEvents.Count; index++)
            {
                BehaviorEvent current = validEvents[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0 && validEvents[insertionIndex].time > current.time)
                {
                    validEvents[insertionIndex + 1] = validEvents[insertionIndex];
                    insertionIndex--;
                }

                validEvents[insertionIndex + 1] = current;
            }

            sortedEvents = validEvents.ToArray();
            nextEventIndex = 0;
        }

        /// <summary>
        /// 触发当前时间之前尚未执行的全部事件。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        public void Tick(float elapsedTime)
        {
            while (nextEventIndex < sortedEvents.Length && sortedEvents[nextEventIndex].time <= elapsedTime)
            {
                ExecuteEvent(sortedEvents[nextEventIndex], elapsedTime);
                nextEventIndex++;
            }
        }

        /// <summary>
        /// 清理本次播放的事件表和索引。
        /// </summary>
        public void Stop()
        {
            sortedEvents = Array.Empty<BehaviorEvent>();
            nextEventIndex = 0;
        }

        /// <summary>
        /// 解析事件世界位姿并交由其 ScriptableObject 执行配置处理。
        /// </summary>
        /// <param name="behaviorEvent">当前到期的事件。</param>
        /// <param name="elapsedTime">事件实际执行时的行为经过时间。</param>
        private void ExecuteEvent(BehaviorEvent behaviorEvent, float elapsedTime)
        {
            if (behaviorEvent?.execute == null || context.OwnerTransform == null) return;

            // 世界空间事件使用原始偏移；骨骼事件通过共享解析服务计算位姿。
            bool useWorldSpace = string.IsNullOrWhiteSpace(behaviorEvent.referenceBone);
            Transform reference = useWorldSpace ? null : context.TransformResolver.Resolve(behaviorEvent.referenceBone);
            Transform anchor = reference != null ? reference : context.OwnerTransform;
            Vector3 position = useWorldSpace ? behaviorEvent.positionOffset : anchor.TransformPoint(behaviorEvent.positionOffset);
            Quaternion rotation = useWorldSpace ? Quaternion.Euler(behaviorEvent.rotationOffset) : anchor.rotation * Quaternion.Euler(behaviorEvent.rotationOffset);

            // 填充复用上下文，具体业务由执行配置子类决定。
            eventContext.OwnerGameObject = context.OwnerGameObject;
            eventContext.OwnerTransform = context.OwnerTransform;
            eventContext.ReferenceTransform = reference;
            eventContext.ReferenceBonePath = behaviorEvent.referenceBone;
            eventContext.Position = position;
            eventContext.Rotation = rotation;
            eventContext.Scale = behaviorEvent.scaleOffset;
            eventContext.TriggerTime = behaviorEvent.time;
            eventContext.ElapsedTime = elapsedTime;
            behaviorEvent.execute.Execute(eventContext);

            if (data.logEvents)
                Debug.Log($"[{context.OwnerGameObject.name}] 触发事件：{behaviorEvent.execute.name} | Time={behaviorEvent.time:F2}s | Bone={(useWorldSpace ? "<World>" : behaviorEvent.referenceBone)}", context.OwnerGameObject);
        }
    }
}
