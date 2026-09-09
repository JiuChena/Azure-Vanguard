using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// 定义 HitBox 检测完成后如何处理命中上下文的配置资产。
    /// </summary>
    [MovedFrom("BehaviorCore")]
    public abstract class HitExecuteSO : ScriptableObject
    {
        /// <summary>
        /// 执行本次 HitBox 命中结果；上下文列表可由子类按自身玩法需求修改。
        /// </summary>
        /// <param name="context">本次检测构建的命中上下文，索引 0 为调用者自身。</param>
        public abstract void Execute(HitContext context);
    }
}
