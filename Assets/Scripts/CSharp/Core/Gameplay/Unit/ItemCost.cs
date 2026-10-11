using System;
using System.Collections.Generic;
using System.Text;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 物品消耗条目：表内消耗列的解析结果（格式 itemId:count，逗号分隔多组，如 "2:20000,1002:3"）。
    /// 物品 ID 为字符串；基础货币（信用点）也是物品，消耗统一走 ItemTable 主键。
    /// </summary>
    public readonly struct ItemCost
    {
        // 消耗的物品 id（ItemTable 主键，字符串）
        public readonly string ItemId;

        // 消耗数量
        public readonly int Count;

        public ItemCost(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        /// <summary>解析消耗字符串；空列返回空数组；非法片段跳过（导出工具已做格式校验的第一道关）。</summary>
        public static ItemCost[] ParseList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<ItemCost>();
            string[] parts = raw.Split(',');
            List<ItemCost> costs = new List<ItemCost>(parts.Length);
            foreach (string part in parts)
            {
                string token = part.Trim();
                if (token.Length == 0) continue;

                string[] pair = token.Split(':');
                if (pair.Length != 2) continue;
                string itemId = pair[0].Trim();
                if (itemId.Length == 0) continue;
                if (!int.TryParse(pair[1].Trim(), out int count)) continue;
                costs.Add(new ItemCost(itemId, count));
            }
            return costs.ToArray();
        }

        /// <summary>调试用文本（"1×20000, 1002×3"）。</summary>
        public override string ToString()
        {
            return $"{ItemId}×{Count}";
        }

        /// <summary>把消耗数组拼成调试文本。</summary>
        public static string Format(ItemCost[] costs)
        {
            if (costs == null || costs.Length == 0) return "(空)";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < costs.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(costs[i].ToString());
            }
            return sb.ToString();
        }
    }
}
