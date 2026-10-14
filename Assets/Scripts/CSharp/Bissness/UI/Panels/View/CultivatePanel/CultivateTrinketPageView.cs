using TMPro;
using UnityEngine;

/// <summary>
/// 养成面板装备页视图分区（MVC 的 V）：纯引用持有，不含逻辑。
/// 挂在 Pages/TrinketPage 上；槽位内容与合奏展示由 Control 绑定。
/// </summary>
public class CultivateTrinketPageView : MonoBehaviour
{
    [Header("装备槽位")]
    //装备槽位（TrinketContainer 下 3 个，不绑类别；下标即槽位顺序）
    public TrinketSlotView[] slots;

    [Header("合奏展示")]
    //合奏加成文本（SetBonus/Scroll View/Viewport/Content；触发合奏时写描述，未触发清空）
    public TMP_Text setBonusText;

    [Header("装备背包")]
    //背包分区（TrinketBag）：条目列表/类别与套装筛选/详情卡，内容由 Control 绑定
    public TrinketBagView bag;
}
