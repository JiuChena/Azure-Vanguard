using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 场内单位信息（②表现层缓存）：单位模拟状态 + 出生时解析好的头部骨骼 Transform。
    /// 供子弹锁头、攻击转向取目标 head 世界位共用；死亡出表时随注销移除。
    /// </summary>
    public sealed class FieldUnitInfo
    {
        // 单位模拟状态（③注册表持有所有权；反查时按 IsAlive 过滤，倒地单位不被锁头/转向）
        public readonly BattleUnitState Unit;

        // 头部骨骼节点（headNode 列解析结果；解析失败为 null，调用方回退根位置/事件正向）
        public readonly Transform HeadNode;

        /// <summary>构造场内单位信息。</summary>
        /// <param name="unit">单位模拟状态；不允许为 null。</param>
        /// <param name="headNode">解析好的头部骨骼；未配/解析失败可为 null。</param>
        public FieldUnitInfo(BattleUnitState unit, Transform headNode)
        {
            Unit = unit;
            HeadNode = headNode;
        }
    }
}
