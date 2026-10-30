using System;
using System.Collections.Generic;
using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 角色配置表行：对应 Assets/Module/Unit/Character/Excel/CharacterTable.xlsx，
    /// 字段与 Excel 列一一对应（列名即 [Key] 名）。
    /// 本表只管数值与身份（策划可调），图标/BEclip 等资产引用归 UnitConfig（SO）。
    /// </summary>
    [MessagePackObject]
    public sealed class CharacterRow : IConfigTableRow<string>
    {
        // 单位 ID，全表唯一主键；与 UnitConfig.unitId、头像图集 sprite 名、存档仓库键三方对齐（如 CH0221）
        [Key("id")]
        public string Id;

        // 角色名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 稀有度等级，数值越大越稀有
        [Key("rarity")]
        public int Rarity;

        // 职业定位（如 support/dps/tank，枚举接入后替换）
        [Key("profession")]
        public string Profession;

        // 角色主要定位类型（对应 Tag 图集中的 Tag_Small_DPS / Tag_Small_Defence / Tag_Small_Support / Tag_Small_Heal）
        [Key("archetype")]
        public string Archetype;

        // 阵营（如 student/enemy）
        [Key("camp")]
        public string Camp;

        // 局内放置点数消耗；服务端台账校验（Σ放置支出 ≤ 推算上限）对账用
        [Key("cost")]
        public int Cost;

        // 基础生命（1 级白板值）
        [Key("hp")]
        public int Hp;

        // 基础攻击（1 级白板值）
        [Key("atk")]
        public int Atk;

        // 基础防御（1 级白板值）
        [Key("def")]
        public int Def;

        // 攻击间隔（秒）
        [Key("atkInterval")]
        public float AtkInterval;

        // 射程（格）
        [Key("range")]
        public float Range;

        // 移动速度（格/秒）
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

        // 普攻技能 id（SkillTable 主键）
        [Key("normalAtkId")]
        public int NormalAtkId;

        // 小技能 E 的技能 id（SkillTable 主键）
        [Key("skillEId")]
        public int SkillEId;

        // 爆发技能 Q 的技能 id（SkillTable 主键）
        [Key("skillQId")]
        public int SkillQId;

        // 能量上限（每角色不同；Q 大招施放消耗的能量门槛，CH0221=80）
        [Key("maxEnergy")]
        public int MaxEnergy;

        // 天赋节点 id，逗号分隔多值（UnlockNodeTable 主键）
        [Key("talentIds")]
        public string TalentIds;

        // 属性加成节点 id，逗号分隔多值（UnlockNodeTable 主键）
        [Key("bonusIds")]
        public string BonusIds;

        // 角色标签声明，逗号分隔多值；可带方括号附带属性（如 Indoor[Feeling_0]，格式见 CharacterTagDecl）
        [Key("tags")]
        public string Tags;

        /// <summary>解析后的天赋节点 id 集合；空列返回空数组。</summary>
        [IgnoreMember]
        public int[] TalentIdList => ParseIdList(TalentIds);

        /// <summary>解析后的属性加成节点 id 集合；空列返回空数组。</summary>
        [IgnoreMember]
        public int[] BonusIdList => ParseIdList(BonusIds);

        /// <summary>解析后的角色标签声明（名称 + 方括号附带属性）；空列返回空数组。</summary>
        [IgnoreMember]
        public CharacterTagDecl[] TagList => CharacterTagDecl.ParseList(Tags);

        /// <summary>解析逗号分隔的 int 列表；空/碎片自动滤除。</summary>
        private static int[] ParseIdList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<int>();
            string[] parts = raw.Split(',');
            List<int> ids = new List<int>(parts.Length);
            foreach (string part in parts)
            {
                string token = part.Trim();
                if (token.Length > 0 && int.TryParse(token, out int id)) ids.Add(id);
            }
            return ids.ToArray();
        }

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
