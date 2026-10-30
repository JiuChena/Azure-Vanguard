using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 敌人配置表行：对应 Assets/Module/Unit/Enemy/Excel/EnemyTable.xlsx。
    /// 数值与身份进表（同 CharacterRow 口径），敌人 prefab 按 id 命名走 Addressables 地址约定。
    /// 普攻经 normalAtkId 引用 SkillTable（敌人段编码：9+敌序号×100+槽位，如 EN_Solider_AR_0001 → 90101）。
    /// </summary>
    [MessagePackObject]
    public sealed class EnemyRow : IConfigTableRow<string>
    {
        // 敌人 ID，全表唯一主键；同时是 prefab 的 Addressables 地址（如 EN_Solider_AR_0001）
        [Key("id")]
        public string Id;

        // 敌人名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 阵营（当前恒 enemy；列与 CharacterRow.camp 对齐，留将来中立/敌内部分派）
        [Key("camp")]
        public string Camp;

        // 1 级基准生命（随单位等级按 LevelTable.multiplier 成长，与角色同机制；v1 复用角色曲线，
        // 将来独立敌用曲线 = 换表名一行）——BattleDataModule 装配期按出兵等级折算
        [Key("hp")]
        public int Hp;

        // 1 级基准攻击（同上，随等级成长）
        [Key("atk")]
        public int Atk;

        // 1 级基准防御（同上，随等级成长）
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

        // 攻击转向速度（度/秒；攻击面向目标时的固定转速，0 = 不转向）
        [Key("rotateSpeed")]
        public float RotateSpeed;

        // 攻击转向节点路径（空 = 转自身 root；非空 = transform.Find 相对路径，如坦克炮塔节点）
        [Key("rotateNode")]
        public string RotateNode;

        // 子弹锁头 XYZ 散射半径（米；枪械精度差异配置项，0 = 精准锁头，目标点不加随机偏移）
        [Key("bulletSpread")]
        public float BulletSpread;

        // 头部骨骼节点（prefab 内骨骼名或相对路径；子弹锁头与攻击转向取目标 head 世界位用，空 = 未配）
        [Key("headNode")]
        public string HeadNode;

        // 停走横向偏移（米；停走后沿朝向垂线随机左右偏移量，0 = 不偏移，聚堆错开用）
        [Key("stopLateralOffset")]
        public float StopLateralOffset;

        // 停走阻挡判定半径（米；找去路上"挡路角色"的垂距门槛，0 = 用默认 0.8——每种敌人体型不同，各自配）
        [Key("blockRadius")]
        public float BlockRadius;

        // 守点消耗值（每只敌人进入守点直接扣的守点血量；2026-10-27 守点交互重做——敌人不攻击守点，走进守点回收扣血）
        [Key("guardPointDamage")]
        public int GuardPointDamage;

        // 击杀回能（该敌人被击杀返给击杀者的能量；每种敌人不同，boss 可配多——表驱动能量经济 2026-10-28）
        [Key("energyOnKill")]
        public int EnergyOnKill;

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
