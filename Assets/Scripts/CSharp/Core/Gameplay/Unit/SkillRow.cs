using System;
using System.Collections.Generic;
using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能骨架表行：对应 Assets/Module/Unit/Character/Excel/SkillTable.xlsx。
    /// 技能是骨架，具体效果在 SkillEffectTable 一效果一行，本表经 effectIds 引用。
    /// 技能 id 编码 = 角色 id×100 + 槽位（01 普攻 / 02 E / 03 Q），如 22102。
    /// </summary>
    [MessagePackObject]
    public sealed class SkillRow : IConfigTableRow<int>
    {
        /// <summary>技能槽位类型值（type 列的约定取值）。</summary>
        public static class SkillTypes
        {
            // 普攻
            public const string Normal = "normal";
            // 小技能 E
            public const string E = "e";
            // 爆发技能 Q
            public const string Burst = "burst";
        }

        // 技能 id，全表唯一主键
        [Key("skillId")]
        public int SkillId;

        // 技能名称（显示用）
        [Key("name")]
        public string Name;

        // 技能类型：normal普攻 / e小技能 / burst爆发
        [Key("type")]
        public string Type;

        // 最大等级（普攻/E/Q 为 1~10，天赋/加成节点无等级不进本表）
        [Key("maxLevel")]
        public int MaxLevel;

        // 冷却秒数，0 = 无冷却
        [Key("cooldown")]
        public float Cooldown;

        // 局内点数消耗
        [Key("cost")]
        public int Cost;

        // 效果 id 列表，逗号分隔（SkillEffectTable 主键）
        [Key("effectIds")]
        public string EffectIds;

        // 技能描述文本
        [Key("desc")]
        public string Desc;

        /// <summary>解析后的效果 id 集合；空列返回空数组。</summary>
        [IgnoreMember]
        public int[] EffectIdList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(EffectIds)) return Array.Empty<int>();
                string[] parts = EffectIds.Split(',');
                List<int> ids = new List<int>(parts.Length);
                foreach (string part in parts)
                {
                    string token = part.Trim();
                    if (token.Length > 0 && int.TryParse(token, out int id)) ids.Add(id);
                }
                return ids.ToArray();
            }
        }

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => SkillId;
    }
}
