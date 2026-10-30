using Framework.Gameplay.Abilities;
using Framework.Gameplay.Battle;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Stat
{
    /// <summary>
    /// 状态（stat）能力运行时：本能力向上下文提供 <see cref="StatRuntimeData"/>。
    /// 时序：GAS Awake（能力初始化）早于 ② 出生注册，故 Update 惰性等待——
    /// BattleUnit 就绪后按单位数据键（StatsKey，敌人带等级）从战斗数据模块创建独立状态数据
    /// （构造即拷贝白值基底，加成条目表每单位各持己份，敌人同 id 多实例不串）
    /// 并注册进上下文（AbilityRuntimeDataType.Stat）。外部经
    /// OwnerContext.TryGet&lt;StatRuntimeData&gt;(AbilityRuntimeDataType.Stat) 中转访问。
    /// 数据未预热（装配链漏单位）报错一次后停摆，不阻断其他能力。
    /// </summary>
    public sealed class StatAbilityRuntime : AbilityRuntime
    {
        // 本能力的读取键（② 单位模拟状态）与注册键（状态数据）。
        private const AbilityRuntimeDataType UnitDataType = AbilityRuntimeDataType.BattleUnit;
        private const AbilityRuntimeDataType StatDataType = AbilityRuntimeDataType.Stat;

        // 当前能力的静态配置（首切片无字段，留引用保配置语义）。
        private readonly StatAbilitySO _configuration;

        // 已创建并注册的状态数据；非 null 即本能力就绪，后续帧直接返回（零每帧开销）。
        private StatRuntimeData _statData;

        // 未预热报错只报一次（装配缺陷，重复报无增量信息）。
        private bool _missingLogged;

        /// <summary>创建状态能力运行时并保存配置引用。</summary>
        /// <param name="configuration">状态能力配置资产。</param>
        public StatAbilityRuntime(StatAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// 执行能力普通帧逻辑：惰性就绪（② 注册晚于本能力初始化，Awake 早于出生注册），
        /// 数据就位后本能力不再做事。
        /// </summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            if (_statData != null) return;
            if (!OwnerContext.TryGet(UnitDataType, out BattleUnitRuntimeData unitData)) return;

            // 数据键 = StatsKey（敌人带等级后缀如 EN_Solider_AR_0001@1；角色 = UnitId），非身份键 UnitId
            string dataKey = unitData.Unit.StatsKey;
            if (BattleDataModule.TryCreateStatData(dataKey, out StatRuntimeData statData))
            {
                _statData = statData;
                OwnerContext.Register(StatDataType, statData);
            }
            else if (!_missingLogged)
            {
                _missingLogged = true;
                Debug.LogError($"[StatAbility] '{dataKey}' 状态数据未预热，Stat 能力停摆（确认 PreloadAsync 已覆盖该单位）。");
            }
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            _statData = null;
            base.AbilityDispose();
        }
    }
}
