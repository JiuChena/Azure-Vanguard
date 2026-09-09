using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 定时事件数据。
    /// </summary>
    public class TimerEvent
    {
        /// <summary>
        /// 每次触发间隔（秒）。
        /// </summary>
        public readonly double Interval;

        /// <summary>
        /// 触发总次数。-1 代表无限次。
        /// </summary>
        public readonly int TriggerCount;

        /// <summary>
        /// 已执行次数。
        /// </summary>
        public int ExecutedCount;

        /// <summary>
        /// 上次记录的触发时间点。
        /// </summary>
        public double RecordTime;

        /// <summary>
        /// 到期后执行的回调。
        /// </summary>
        public readonly UnityAction Action;

        public TimerEvent(float interval, int triggerCount, UnityAction action, double recordTime)
        {
            Interval = interval;
            TriggerCount = triggerCount;
            Action = action;
            RecordTime = recordTime;
        }
    }
}
