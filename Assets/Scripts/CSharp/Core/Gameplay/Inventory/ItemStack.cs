using System;
using MessagePack;

namespace Framework.Gameplay.Inventory
{
    /// <summary>
    /// 背包物品条目，仅存物品 ID 与数量，用于 MessagePack 持久化存储。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class ItemStack
    {
        // 物品 ID（字符串，与物品配置表主键对齐；v2 起由 int 改 string，旧档不兼容，加载失败即重置）
        [Key(0)]
        public string itemId;

        // 该条目的物品数量
        [Key(1)]
        public long count;
    }
}
