using System;
using System.Collections.Generic;
using MessagePack;

namespace Framework.Gameplay.Trinket
{
    /// <summary>
    /// 装备存档根对象，用于 MessagePack 持久化存储。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class TrinketData
    {
        // 存档版本号，未来数据迁移的唯一抓手；[Key] 契约只追加不重排
        [Key(0)]
        public int version = 1;

        // 实例 ID 自增计数器（从 1 起，0 留作无效值）；发放时取用后前移
        [Key(1)]
        public ulong nextInstanceId = 1;

        // 全部装备实例（含在穿与未穿）；逐件存在，同 itemId 可多条
        [Key(2)]
        public List<TrinketInstance> trinkets = new();
    }
}
