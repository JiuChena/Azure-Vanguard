namespace Framework.Gameplay.Abilities.Stat
{
    /// <summary>
    /// 属性键（系统级封闭集合）：养成 8 项对齐 CharacterStats 字段，战斗专有 3 项对齐 BattleUnitStats。
    /// 加新属性 = 加枚举值（additive）；运行时全部以本枚举为键，禁用字符串散键。
    /// </summary>
    public enum StatType
    {
        // 最大生命
        MaxHp = 0,

        // 攻击
        Atk = 1,

        // 防御
        Def = 2,

        // 暴击率（0~1 小数）
        CritRate = 3,

        // 暴击伤害加成倍率（不含 1.0 基数）
        CritDmg = 4,

        // 充能效率（乘区系数，1.0 = 100%）
        EnergyRecharge = 5,

        // 伤害加成（加法区，0 = 无加成）
        DamageBonus = 6,

        // 治疗加成（加法区，0 = 无加成）
        HealBonus = 7,

        // 移动速度（战斗专有，装配自 CharacterRow/EnemyRow）
        MoveSpeed = 8,

        // 攻击间隔秒（战斗专有）
        AtkInterval = 9,

        // 索敌/攻击射程（战斗专有）
        Range = 10,
    }
}
