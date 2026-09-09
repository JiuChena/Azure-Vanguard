using System;
using Core.Gear;
using UnityEngine;

/// <summary>
/// 全局音频设置管理器。负责音频设置的本地持久化，并通过 SettingsChanged 事件广播音量/开关变更。
/// </summary>
public class AudioDataManager
{
    private const string SaveFolder = "PlayerData/Setting/";
    private const string SaveFileName = "GlobalAudio";

    private static AudioDataManager instance;
    public static AudioDataManager Instance => instance ??= new AudioDataManager();

    // 当前音频设置数据
    private AudioData data;

    private AudioDataManager()
    {
        LoadData();
        ApplyRuntimeSettings();
    }

    // 当前设置数据的只读访问
    public AudioData Data
    {
        get
        {
            data ??= new AudioData();
            return data;
        }
    }

    // 设置变更事件，AudioManager 集中订阅以统一刷新音量/开关
    public event Action<AudioData> SettingsChanged;

    #region 设置读写

    /// <summary>
    /// 批量写入设置数据并保存。
    /// </summary>
    /// <param name="nextData">新的设置数据</param>
    public void PushData(AudioData nextData)
    {
        if (nextData == null) return;

        // 复制设置值
        Data.musicEnabled = nextData.musicEnabled;
        Data.musicVolume = Mathf.Clamp01(nextData.musicVolume);
        Data.soundEnabled = nextData.soundEnabled;
        Data.soundVolume = Mathf.Clamp01(nextData.soundVolume);

        // 应用并保存
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音乐开关。
    /// </summary>
    public void SetMusicEnabled(bool enabled)
    {
        if (Data.musicEnabled == enabled) return;

        Data.musicEnabled = enabled;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音乐音量（0-1）。
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Data.musicVolume, clampedVolume)) return;

        Data.musicVolume = clampedVolume;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音效开关。
    /// </summary>
    public void SetSoundEnabled(bool enabled)
    {
        if (Data.soundEnabled == enabled) return;

        Data.soundEnabled = enabled;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音效音量（0-1）。
    /// </summary>
    public void SetSoundVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Data.soundVolume, clampedVolume)) return;

        Data.soundVolume = clampedVolume;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 将当前设置序列化到本地文件。
    /// </summary>
    public void SaveData()
    {
        BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, Data);
    }

    /// <summary>
    /// 从本地文件加载设置数据，文件不存在时使用默认值。
    /// </summary>
    public void LoadData()
    {
        data = BinaryDataManager.Instance.Load<AudioData>(SaveFolder, SaveFileName) ?? new AudioData();
    }

    #endregion

    #region Private

    /// <summary>
    /// 应用当前设置并广播设置变更事件，订阅方（AudioManager）据此统一刷新音量。
    /// </summary>
    private void ApplyRuntimeSettings()
    {
        SettingsChanged?.Invoke(Data);
    }

    #endregion
}