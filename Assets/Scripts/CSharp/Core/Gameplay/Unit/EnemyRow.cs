using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 敌人配置表行：对应 Assets/Module/Unit/Enemy/Excel/EnemyTable.xlsx。
    /// 数值与身份进表（同 CharacterRow 口径），敌人 prefab 按 id 命名走 Addressables 地址约定。
    /// 普攻经 normalAtkId 引用 SkillTable（敌人段编码：9+敌序号×100+槽位，如 EN0001 → 90101）。
    /// </summary>
    [MessagePackObject]
    public sealed class EnemyRow : IConfigTableRow<string>
    {
        // 敌人 ID，全表唯一主键；同时是 prefab 的 Addressables 地址（如 EN0001）
        [Key("id")]
        public string Id;

        // 敌人名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 阵营（当前恒 enemy；列与 CharacterRow.camp 对齐，留将来中立/敌内部分派）
        [Key("camp")]
        public string Camp;

        // 基础生命
        [Key("hp")]
        public int Hp;

        // 基础攻击
        [Key("atk")]
        public int Atk;

        // 基础防御
        [Key("def")]
        public int Def;

        // 攻击间隔（秒）
        [Key("atkInterval")]
        public float AtkInterval;

        // 索敌/攻击射程
        [Key("range")]
        public float Range;

        // 移动速度
        [Key("moveSpeed")]
        public float MoveSpeed;

        // 普攻技能 id（SkillTable 主键，敌人段 9xx 编码）
        [Key("normalAtkId")]
        public int NormalAtkId;

        // 暴击率（0~1 小数）；v1 敌人不暴击，列先备
        [Key("critRate")]
        public float CritRate;

        // 暴击伤害加成倍率（不含 1.0 基数）；v1 恒 0，列先备
        [Key("critDmg")]
        public float CritDmg;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
