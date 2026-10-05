using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能效果表行：对应 Assets/Module/Unit/Character/Excel/SkillEffectTable.xlsx。
    /// 一个效果一行，多效果技能 = 多行，由 SkillRow.effectIds 引用。
    /// 等级成长跟效果走：某级数值 = value + growth × (level − 1)，不逐级建行。
    /// 运行时按 effectType 经注册表分发执行器（同 ItemFeatureCatalog 惯例）。
    /// 效果 id 编码 = skillId×100 + 序号，如 2210201。
    /// </summary>
    [MessagePackObject]
    public sealed class SkillEffectRow : IConfigTableRow<int>
    {
        // 效果 id，全表唯一主键
        [Key("effectId")]
        public int EffectId;

        // 效果类型：damage/heal/slow…（执行器注册键；value 单位由该类型的语义约定决定）
        [Key("effectType")]
        public string EffectType;

        // 目标：self / team_all / team_except_self / single_target / enemies_in_range
        [Key("target")]
        public string Target;

        // 作用范围（格），0 = 单体
        [Key("range")]
        public float Range;

        // 1 级数值（单位由 effectType 语义决定：damage=攻击力倍率、heal=最大生命比例、slow=移速降低比例…）
        [Key("value")]
        public float Value;

        // 每级增量
        [Key("growth")]
        public float Growth;

        // 持续秒数，0 = 瞬时
        [Key("duration")]
        public float Duration;

        // 说明文本
        [Key("desc")]
        public string Desc;

        /// <summary>按技能等级计算效果数值：value + growth × (level − 1)。（方法不参与序列化，无需标注）</summary>
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
