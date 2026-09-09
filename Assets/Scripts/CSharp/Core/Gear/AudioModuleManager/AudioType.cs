namespace Core.Gear
{
    /// <summary>
    /// 音频类型。用于区分背景音乐与普通音效，决定读取哪组玩家音量设置。
    /// </summary>
    public enum AudioType
    {
        Music,
        Sound,
    }
}
