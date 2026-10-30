namespace Framework.Gameplay.Abilities.Stat
{
    /// <summary>
    /// 单条属性加成条目：带来源标识的可撤回加成。条目进出（注册/注销）是加成唯一变更方式，
    /// 禁止按值直接增减 final——撤回必须按来源整组进行，杜绝增量漂移。
    /// </summary>
    public readonly struct StatModifier
    {
        /// <summary>
        /// 加成类型：分类汇总口径不同——Flat 加法并入基数、PercentAdd 同键求和进单一乘区、Mul 独立乘区连乘。
        /// </summary>
        public enum ModifierType
        {
            // 数值型加法（如 +50 攻击）：并入基数段
            Flat = 0,

            // 百分比加法（如 +10%）：存加数（0.1 = +10%），同属性同区求和后 (1 + Σ) 结算
            PercentAdd = 1,

            // 独立乘区（如 ×1.2 增伤区）：存乘区增量（0.2 = ×1.2），同属性逐条 (1 + value) 连乘；
            // 现有养成口径未启用（只有加法区），战斗内词条接入后按需使用
            Mul = 2,
        }

        // 来源标识：装备实例/养成节点/buff 实例的稳定 id（形态统一 string 收口）；
        // 同来源允许多条（一件装备多词条、buff 叠层），注销按来源整组撤回
        public readonly string SourceId;

        // 加成目标属性
        public readonly StatType Type;

        // 加成类型
        public readonly ModifierType Kind;

        // 加成值（Flat = 数值；PercentAdd/Mul = 加数/增量，非倍率）
        public readonly float Value;

        /// <summary>创建一条加成条目。</summary>
        /// <param name="sourceId">来源标识；不允许为空（撤回按来源定位）。</param>
        /// <param name="type">目标属性。</param>
        /// <param name="kind">加成类型。</param>
        /// <param name="value">加成值。</param>
        public StatModifier(string sourceId, StatType type, ModifierType kind, float value)
        {
            SourceId = sourceId;
            Type = type;
            Kind = kind;
            Value = value;
        }
    }
}
