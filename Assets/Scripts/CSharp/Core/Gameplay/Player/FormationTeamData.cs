using System;
using System.Collections.Generic;
using MessagePack;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 编队存档根对象，用于 MessagePack 持久化存储。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class FormationTeamData
    {
        //编队套数（LeftBar 页签 01~06）
        public const int TeamCount = 6;

        //每套编队槽位数（战备面板 8 格）
        public const int TeamSlotCount = 8;

        // 存档版本号，未来数据迁移的唯一抓手；[Key] 契约只追加不重排
        [Key(0)]
        public int version = 1;

        // 当前选中编队索引（0 起）
        [Key(1)]
        public int currentTeamIndex;

        // 全部编队：teams[套索引][槽位] = unitId（如 CH0221），空串 = 未编队
        [Key(2)]
        public List<string[]> teams = new();
    }
}
