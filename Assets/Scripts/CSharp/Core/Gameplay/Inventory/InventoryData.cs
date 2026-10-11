using System;
using System.Collections.Generic;
using MessagePack;

namespace Framework.Gameplay.Inventory
{
    /// <summary>
    /// 背包存档根对象，用于 MessagePack 持久化存储。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class InventoryData
    {
        // 存档版本号，未来数据迁移的唯一抓手；[Key] 契约只追加不重排
        // v2（2026-10-09）：物品 ID int→string，旧档反序列化必然失败，加载端兜底重置
        [Key(0)]
        public int version = 2;

        // 全部物品条目；同一物品 ID 只保留一条，脏数据在加载时合并剔除
        [Key(1)]
        public List<ItemStack> items = new();
    }
}
