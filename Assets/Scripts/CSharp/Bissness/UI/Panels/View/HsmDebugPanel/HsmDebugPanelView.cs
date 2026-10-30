using TMPro;
using UnityEngine;

/// <summary>
/// HSM 状态调试面板根视图（MVC 的 V）：持有选中角色名、上一个/当前状态、状态参数与转换日志的文本引用。
/// 挂在 HsmDebugPanel 预制体根节点上；引用由 SetupHsmDebugPanel 接线，EnsureBindings 仅作未接线兜底。
/// 文本内容一律英文（字体缺中文字形）。
/// </summary>
public class HsmDebugPanelView : MonoBehaviour
{
    [Header("Header（选中角色）")]
    // 选中角色名与阵营（无选中时显示提示）
    public TMP_Text unitNameText;

    [Header("States（状态）")]
    // 上一个状态名（无转换记录时 None）
    public TMP_Text prevStateText;
    // 当前状态名
    public TMP_Text currStateText;

    [Header("Params（状态相关参数）")]
    // HSM 上下文标志 + 模拟域参数（多行）
    public TMP_Text paramsText;

    [Header("Transition Log（最近转换）")]
    // 最近状态转换记录（追加式，最新在最后一行）
    public TMP_Text transitionLogText;

    private void Awake()
    {
        EnsureBindings();
    }

    /// <summary>
    /// 自动绑定子节点引用，在未在 Inspector 中显式配置时作为安全兜底。
    /// </summary>
    public void EnsureBindings()
    {
        Transform header = transform.Find("Header");
        if (header != null && unitNameText == null)
        {
            unitNameText = header.Find("UnitNameText")?.GetComponent<TMP_Text>();
        }
        Transform states = transform.Find("States");
        if (states != null)
        {
            if (prevStateText == null) prevStateText = states.Find("PrevStateText")?.GetComponent<TMP_Text>();
            if (currStateText == null) currStateText = states.Find("CurrStateText")?.GetComponent<TMP_Text>();
        }
        if (paramsText == null) paramsText = transform.Find("ParamsText")?.GetComponent<TMP_Text>();
        if (transitionLogText == null) transitionLogText = transform.Find("TransitionLogText")?.GetComponent<TMP_Text>();
    }
}
