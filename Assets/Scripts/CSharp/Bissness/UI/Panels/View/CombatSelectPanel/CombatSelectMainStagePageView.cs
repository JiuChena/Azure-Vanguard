using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主关卡页面视图（MVC 的 V）：持有主关卡子页面信息头与关卡滚动列表的引用。
/// 挂在 Pages/MainStagePage 节点上。
/// </summary>
public class CombatSelectMainStagePageView : MonoBehaviour
{
    [Header("章节信息头（SubPage）")]
    // 章节底图（SubPage/BG）
    public Image subPageBg;
    // 章节立绘/缩略图（SubPage/Mask/Image）
    public Image subPageImage;
    // 章节完成标记（SubPage/Mask/Complete）
    public Image completeMark;
    // 章节标题文本（SubPage/Title，如 "Chapter 01 - Operation Vanguard"）
    public TMP_Text titleText;
    // 分割线（SubPage/Split）
    public Image split;
    // 进度文本（SubPage/Process，如 "6 / 6" 或 "100%"）
    public TMP_Text processText;
    // S 评级标志（SubPage/S-Icon）
    public Image sIcon;

    [Header("关卡列表（SelectView）")]
    // 滚动区域（SelectView/Scroll View）
    public ScrollRect stageScrollRect;
    // 关卡条目父容器（SelectView/Scroll View/Viewport/Content）
    public RectTransform stageContent;
    // 关卡条目预制体（LevelItem）
    public GameObject levelItemPrefab;

    private void Awake()
    {
        EnsureBindings();
    }

    /// <summary>
    /// 自动绑定子节点引用，在未在 Inspector 中显式配置时作为安全兜底。
    /// </summary>
    public void EnsureBindings()
    {
        if (subPageBg == null) subPageBg = transform.Find("SubPage/BG")?.GetComponent<Image>();
        if (subPageImage == null) subPageImage = transform.Find("SubPage/Mask/Image")?.GetComponent<Image>();
        if (completeMark == null) completeMark = transform.Find("SubPage/Mask/Complete")?.GetComponent<Image>();
        if (titleText == null) titleText = transform.Find("SubPage/Title")?.GetComponent<TMP_Text>();
        if (split == null) split = transform.Find("SubPage/Split")?.GetComponent<Image>();
        if (processText == null) processText = transform.Find("SubPage/Process")?.GetComponent<TMP_Text>();
        if (sIcon == null) sIcon = transform.Find("SubPage/S-Icon")?.GetComponent<Image>();

        if (stageScrollRect == null) stageScrollRect = transform.Find("SelectView/Scroll View")?.GetComponent<ScrollRect>();
        if (stageContent == null) stageContent = transform.Find("SelectView/Scroll View/Viewport/Content") as RectTransform;
    }
}
