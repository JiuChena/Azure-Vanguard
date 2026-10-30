using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能效果表行：对应 Assets/Module/Unit/Character/Excel/SkillEffectTable.xlsx。
    /// 一个效果一行，多效果技能 = 多行，由 SkillRow.effectIds 引用。
    /// 表只表达数据（2026-10-28 用户拍板）：effectType/target/value/growth/duration 描述效果；
    /// 效果执行函数由 SkillEffectExecuteSO 选择（SO 上选函数 + 专属参数），不靠表分派。
    /// 等级成长跟效果走：某级数值 = value + growth × (level − 1)，不逐级建行。
    /// </summary>
    [MessagePackObject]
    public sealed class SkillEffectRow : IConfigTableRow<int>
    {
        // 效果 id，全表唯一主键
        [Key("effectId")]
        public int EffectId;

        // 效果类型（数据标识：damage/heal/buff/slow…；执行由 SO 选函数，本列不驱动分派）
        [Key("effectType")]
        public string EffectType;

        // 目标：self / single_target / team_all / team_except_self / enemies_in_range
        [Key("target")]
        public string Target;

        // 作用范围（格），0 = 单体
        [Key("range")]
        public float Range;

        // 1 级数值（函数语义自定：damage=攻击力倍率、heal=最大生命比例…）
        [Key("value")]
        public float Value;

        // 每级增量
        [Key("growth")]
        public float Growth;

        // 持续秒数，0 = 瞬时
        [Key("duration")]
        public float Duration;

        // Buff 效果行的 buffId（effectType=buff 时；非 buff 效果填 0）
        [Key("buffId")]
        public int BuffId;

        // 说明文本
        [Key("desc")]
        public string Desc;

        /// <summary>按技能等级计算效果数值：value + growth × (level − 1)。</summary>
        public float GetValueAtLevel(int level)
        {
            int clamped = level < 1 ? 1 : level;
            return Value + Growth * (clamped - 1);
        }

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => EffectId;
    }
}
