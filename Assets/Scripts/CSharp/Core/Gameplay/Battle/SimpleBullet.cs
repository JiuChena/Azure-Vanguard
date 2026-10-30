using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 通用不可穿透子弹：沿用基类直线飞行与线段检测，首个命中即生成命中特效并回收；可命中层级与特效表由预制体 Inspector 配置。
    /// </summary>
    public class SimpleBullet : CombatBullet
    {
        /// <summary>
        /// 命中处理：先生成层级匹配的命中特效再立即回收；不可穿透，同帧后续命中由基类在回收后终止派发。
        /// </summary>
        /// <param name="hit">本次命中的物理结果。</param>
        protected override void OnBulletHit(RaycastHit hit)
        {
            SpawnHitFx(hit);
            Recycle();
        }
    }
}
