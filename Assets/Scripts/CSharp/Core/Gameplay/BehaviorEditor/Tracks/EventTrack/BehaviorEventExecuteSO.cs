using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// 定义行为事件触发后如何处理通用场景上下文的配置资产。
    /// </summary>
    [MovedFrom("BehaviorCore")]
    public abstract class BehaviorEventExecuteSO : ScriptableObject
    {
        /// <summary>
        /// 执行当前事件；具体业务由项目侧子类决定。
        /// </summary>
        /// <param name="context">本次触发的通用行为事件上下文。</param>
        public abstract void Execute(BehaviorEventContext context);
    }
}
