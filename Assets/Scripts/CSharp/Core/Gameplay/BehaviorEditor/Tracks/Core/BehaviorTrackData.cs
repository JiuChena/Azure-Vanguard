using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为轨道导出到运行时的多态数据基类。
    /// </summary>
    [Serializable]
    public abstract class BehaviorTrackData
    {
        // 行为执行器调用该轨道的顺序。
        [Tooltip("运行时 Tick 调度顺序，数值越小越先执行。")]
        public int executionOrder;

        /// <summary>
        /// 获取该轨道在运行时用于诊断的显示名称。
        /// </summary>
        /// <returns>非空的轨道显示名称。</returns>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 为当前轨道数据创建一次播放专用的执行器。
        /// </summary>
        /// <param name="context">当前行为执行上下文。</param>
        /// <returns>对应的轨道执行器。</returns>
        public abstract IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context);
    }
}
