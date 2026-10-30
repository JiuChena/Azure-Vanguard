using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Battle;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Buff
{
    /// <summary>
    /// Buff 能力运行时：向上下文提供 <see cref="BuffRuntimeData"/>。
    /// 时序对齐 Stat/Display 能力（GAS Awake 早于 ② 出生注册）——Update 惰性等待
    /// BattleUnit 与 Stat 两键就绪后建 buff 数据并注册（AbilityRuntimeDataType.Buff），
    /// 就绪后每帧 _buffData.Tick(deltaTime) 驱动全单位 buff 帧轮询/中断/注销。
    /// 数据未就绪报错一次后停摆，不阻断其他能力。本能力 SO 零可配置参数
    /// （buff 由应用方 new 实例经 BuffRuntimeData.ApplyBuff 注入，逻辑自洽）。
    /// </summary>
    public sealed class BuffAbilityRuntime : AbilityRuntime
    {
        // 读取键（② 单位模拟状态 / Stat 属性数据）与注册键（buff 数据）。
        private const AbilityRuntimeDataType UnitDataType = AbilityRuntimeDataType.BattleUnit;
        private const AbilityRuntimeDataType StatDataType = AbilityRuntimeDataType.Stat;
        private const AbilityRuntimeDataType BuffDataType = AbilityRuntimeDataType.Buff;

        // 当前能力的静态配置（零字段，留引用保配置语义）。
        private readonly BuffAbilitySO _configuration;

        // 已创建并注册的 buff 数据；非 null 即本能力就绪，每帧转 Tick。
        private BuffRuntimeData _buffData;

        // 未就绪报错只报一次（装配缺陷，重复报无增量信息）。
        private bool _missingLogged;

        /// <summary>创建 buff 能力运行时并保存配置引用。</summary>
        /// <param name="configuration">buff 能力配置资产。</param>
        public BuffAbilityRuntime(BuffAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// 执行能力普通帧逻辑：惰性就绪（② 注册晚于本能力初始化），
        /// BattleUnit + Stat 两键齐备后建 buff 数据并注册；就绪后每帧转 Tick（帧中断→注销→轮询）。
        /// </summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            if (_buffData != null)
            {
                _buffData.Tick(deltaTime);
                return;
            }
            if (!OwnerContext.TryGet(UnitDataType, out BattleUnitRuntimeData unitData)) return;
            if (!OwnerContext.TryGet(StatDataType, out StatRuntimeData statData)) return;

            _buffData = new BuffRuntimeData(OwnerContext, unitData.Unit, statData);
            OwnerContext.Register(BuffDataType, _buffData);
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            _buffData = null;
            base.AbilityDispose();
        }
    }
}
