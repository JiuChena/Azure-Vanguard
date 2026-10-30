using System;
using Framework.Gameplay.Abilities;

namespace Framework.Gameplay.Battle
{
    /// <summary>③ 战场共享面向 ④ 能力侧的入口（②Presenter 装配时创建，出生单位逐个注册）：
    /// 感知能力经它取全场单位候选与守点，接线一次全战斗有效（控制器内列表实例稳定）。</summary>
    public sealed class BattleWorldRuntimeData : IAbilityRuntimeData
    {
        // 当前战斗控制器（③心脏）。
        private readonly BattleController _controller;

        /// <summary>
        /// 创建战场共享面入口。
        /// </summary>
        /// <param name="controller">战斗控制器；不允许为 null。</param>
        /// <exception cref="ArgumentNullException">战斗控制器为空时抛出。</exception>
        public BattleWorldRuntimeData(BattleController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>获取当前战斗控制器（单位候选/守点均由它提供）。</summary>
        public BattleController Controller => _controller;

        /// <summary>保持战场共享面，不在 GAS 的通用数据重置阶段改写。</summary>
        public void Reset()
        {
        }
    }
}
