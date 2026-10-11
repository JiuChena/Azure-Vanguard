using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成面板装备页单槽视图（TrinketPage/TrinketContainer 下三个槽位）：纯引用持有，不含逻辑。
/// 每槽 = 图标 + 空槽提示 + 主/副词条文本；词条行模板由预制体静态串提供（"{0} +{1}%"，Control 捕获替换）。
/// 槽位不绑类别（三自由格）；更换按钮为后续换装面板预留，本轮不接。
/// </summary>
public class TrinketSlotView : MonoBehaviour
{
    //装备图标（IconBG/Icon；TrinketRow.icon 单图按需加载，未命中保留默认图）
    public Image iconImg;

    //空槽提示文本（IconBG/NullText；空槽激活，有件失活）
    public TMP_Text nullText;

    //主词条文本（Property/Main Stat/Text；模板 "{0} +{1}%"，{0}=加成名称 {1}=数值；空槽直写 "-"）
    public TMP_Text mainStatText;

    //副词条文本 ×3（Property/Sub Stat 1/2/3/Text；模板与主词条同口径）
    public TMP_Text[] subStatTexts;

    //当前绑定的装备实例 ID：异步图标回包校验（防串写），运行时由 Control 绑定（0 = 空槽）
    [System.NonSerialized] public ulong instanceId;
}
