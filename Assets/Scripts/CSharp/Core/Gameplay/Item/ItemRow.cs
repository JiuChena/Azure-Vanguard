using System;
using System.Collections.Generic;
using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Item
{
    /// <summary>
    /// 物品配置表行：对应 ConfigSources/Excel/ItemTable.xlsx，字段与 Excel 列一一对应（列名即 [Key] 名）。
    /// 物品 ID 为字符串（2026-10-09 起，设计约定 id 类型先问策划）。
    /// </summary>
    [MessagePackObject]
    public sealed class ItemRow : IConfigTableRow<string>
    {
        // 物品 ID，全表唯一主键（字符串）
        [Key("id")]
        public string Id;

        // 物品名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 物品图标的 Addressables 地址（与图标组内条目名一字不差，如 Item_Icon_Currency_CreditPoint；
        // 图标分组的组内名称即映射真相，本列就是 id→地址 的映射表）
        [Key("icon")]
        public string Icon;

        // 物品类型（1=消耗品 2=材料 3=装备…，枚举接入后替换）
        [Key("type")]
        public int Type;

        // 品质等级，数值越大越稀有
        [Key("quality")]
        public int Quality;

        // 单格最大堆叠数，1 表示不可堆叠
        [Key("maxStack")]
        public int MaxStack;

        // 商店售价（货币类型接入经济系统后再细分）
        [Key("price")]
        public int Price;

        // 可售出价，0 表示不可售（与 tags 的 sell 标签配套）
        [Key("sellPrice")]
        public int SellPrice;

        // 物品描述文本
        [Key("desc")]
        public string Desc;

        // 功能标签，逗号分隔多标签（如 usable,sell,batch）；空 = 纯展示物品。分发见 ItemFeatureCatalog
        [Key("tags")]
        public string Tags;

        // 使用后提供的升级经验（技能书专用，养成面板"点击使用技能书升级"读取本列）；非经验物品为 0
        [Key("exp")]
        public int Exp;

        /// <summary>解析后的功能标签集合；空标签行返回空数组，标签精确匹配不分大小写变体。</summary>
        [IgnoreMember]
        public string[] TagList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Tags)) return Array.Empty<string>();
                string[] parts = Tags.Split(',');
                List<string> tags = new List<string>(parts.Length);
                foreach (string part in parts)
                {
                    string tag = part.Trim();
                    if (tag.Length > 0) tags.Add(tag);
                }
                return tags.ToArray();
            }
        }

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
