using Core.Gear;
using UnityEngine;

/// <summary>
/// 常驻通知面板（UILayer.System，开局即开、永不关闭）：单条消息模型——Submit 立即覆盖旧
/// 文案并重触发入场动画（连发即刷掉上一条；控制器 Any State→入场 自转场可重入），静默
/// popupInterval 秒后容器回落（Hide=true），期间来新消息则计时复位。
/// 提交途径（二选一，殊途同 Submit）：
/// ① 任意系统经 EventCenter 发布 EventNames.NoticeRequested（Core 层拒绝提示走这条，不依赖 UI）；
/// ② 直接 PanelManager.Instance.GetPanel&lt;NoticePanel&gt;(UIPath.NOTICEPANEL)?.Submit("...")。
/// 文案一律英文（代码流向 .text 的字符串不写中文，字体缺中文字形）。
/// </summary>
public class NoticePanel : PanelBase
{
    //单条消息在容器中的停留时长（秒）；期间来新消息则计时复位重触发
    [SerializeField] private float popupInterval = 2f;

    //View 引用（MVC 的 V）：容器与文本引用集合
    private NoticePanelView view;

    //当前是否有消息在展示（静默期 false，容器已由 Hide 驱动回落）
    private bool occupied;

    //当前消息的剩余停留时间
    private float remain;

    //NoticeTrigger 参数哈希缓存，避免每次 SetTrigger 字符串查找
    private static readonly int NoticeTriggerId = Animator.StringToHash("NoticeTrigger");

    //Hide Bool 参数哈希：容器回落为空时置 true（播退场），有消息时置 false
    private static readonly int HideId = Animator.StringToHash("Hide");

    protected override void EventInit()
    {
        //核心层的拒绝提示经事件总线回流（Core 不依赖 UI），本面板是消费端
        EventCenter.Instance.Register<string>(EventNames.NoticeRequested, Submit);
    }

    protected override void ComponentInit()
    {
        //View 挂在面板子层级（不限定直接子级，容器结构自由）
        view = GetComponentInChildren<NoticePanelView>(true);
    }

    public override void DisplayPanel() { } //常驻面板，无入场逻辑

    public override void OnEscapePressed() { } //常驻面板，ESC 不关闭

    /// <summary>
    /// 提交一条通知：立即覆盖旧文案并重触发入场动画（连发即刷掉上一条），停留计时复位。
    /// </summary>
    /// <param name="message">通知文案（英文；字体缺中文字形）。</param>
    public void Submit(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (view != null) view.SetMessage(message);
        remain = Mathf.Max(0.01f, popupInterval);
        occupied = true;
        animator.SetBool(HideId, false);
        animator.SetTrigger(NoticeTriggerId);
    }

    protected override void OnUpdate()
    {
        if (!occupied) return;

        remain -= Time.deltaTime;
        if (remain > 0f) return;

        //静默到点：容器回落为空（Hide=true），下一条消息 Submit 时再触发
        occupied = false;
        animator.SetBool(HideId, true);
    }

    /// <summary>销毁时反注册事件监听；事件中心为静态单例，不反注册会持有已销毁面板的回调。</summary>
    protected override void OnDestroy()
    {
        EventCenter.Instance.Unregister<string>(EventNames.NoticeRequested, Submit);
        base.OnDestroy();
    }
}
