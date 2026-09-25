using Core.Gear;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PreloadPanel : PanelBase
{
    // 溶解材质引用；溶解度直接写入该材质的 _DissolveAmount
    [SerializeField] private Material dissolveMaterial;

    // 溶解度（0 完整显示，1 完全消失），变更时同步到材质
    [Range(0f, 1f)]
    [SerializeField] private float dissolveAmount;

    // 加载进度文本（0~100%），由 EventNames.LoadSceneProgress 事件驱动刷新
    [SerializeField] private TMP_Text processText;

    // 防止进度事件重复触发关闭流程
    private bool loadFinished;

    // 开场动画是否已播完（播完前不开始场景加载）
    private bool displayAnimDone;

    // 开场动画状态哈希（首次进入有效状态时捕获，即 Animator 默认状态）
    private int displayStateHash;
    // 是否已捕获开场动画状态
    private bool displayStateCaptured;

    // 开场动画完成回调（Preloader 注册，动画播完才开始场景加载）
    private UnityAction displayFinished;

    // 材质属性 ID 缓存，避免每次 SetFloat 字符串查找
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    /// <summary>
    /// 溶解度参数：0 完整显示，1 完全消失；赋值即写入溶解材质。
    /// </summary>
    public float DissolveAmount
    {
        get => dissolveAmount;
        set
        {
            dissolveAmount = Mathf.Clamp01(value);
            ApplyDissolve();
        }
    }

    protected override void EventInit()
    {
        // 监听场景异步加载进度（0~1），由 LoadSceneManager 每帧广播
        EventCenter.Instance.Register<float>(EventNames.LoadSceneProgress, OnLoadProgressChanged);
    }

    protected override void ComponentInit()
    {
        // 初始化时把当前溶解度写入材质
        ApplyDissolve();
    }

    protected override void OnUpdate()
    {
        ApplyDissolve();

        // 开场动画播完后触发一次回调，之后不再检测
        if (!displayAnimDone && IsDisplayAnimFinished())
        {
            displayAnimDone = true;
            displayFinished?.Invoke();
            displayFinished = null;
        }
    }

    /// <summary>
    /// 注册开场动画播完后的回调；若动画已播完（或面板无开场动画）则立即执行。
    /// </summary>
    /// <param name="action">动画完成后要执行的动作。</param>
    public void AddDisplayFinishedListener(UnityAction action)
    {
        if (displayAnimDone) action?.Invoke();
        else displayFinished += action;
    }

    public override void DisplayPanel()
    {
        // 开场动画由 Animator 默认状态自动播放，这里只重置完成标记与状态捕获
        displayAnimDone = false;
        displayStateCaptured = false;
    }

    /// <summary>
    /// 开场动画是否播完：离开默认状态（自动过渡走）或默认状态 normalizedTime ≥ 1。
    /// 无 Animator / 无控制器视为无开场动画，直接放行。
    /// </summary>
    private bool IsDisplayAnimFinished()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return true;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        // 刚实例化的前几帧可能还没进入有效状态（length 为 0），等拿到真实状态再捕获判定
        if (info.length <= 0f) return false;

        if (!displayStateCaptured)
        {
            displayStateHash = info.shortNameHash;
            displayStateCaptured = true;
            return false;
        }

        return info.shortNameHash != displayStateHash || info.normalizedTime >= 1f;
    }

    public override void OnEscapePressed() { }

    /// <summary>
    /// 销毁时反注册进度监听；事件中心为静态单例，不反注册会持有已销毁面板的回调。
    /// </summary>
    protected override void OnDestroy()
    {
        EventCenter.Instance.Unregister<float>(EventNames.LoadSceneProgress, OnLoadProgressChanged);
        base.OnDestroy();
    }

    /// <summary>
    /// 场景加载进度回调：刷新进度文本，进度满时关闭面板进入主城。
    /// </summary>
    /// <param name="progress">加载进度 0~1。</param>
    private void OnLoadProgressChanged(float progress)
    {
        // 刷新进度文本为整数百分比
        if (processText != null)
            processText.text = $"Loading: {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100)}%";

        // 进度满时走 PanelManager 关闭流程（播退出动画 + 延迟销毁 + 释放资源作用域）
        if (progress >= 1f && !loadFinished)
        {
            loadFinished = true;
            PanelManager.Instance.ClosePanel(this);
        }
    }

    /// <summary>
    /// 将当前溶解度写入溶解材质；材质缺少 _DissolveAmount 属性时由 Unity 静默忽略。
    /// </summary>
    private void ApplyDissolve()
    {
        if (dissolveMaterial == null) return;
        dissolveMaterial.SetFloat(DissolveAmountId, dissolveAmount);
    }
}
