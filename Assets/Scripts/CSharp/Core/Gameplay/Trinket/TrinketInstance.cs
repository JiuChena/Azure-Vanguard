using System;
using MessagePack;

namespace Framework.Gameplay.Trinket
{
    /// <summary>
    /// 装备实例：装备逐件存在（同 itemId 可多件，各自可穿、互不影响），instanceId 唯一。
    /// wearerId 是单一事实源——装备那一刻写入持有角色 id，空串 = 未穿；
    /// 角色身上三件 = 扫实例反查（wearerId == characterId），CharacterEntry 零改动。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class TrinketInstance
    {
        // 实例唯一 ID（自增，0 为无效值）
        [Key(0)]
        public ulong instanceId;

        // 装备 id（权威，与 TrinketTable 主键对齐；类别/合奏/词条/图标全部查表派生）
        [Key(1)]
        public string itemId;

        // 持有角色 id（空 = 未穿；背包页显示"谁在穿"直读此字段 → 按角色 id 查小头像）
        [Key(2)]
        public string wearerId = "";
    }
}
