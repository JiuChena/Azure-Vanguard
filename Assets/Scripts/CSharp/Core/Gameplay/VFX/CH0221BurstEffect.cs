using UnityEngine;

/// <summary>驱动 CH0221 一次性舞台爆发特效并在结束后关闭渲染。</summary>
public sealed class CH0221BurstEffect : MonoBehaviour
{
    // 播放持续时间，单位：秒；非正值使用材质默认值。
    [SerializeField, Min(0.2f), Tooltip("爆发持续时间，单位：秒")]
    private float duration = 1.8f;
    // 播放开始时是否自动重置并播放。
    [SerializeField, Tooltip("启用后在对象启用时自动播放一次")]
    private bool playOnEnable = true;

    // 本特效的所有渲染器，按层共享材质且不修改材质资产。
    private Renderer[] renderers;
    // 每个实例独立的材质属性块，避免运行时创建材质副本。
    private MaterialPropertyBlock propertyBlock;
    // 当前播放时间，单位：秒。
    private float elapsed;
    // 是否已经完成当前一次播放。
    private bool finished;

    /// <summary>缓存渲染器并根据配置决定是否开始播放。</summary>
    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
        if (playOnEnable) Play();
        else SetProgress(0f);
    }

    /// <summary>推进播放时间并在持续时间结束时关闭渲染。</summary>
    private void Update()
    {
        if (finished) return;
        elapsed += Time.deltaTime;
        SetProgress(elapsed);
        if (elapsed >= duration)
        {
            finished = true;
            SetRenderers(false);
        }
    }

    /// <summary>从零开始播放一次特效。</summary>
    public void Play()
    {
        elapsed = 0f;
        finished = false;
        SetRenderers(true);
        SetProgress(0f);
    }

    /// <summary>设置材质层的播放进度并同步持续时间。</summary>
    /// <param name="time">相对播放时间，单位：秒。</param>
    private void SetProgress(float time)
    {
        propertyBlock.SetFloat("_EffectTime", time);
        propertyBlock.SetFloat("_Duration", duration);
        foreach (Renderer renderer in renderers) renderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>统一开关所有爆发层，避免结束后继续消耗透明渲染。</summary>
    /// <param name="enabled">是否显示全部层。</param>
    private void SetRenderers(bool enabled)
    {
        foreach (Renderer renderer in renderers) renderer.enabled = enabled;
    }
}
