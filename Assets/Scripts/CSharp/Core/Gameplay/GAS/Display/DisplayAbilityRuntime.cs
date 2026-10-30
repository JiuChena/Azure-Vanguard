using System;
using Core.Gear;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Battle;
using Framework.Gameplay.Unit;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Display
{
    /// <summary>
    /// 显示能力运行时：向上下文提供 <see cref="DisplayRuntimeData"/>。
    /// 时序对齐 StatAbilityRuntime（GAS Awake 早于 ② 出生注册）——Update 惰性等待
    /// BattleUnit 与 Stat 两键就绪后建显示数据并注册（AbilityRuntimeDataType.Display），
    /// 此后每帧零开销（BuildModel 由面板拉取调用，能力本身不做事）。
    /// 就绪时异步回填显示名（fire-and-forget：角色 UnitConfig.Name / 敌人 EnemyRow.Name；
    /// 未回填前显示模型回退 UnitId）。数据未就绪报错一次后停摆，不阻断其他能力。
    /// </summary>
    public class DisplayAbilityRuntime : AbilityRuntime
    {
        // 读取键（② 单位模拟状态 / Stat 属性数据）与注册键（显示数据）。
        private const AbilityRuntimeDataType UnitDataType = AbilityRuntimeDataType.BattleUnit;
        private const AbilityRuntimeDataType StatDataType = AbilityRuntimeDataType.Stat;
        private const AbilityRuntimeDataType DisplayDataType = AbilityRuntimeDataType.Display;

        // 当前能力的静态配置（字段条目列表 = 显示方案，敌我差异所在）。
        private readonly DisplayAbilitySO _configuration;

        // 已创建并注册的显示数据；非 null 即本能力就绪，后续帧直接返回（零每帧开销）。
        private DisplayRuntimeData _displayData;

        // 未就绪报错只报一次（装配缺陷，重复报无增量信息）。
        private bool _missingLogged;

        /// <summary>创建显示能力运行时并保存配置引用。</summary>
        /// <param name="configuration">显示方案配置资产。</param>
        public DisplayAbilityRuntime(DisplayAbilitySO configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// 执行能力普通帧逻辑：惰性就绪（② 注册晚于本能力初始化），
        /// 两数据键（BattleUnit + Stat）齐备后建显示数据并注册，随后本能力不再做事。
        /// </summary>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public override void AbilityUpdate(float deltaTime)
        {
            if (_displayData != null) return;
            if (!OwnerContext.TryGet(UnitDataType, out BattleUnitRuntimeData unitData)) return;
            if (!OwnerContext.TryGet(StatDataType, out StatRuntimeData statData)) return;

            _displayData = new DisplayRuntimeData(_configuration, unitData.Unit, statData);
            OwnerContext.Register(DisplayDataType, _displayData);
            RefreshDisplayNameAsync(unitData.Unit);
        }

        /// <summary>
        /// 异步回填显示名（fire-and-forget，不外传任务）：
        /// 角色经 UnitConfigLibrary 取配置名；敌人经 DataTableManager 查 EnemyTable 名。
        /// 回填失败保留 UnitId 兜底（显示名缺失不阻断面板）。异常一律吞掉只告警。
        /// </summary>
        /// <param name="unit">单位模拟状态（仅读身份/阵营，不持有）。</param>
        private async void RefreshDisplayNameAsync(BattleUnitState unit)
        {
            try
            {
                string name = null;
                if (unit.Camp == BattleCamp.Character)
                {
                    UnitConfig config = await UnitConfigLibrary.GetAsync(unit.UnitId);
                    if (config != null) name = config.Name;
                }
                else
                {
                    DataTable<EnemyRow, string> table =
                        await DataTableManager.Instance.LoadTableAsync<EnemyRow, string>("EnemyTable");
                    if (table != null && table.TryGet(unit.UnitId, out EnemyRow row) && row != null)
                    {
                        name = row.Name;
                    }
                }

                if (_displayData != null && !string.IsNullOrEmpty(name))
                {
                    _displayData.DisplayName = name;
                }
            }
            catch (Exception e)
            {
                // 异步路径吞异常只告警：显示名缺失回退 UnitId，不影响面板主体
                Debug.LogWarning($"[DisplayAbility] 显示名回填失败，保留 UnitId（{e.Message}）");
            }
        }

        /// <summary>释放能力持有的运行时依赖。</summary>
        public override void AbilityDispose()
        {
            _displayData = null;
            base.AbilityDispose();
        }
    }
}
