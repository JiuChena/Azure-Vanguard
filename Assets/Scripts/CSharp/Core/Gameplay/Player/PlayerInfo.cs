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
        // 默认名片头像：单图 Addressables 地址（= png 文件名去扩展，Sprite-Character-Avatar-PlayerAvatar 组）
        public const string DefaultAvatarName = "CHAvatar_PlayerAvatar_CH0221";

        // 旧图集方案时代的默认头像名：已随头像单图化从 Addressables 移除，仅作存量档迁移判据
        public const string LegacyAvatarName = "Student_Portrait_Airi_Small";

        // 存档版本号，未来数据迁移的唯一抓手；[Key] 契约只追加不重排
        [Key(0)]
        public int version = 1;

        // 当前使用头像的资源名称（单图 Addressables 地址，如 CHAvatar_PlayerAvatar_CH0221）
        [Key(1)]
        public string avatarName = DefaultAvatarName;

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
