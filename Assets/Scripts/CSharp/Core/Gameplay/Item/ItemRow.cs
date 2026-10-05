using System;
using System.Collections.Generic;
using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Item
{
    /// <summary>
    /// 物品配置表行：对应 ConfigSources/Excel/ItemTable.xlsx，字段与 Excel 列一一对应（列名即 [Key] 名）。
    /// </summary>
    [MessagePackObject]
    public sealed class ItemRow : IConfigTableRow<int>
    {
        // 物品 ID，全表唯一主键
        [Key("id")]
        public int Id;

        // 物品名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 物品图标的资源名，运行时经图集/Addressables 取图
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
        public int PrimaryKey => Id;
    }
}
