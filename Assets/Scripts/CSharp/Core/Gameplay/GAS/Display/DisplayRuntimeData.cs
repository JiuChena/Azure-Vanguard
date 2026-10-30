using System.Collections.Generic;
using Framework.Gameplay.Abilities.Stat;
using Framework.Gameplay.Battle;
using UnityEngine;

namespace Framework.Gameplay.Abilities.Display
{
    /// <summary>
    /// 单位信息显示运行时数据：持配置（SO）+ 单位模拟状态 + 属性数据三个引用，
    /// 对外只暴露一个读取入口 <see cref="BuildModel"/>——纯函数，每次现场从三源取数
    /// 产全新 <see cref="DisplayModel"/>（零缓存：HP 每帧变，面板本就逐帧读）。
    /// 显示名 <see cref="DisplayName"/> 由 <see cref="DisplayAbilityRuntime"/> 异步回填
    /// （角色 UnitConfig.Name / 敌人 EnemyRow.Name），未回填前回退 UnitId。
    /// </summary>
    public sealed class DisplayRuntimeData : IAbilityRuntimeData
    {
        // 显示方案配置（SO；条目序 = 面板行序，敌我差异 = 各自 SO 条目不同）
        private readonly DisplayAbilitySO _configuration;

        // 单位模拟状态（③注册表持有所有权；只读引用不持有）
        private readonly BattleUnitState _unit;

        // 属性数据（Stat 能力注册；读 final / base / 条目分组用，只读引用不持有）
        private readonly StatRuntimeData _stat;

        /// <summary>创建显示运行时数据。</summary>
        /// <param name="configuration">显示方案配置。</param>
        /// <param name="unit">单位模拟状态；不允许为 null。</param>
        /// <param name="stat">单位属性数据；不允许为 null。</param>
        public DisplayRuntimeData(DisplayAbilitySO configuration, BattleUnitState unit, StatRuntimeData stat)
        {
            _configuration = configuration;
            _unit = unit;
            _stat = stat;
            DisplayName = unit.UnitId;
        }

        /// <summary>显示名（异步回填；测试可直接注入断言，未回填 = UnitId）。</summary>
        public string DisplayName { get; set; }

        /// <summary>当前显示方案配置（验证/对接期只读检查用）。</summary>
        public DisplayAbilitySO Configuration => _configuration;

        /// <summary>
        /// 读取当前显示模型（纯函数）：头部信息恒产；条目段按 SO 列表序逐条取数格式化；
        /// buff 段按配置开关决定分组产出（v1 恒空）。调用方每帧调用，返回全新实例。
        /// </summary>
        /// <returns>当前显示模型。</returns>
        public DisplayModel BuildModel()
        {
            DisplayModel model = new DisplayModel
            {
                UnitId = _unit.UnitId,
                DisplayName = DisplayName,
                Camp = _unit.Camp,
                Vital = _unit.Vital,
            };

            // 头部生命：MaxHp 以 Stat final 为准（buff 可改），缺失键回退单位装配快照
            int maxHp = _stat.TryGetFinal(StatType.MaxHp, out float statMaxHp) && statMaxHp > 0f
                ? StatMath.FloorToInt(statMaxHp)
                : _unit.Stats.MaxHp;
            model.CurrentHp = _unit.Hp;
            model.MaxHp = maxHp;
            model.HpRatio = maxHp > 0 ? Mathf.Clamp01((float)_unit.Hp / maxHp) : 0f;

            // 条目段：逐条目取源值 → 按风格格式化
            IReadOnlyList<DisplayFieldEntry> entries = _configuration.FieldEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                model.Fields.Add(ResolveField(entries[i], maxHp, model.HpRatio));
            }

            // buff 段：v1 配置开关默认关 → 恒空；开时按 SourceId 分组
            if (_configuration.BuffSection.Enabled)
            {
                BuildBuffs(model);
            }

            return model;
        }

        /// <summary>保持引用不动，显示模型每次现场重建（Reset 语义 = 无运行时可清状态）。</summary>
        public void Reset()
        {
        }

        // 单条目取数 + 格式化（纯函数；maxHp/hpRatio 由 BuildModel 预先算好复用）
        private DisplayFieldValue ResolveField(DisplayFieldEntry entry, int maxHp, float hpRatio)
        {
            var field = new DisplayFieldValue
            {
                Key = entry.Key,
                Label = entry.Label,
                Style = entry.Style,
            };

            switch (entry.Source)
            {
                case DisplaySource.Stat:
                    _stat.TryGetFinal(entry.StatType, out float statValue);
                    ApplyNumeric(field, statValue, entry.Style);
                    field.Modified = IsStatModified(entry.StatType);
                    break;

                case DisplaySource.CurrentHp:
                    ApplyNumeric(field, _unit.Hp, entry.Style);
                    break;

                case DisplaySource.MaxHp:
                    ApplyNumeric(field, maxHp, entry.Style);
                    break;

                case DisplaySource.HpPercent:
                    ApplyNumeric(field, hpRatio, entry.Style);
                    break;

                case DisplaySource.DisplayName:
                    field.Text = string.IsNullOrEmpty(DisplayName) ? _unit.UnitId : DisplayName;
                    break;

                case DisplaySource.UnitId:
                    field.Text = _unit.UnitId;
                    break;

                case DisplaySource.Camp:
                    field.Text = _unit.Camp == BattleCamp.Character ? "CHARACTER" : "ENEMY";
                    break;

                case DisplaySource.Vital:
                    field.Text = _unit.Vital == UnitVitalState.Alive ? "ALIVE" : "DOWNED";
                    break;
            }

            return field;
        }

        // 数值源格式化：Number 整型 / Float 一位小数 / Percent 整数百分比 / Bar 比例条（越界截断）
        private static void ApplyNumeric(DisplayFieldValue field, float value, DisplayStyle style)
        {
            switch (style)
            {
                case DisplayStyle.Number:
                    field.Text = StatMath.FloorToInt(value).ToString();
                    break;

                case DisplayStyle.Float:
                    field.Text = value.ToString("F1");
                    break;

                case DisplayStyle.Percent:
                    field.Text = Mathf.RoundToInt(value * 100f).ToString() + "%";
                    break;

                case DisplayStyle.Bar:
                    field.Ratio = Mathf.Clamp01(value);
                    field.Text = Mathf.RoundToInt(value * 100f).ToString() + "%";
                    break;

                default: // Text 风格遇数值源：整型显示兜底（配置漂移防御）
                    field.Text = StatMath.FloorToInt(value).ToString();
                    break;
            }
        }

        // 属性是否被 buff 影响（存在该属性的 Modifier 条目，View 可高亮行）
        private bool IsStatModified(StatType type)
        {
            IReadOnlyList<StatModifier> modifiers = _stat.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Type == type) return true;
            }
            return false;
        }

        // buff 汇总：Modifiers 按 SourceId 分组（首次出现序）→ 每条叠层计数 + 各属性边际贡献
        private void BuildBuffs(DisplayModel model)
        {
            Dictionary<string, List<StatModifier>> groups = null;
            IReadOnlyList<StatModifier> modifiers = _stat.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier mod = modifiers[i];
                if (string.IsNullOrEmpty(mod.SourceId)) continue;
                if (groups == null) groups = new Dictionary<string, List<StatModifier>>();
                if (!groups.TryGetValue(mod.SourceId, out List<StatModifier> list))
                {
                    list = new List<StatModifier>();
                    groups[mod.SourceId] = list;
                }
                list.Add(mod);
            }

            if (groups == null) return;

            DisplayBuffSectionConfig section = _configuration.BuffSection;
            foreach (KeyValuePair<string, List<StatModifier>> pair in groups)
            {
                // maxShown 截断（0 = 不限制）
                if (section.MaxShown > 0 && model.Buffs.Count >= section.MaxShown) break;

                string sourceId = pair.Key;
                List<StatModifier> stack = pair.Value;

                DisplayBuffValue buff = new DisplayBuffValue
                {
                    SourceId = sourceId,
                    DisplayName = sourceId, // v1 无名称表，SourceId 兜底
                    StackCount = stack.Count,
                    RemainingTime = 0f,
                    MaxTime = 0f,
                };

                // 该来源触及的属性集合（保持首次出现序），逐属性算边际贡献
                List<StatType> types = new List<StatType>();
                for (int i = 0; i < stack.Count; i++)
                {
                    StatType type = stack[i].Type;
                    if (!types.Contains(type)) types.Add(type);
                }
                for (int i = 0; i < types.Count; i++)
                {
                    buff.Effects.Add(new DisplayBuffStatEffect
                    {
                        Type = types[i],
                        Delta = MarginalDelta(_stat, types[i], sourceId),
                    });
                }

                model.Buffs.Add(buff);
            }
        }

        // 单来源对某属性的边际贡献：全量 final − 剔除本来源后的 final。
        // 用剔除重算而非简单求和——PercentAdd/Mul 非线性，直接加值会算错。
        private static float MarginalDelta(StatRuntimeData stat, StatType type, string sourceId)
        {
            float baseValue = stat.GetBase(type);
            IReadOnlyList<StatModifier> all = stat.Modifiers;
            double withAll = StatMath.Evaluate(baseValue, all, type);

            List<StatModifier> filtered = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].SourceId == sourceId) continue;
                if (filtered == null) filtered = new List<StatModifier>();
                filtered.Add(all[i]);
            }
            double without = StatMath.Evaluate(baseValue, filtered, type);

            return (float)(withAll - without);
        }
    }
}
