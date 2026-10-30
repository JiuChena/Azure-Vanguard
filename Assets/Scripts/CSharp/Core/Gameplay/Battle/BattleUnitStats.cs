namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗单位数值（①数据层→③模拟层的统一形状）：模拟层与 BattleMath 只认此结构，
    /// 角色源 = CalculateStatsAsync + CharacterRow 装配，敌人源 = EnemyRow 1 级基准 × LevelTable.multiplier 折算。
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

        // 攻击转向速度（度/秒；攻击面向目标时的固定转速，0 = 不转向）
        public readonly float RotateSpeed;

        // 攻击转向节点路径（空 = 转自身 root；非空 = transform.Find 相对路径，如坦克炮塔节点）
        public readonly string RotateNode;

        // 子弹锁头 XYZ 散射半径（米；枪械精度差异配置项，0 = 精准锁头，目标点不加随机偏移）
        public readonly float BulletSpread;

        // 头部骨骼节点（prefab 内骨骼名或相对路径；子弹锁头与攻击转向取目标 head 世界位用，空 = 未配）
        public readonly string HeadNode;

        // 敌人停走横向偏移（米；停走后沿朝向垂线随机左右偏移量，0 = 不偏移）
        public readonly float StopLateralOffset;

        // 停走阻挡判定半径（米；找去路上"挡路角色"的垂距门槛，≤0 由 FindPathBlocker 兜底默认 0.8）
        public readonly float BlockRadius;

        // 守点消耗值（每只敌人进入守点直接扣的守点血量；守点交互重做 2026-10-27）
        public readonly int GuardPointDamage;

        // 能量上限（角色表 maxEnergy 列；Q 大招能量门槛，敌人无能量恒 0）
        public readonly int MaxEnergy;

        // 充能效率（回能乘区系数，1.0 = 100%；装配自角色养成 CharacterStats.EnergyRecharge，敌人恒 1）
        public readonly float EnergyRecharge;

        // 小技能 E 的技能 id（SkillTable 主键；敌人无技能恒 0）
        public readonly int SkillEId;

        // 爆发技能 Q 的技能 id（SkillTable 主键；敌人无技能恒 0）
        public readonly int SkillQId;

        // 普攻技能 id（SkillTable 主键；子弹命中回能读普攻行 energyGain 用）
        public readonly int NormalAtkId;

        /// <summary>全字段构造（装配出口唯一入口，防漏字段）；转向/锁头/侧偏/阻挡半径/守点消耗/能量/技能字段带默认值，未配时不生效。</summary>
        public BattleUnitStats(int maxHp, int atk, int def, float critRate, float critDmg,
            float moveSpeed, float atkInterval, float range,
            float rotateSpeed = 0f, string rotateNode = null,
            float bulletSpread = 0f, string headNode = null, float stopLateralOffset = 0f,
            float blockRadius = 0.8f, int guardPointDamage = 0, int maxEnergy = 0,
            float energyRecharge = 1f, int skillEId = 0, int skillQId = 0, int normalAtkId = 0)
        {
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            CritRate = critRate;
            CritDmg = critDmg;
            MoveSpeed = moveSpeed;
            AtkInterval = atkInterval;
            Range = range;
            RotateSpeed = rotateSpeed;
            RotateNode = rotateNode;
            BulletSpread = bulletSpread;
            HeadNode = headNode;
            StopLateralOffset = stopLateralOffset;
            BlockRadius = blockRadius;
            GuardPointDamage = guardPointDamage;
            MaxEnergy = maxEnergy;
            EnergyRecharge = energyRecharge;
            SkillEId = skillEId;
            SkillQId = skillQId;
            NormalAtkId = normalAtkId;
        }
    }
}
