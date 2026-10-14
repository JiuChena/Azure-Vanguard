namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗单位数值（①数据层→③模拟层的统一形状）：模拟层与 BattleMath 只认此结构，
    /// 角色源 = CalculateStatsAsync + CharacterRow 装配，敌人源 = EnemyRow 直装。
    /// 进战斗时由 BattleDataModule 装配并缓存，战斗内零 async、零表 IO。
    /// 养成侧的 EnergyRecharge/DamageBonus/HealBonus 首切片不进战斗（无技能无治疗），切片 2 再扩。
    /// </summary>
    public readonly struct BattleUnitStats
    {
        // 最大生命
        public readonly int MaxHp;

        // 攻击
        public readonly int Atk;

        // 防御
        public readonly int Def;

        // 暴击率（0~1 小数）
        public readonly float CritRate;

        // 暴击伤害加成倍率（不含 1.0 基数）
        public readonly float CritDmg;

        // 移动速度
        public readonly float MoveSpeed;

        // 攻击间隔（秒）
        public readonly float AtkInterval;

        // 索敌/攻击射程
        public readonly float Range;

        /// <summary>全字段构造（装配出口唯一入口，防漏字段）。</summary>
        public BattleUnitStats(int maxHp, int atk, int def, float critRate, float critDmg,
            float moveSpeed, float atkInterval, float range)
        {
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            CritRate = critRate;
            CritDmg = critDmg;
            MoveSpeed = moveSpeed;
            AtkInterval = atkInterval;
            Range = range;
        }
    }
}
