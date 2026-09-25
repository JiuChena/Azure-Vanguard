using System;
using MessagePack;

namespace Framework.Gameplay.Player
{
    /// <summary>
    /// 玩家信息存档根对象，用于 MessagePack 持久化存储。
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class PlayerInfo
    {
        // 存档版本号，未来数据迁移的唯一抓手；[Key] 契约只追加不重排
        [Key(0)]
        public int version = 1;

        // 当前使用头像的资源名称（头像图集内的 sprite 名，如 Student_Portrait_Airi_Small）
        [Key(1)]
        public string avatarName = "Student_Portrait_Airi_Small";

        // 玩家昵称
        [Key(2)]
        public string nickName = "PlayerName";

        // 玩家等级（展示文本由 UI 层格式化为 Lv.X）
        [Key(3)]
        public int level = 1;

        // 当前等级已累积经验
        [Key(4)]
        public int exp;

        // 当前等级升级所需经验
        [Key(5)]
        public int maxExp = 100;
    }
}
