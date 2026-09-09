using System;
using MessagePack;

/// <summary>
/// 全局音频设置数据，用于 MessagePack 持久化存储。
/// </summary>
[Serializable]
[MessagePackObject]
public class AudioData
{
    // 音乐开关
    [Key(0)]
    public bool musicEnabled = true;

    // 音乐音量（0-1）
    [Key(1)]
    public float musicVolume = 0.5f;

    // 音效开关
    [Key(2)]
    public bool soundEnabled = true;

    // 音效音量（0-1）
    [Key(3)]
    public float soundVolume = 0.5f;
}
