using System;
using System.Collections.Generic;
using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 突破表行：对应 Assets/Module/Unit/Character/Excel/BreakthroughTable.xlsx。
    /// 行 = 突破阶段（全角色共享一张表）：突破后等级上限提升、消耗材料、
    /// 可选附带解锁属性加成/天赋节点（原神式突破解锁）。
    /// </summary>
    [MessagePackObject]
    public sealed class BreakthroughRow : IConfigTableRow<int>
    {
        // 突破阶段（1 起），全表唯一主键
        [Key("breakStage")]
        public int BreakStage;

        // 突破到该阶段后的等级上限
        [Key("levelCap")]
        public int LevelCap;

        // 突破消耗 itemId:count 逗号分隔（金币 = 1）
        [Key("costItems")]
        public string CostItems;

        // 突破附带解锁的节点 id（UnlockNodeTable 主键），逗号分隔；空 = 无附带解锁
        [Key("unlockNodeIds")]
        public string UnlockNodeIds;

        /// <summary>解析后的突破消耗。</summary>
        [IgnoreMember]
        public ItemCost[] CostList => ItemCost.ParseList(CostItems);

        /// <summary>解析后的附带解锁节点 id 集合；空列返回空数组。</summary>
        [IgnoreMember]
        public int[] UnlockNodeIdList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UnlockNodeIds)) return Array.Empty<int>();
                string[] parts = UnlockNodeIds.Split(',');
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
        public int PrimaryKey => BreakStage;
    }
}
