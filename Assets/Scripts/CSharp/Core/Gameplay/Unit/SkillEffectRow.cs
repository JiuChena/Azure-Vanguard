using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能效果表行（2026-11-01 用户拍板：一条 effect = 一条完整效果，自带数值/成长/显示；BuffTable 已删除）：
    /// effectId/effectType/target + value/growth（可逗号分隔多属性）+ statType（加成属性）+ duration + name/icon/desc（显示）。
    /// buff 效果行 statType/value/growth 配齐（如 atk + 30 + 3），Effect 页按 effectId 查本表取 name/icon/desc。
    /// </summary>
    [MessagePackObject]
    public sealed class SkillEffectRow : IConfigTableRow<int>
    {
        // 效果 id，全表唯一主键
        [Key("effectId")]
        public int EffectId;

        // 效果类型：damage/heal/buff/slow…（数据标识）
        [Key("effectType")]
        public string EffectType;

        // 目标：self / single_target / team_all / team_except_self / enemies_in_range / team_in_range
        [Key("target")]
        public string Target;

        // 作用范围（格），0 = 单体
        [Key("range")]
        public float Range;

        // 数值（逗号分隔多值，与 statType 一一对应；单值效果写单值）
        [Key("value")]
        public string Value;

        // 每级增量（逗号分隔多值）
        [Key("growth")]
        public string Growth;

        // 持续秒数，0 = 瞬时
        [Key("duration")]
        public float Duration;

        // 加成属性（逗号分隔多属性：atk/critrate/critdmg/def/movespeed；非 buff 效果空）
        [Key("statType")]
        public string StatType;

        // 效果名称（Effect 页显示）
        [Key("name")]
        public string Name;

        // 图标地址（空 = 默认图）
        [Key("icon")]
        public string Icon;

        // 说明文本
        [Key("desc")]
        public string Desc;

        /// <summary>首个数值 + 首个成长（单值效果/damage/heal/普攻用）。</summary>
        public float GetValueAtLevel(int level)
        {
            int clamped = level < 1 ? 1 : level;
            return ParseFirst(Value) + ParseFirst(Growth) * (clamped - 1);
        }

        /// <summary>全部数值（按 statType 顺序，逐值按等级成长；buff 多属性用）。</summary>
        public float[] GetValuesAtLevel(int level)
        {
            int clamped = level < 1 ? 1 : level;
            string[] values = (Value ?? string.Empty).Split(',');
            string[] growths = (Growth ?? string.Empty).Split(',');
            var result = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                float v = Parse(values[i]);
                float g = i < growths.Length ? Parse(growths[i]) : 0f;
                result[i] = v + g * (clamped - 1);
            }
            return result;
        }

        // 逗号分隔的加成属性（与 value 对齐）
        public string[] GetStatTypes()
        {
            string[] types = (StatType ?? string.Empty).Split(',');
            for (int i = 0; i < types.Length; i++) types[i] = types[i].Trim();
            return types;
        }

        private static float ParseFirst(string text)
        {
            string[] parts = (text ?? string.Empty).Split(',');
            return parts.Length > 0 ? Parse(parts[0]) : 0f;
        }

        private static float Parse(string text)
            => float.TryParse((text ?? string.Empty).Trim(), out float v) ? v : 0f;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => EffectId;
    }
}
