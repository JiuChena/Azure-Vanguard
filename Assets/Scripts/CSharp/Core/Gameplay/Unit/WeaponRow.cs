using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 武器配置表行：对应 Assets/Module/Unit/Weapon/Excel/WeaponTable.xlsx。
    /// 武器与角色 1:1 绑定（抽出自带、不可更换、可升级），主键直接用绑定角色的 unitId——
    /// 武器身份即角色身份，与存档键、武器图标地址（CHWeapon_&lt;unitId&gt;）四方对齐，查询零映射。
    /// 白值三项类型之一随武器等级成长（曲线见 WeaponLevelTable）；主属性一条进属性加法区；
    /// 特殊词条为行为效果（effectKey 走 UnlockNodeRow talent 的行为逻辑键惯例），
    /// 战斗框架落地前只做展示不生效。
    /// </summary>
    [MessagePackObject]
    public sealed class WeaponRow : IConfigTableRow<string>
    {
        /// <summary>白值类型约定值（baseStatType 列）。</summary>
        public static class BaseStatTypes
        {
            // 白值加攻击
            public const string Atk = "atk";
            // 白值加防御
            public const string Def = "def";
            // 白值加生命
            public const string Hp = "hp";
        }

        // 绑定角色的单位 ID，全表唯一主键（如 CH0221）
        [Key("unitId")]
        public string UnitId;

        // 武器名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 武器描述
        [Key("desc")]
        public string Desc;

        // 白值类型：atk / def / hp（见 BaseStatTypes）
        [Key("baseStatType")]
        public string BaseStatType;

        // 1 级白值（随武器等级按 WeaponLevelTable.multiplier 成长）
        [Key("baseStatValue")]
        public int BaseStatValue;

        // 主属性键：复用 UnlockNodeTable bonus 词表（atk_pct/crit_rate/crit_dmg/dmg_bonus/heal_bonus…）
        [Key("mainStatType")]
        public string MainStatType;

        // 1 级主属性值（随武器等级按 WeaponLevelTable.multiplier 成长）
        [Key("mainStatValue")]
        public float MainStatValue;

        // 特殊词条行为键（行为逻辑键惯例，同 UnlockNodeRow talent；战斗框架接入后消费）
        [Key("specialEffectKey")]
        public string SpecialEffectKey;

        // 特殊词条参数（行为参数）
        [Key("specialEffectValue")]
        public float SpecialEffectValue;

        // 特殊词条展示文本
        [Key("specialDesc")]
        public string SpecialDesc;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => UnitId;
    }
}
