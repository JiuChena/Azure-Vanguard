using System;
using Framework.Gameplay.Abilities;

namespace Framework.Gameplay.Battle
{
    /// <summary>③ 单位模拟状态向 ④ 能力侧的入口（②Presenter 出生时注册进单位 GAS 上下文）：
    /// 感知能力经它读意图字段并写决策结论，寻路能力经它读目的地并回写位置。</summary>
    public sealed class BattleUnitRuntimeData : IAbilityRuntimeData
    {
        // 当前单位模拟状态（③注册表持有所有权）。
        private readonly BattleUnitState _unit;

        /// <summary>
        /// 创建单位模拟状态入口。
        /// </summary>
        /// <param name="unit">单位模拟状态；不允许为 null。</param>
        /// <exception cref="ArgumentNullException">单位模拟状态为空时抛出。</exception>
        public BattleUnitRuntimeData(BattleUnitState unit)
        {
            _unit = unit ?? throw new ArgumentNullException(nameof(unit));
        }

        /// <summary>获取当前单位模拟状态。</summary>
        public BattleUnitState Unit => _unit;

        /// <summary>保持单位模拟状态，不在 GAS 的通用数据重置阶段改写。</summary>
        public void Reset()
        {
        }
    }
}
