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
        // 物品 ID，与物品配置表主键对齐，0 保留不使用
        [Key(0)]
        public int itemId;

        // 该条目的物品数量
        [Key(1)]
        public long count;
    }
}
