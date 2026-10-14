using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 主城面板背景分区视图（MVC 的 V）：负责背景视频播放、渲染贴图承接与音频输出。
/// </summary>
public class HomePanel_BG : MonoBehaviour
{
    // 视频播放器组件，负责解码并播放背景视频。
    [SerializeField, Tooltip("背景视频播放器")]
    public VideoPlayer bgVideoPlayer;

    // 视频渲染目标贴图，VideoPlayer 的画面输出到此贴图。
    [SerializeField, Tooltip("视频渲染目标贴图")]
    public RenderTexture bgRenderTexture;

    // 背景原始图片组件，承接并展示 bgRenderTexture。
    [SerializeField, Tooltip("背景渲染展示组件")]
    public RawImage bgRawImage;

    // 背景音频源组件，负责视频伴音或背景环境音输出。
    [SerializeField, Tooltip("背景音频源")]
    public AudioSource bgAudioSource;

    #region 视频控制

    /// <summary>
    /// 设置视频剪辑并开始循环播放，将画面输出至渲染贴图。
    /// </summary>
    /// <param name="clip">待播放的视频剪辑资产；为 null 时不执行播放。</param>
    public void PlayVideo(VideoClip clip)
    {
        // 参数与组件有效性校验
        if (bgVideoPlayer == null || clip == null) return;

        // 配置视频剪辑与渲染目标
        bgVideoPlayer.clip = clip;
        bgVideoPlayer.targetTexture = bgRenderTexture;
        if (bgRawImage != null) bgRawImage.texture = bgRenderTexture;
        bgVideoPlayer.isLooping = true;

        // 开始播放视频
        bgVideoPlayer.Play();
    }

    /// <summary>
    /// 停止视频播放并释放贴图缓冲区，防止在下次播放前残留上一帧画面。
    /// </summary>
    public void StopVideo()
    {
        // 停止播放器并清空剪辑引用
        if (bgVideoPlayer != null)
        {
            bgVideoPlayer.Stop();
            bgVideoPlayer.clip = null;
        }

        // 释放渲染贴图内存缓冲区，清除残影
        if (bgRenderTexture != null) bgRenderTexture.Release();
    }

    #endregion
}
