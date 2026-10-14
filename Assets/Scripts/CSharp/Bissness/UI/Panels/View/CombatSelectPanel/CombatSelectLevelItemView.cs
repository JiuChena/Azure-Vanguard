using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡列表单元格视图（MVC 的 V）：持有关卡条目预制体内的各个视觉组件引用。
/// 挂在 LevelItem 预制体根节点上，供 CombatSelectPanel 出池后填充数据。
/// </summary>
public class CombatSelectLevelItemView : MonoBehaviour
{
    [Header("背景与装饰")]
    // 背景底图（BG 节点）
    public Image bg;
    // 关卡评级评分图标（Score 节点，如 S 评级图）
    public Image scoreIcon;
    // 分割线（Split 节点）
    public Image split;

    [Header("文本与操作")]
    // 关卡名称文本（LevelName 节点，如 "1-1 Encounter"）
    public TMP_Text levelNameText;
    // 进入关卡按钮（Button 节点）
    public Button enterButton;
    // 按钮文案文本（Button/Text 节点，如 "Battle"）
    public TMP_Text enterButtonText;

    private void Awake()
    {
        EnsureBindings();
    }

    /// <summary>
    /// 自动绑定子节点引用，在未在 Inspector 中显式配置时作为安全兜底。
    /// </summary>
    public void EnsureBindings()
    {
        if (bg == null) bg = transform.Find("BG")?.GetComponent<Image>();
        if (scoreIcon == null) scoreIcon = transform.Find("Score")?.GetComponent<Image>();
        if (split == null) split = transform.Find("Split")?.GetComponent<Image>();
        if (levelNameText == null) levelNameText = transform.Find("LevelName")?.GetComponent<TMP_Text>();
        if (enterButton == null) enterButton = transform.Find("Button")?.GetComponent<Button>();
        if (enterButtonText == null && enterButton != null)
        {
            enterButtonText = enterButton.transform.Find("Text")?.GetComponent<TMP_Text>();
        }
    }
}
