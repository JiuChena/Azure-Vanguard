using TMPro;
using UnityEngine;

/// <summary>
/// 通知面板 View（MVC 的 V）：通知容器与文本的引用集合，自身零逻辑。
/// 复用组件约定：挂上即工作——Control 经 <see cref="SetMessage"/> 写入当前消息；
/// 容器的显隐与出入场由 Animator（NoticeTrigger）驱动，容器空消息时保持隐藏态。
/// 预制体结构：面板根（PanelBase + Animator）下挂本组件，container 指向动画驱动的展示根。
/// </summary>
public class NoticePanelView : MonoBehaviour
{
    //通知容器：Animator NoticeTrigger 驱动的展示根（有消息时显示，无消息隐藏）
    public GameObject container;

    //通知文本：当前展示的消息内容
    public TMP_Text noticeText;

    /// <summary>写入当前展示的消息；未接引用时跳过，避免空引用。</summary>
    public void SetMessage(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message;
    }
}
