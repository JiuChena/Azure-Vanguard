using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// HitBox 检测完成后交给命中执行配置的可写上下文。
    /// </summary>
    public sealed class HitContext
    {
        // 命中对象列表；索引 0 固定为调用者自身，后续项为本次物理查询返回的碰撞体所属对象。
        public List<GameObject> GameObjects = new List<GameObject>();
    }
}
