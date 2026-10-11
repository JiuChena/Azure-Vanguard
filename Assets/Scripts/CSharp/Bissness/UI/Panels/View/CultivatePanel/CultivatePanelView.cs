using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成面板视图分区（MVC 的 V）：纯引用持有，不含逻辑。
/// 汇总左侧角色列表、页签与页面、属性页三个子分区。
/// </summary>
public class CultivatePanelView : MonoBehaviour
{
    [Header("左侧角色列表")]
    //角色选择列表分区：内容区/单元格模板
    public CultivateCharacterBarView characterBar;

    [Header("页签与页面")]
    //页签开关（Toggles/List 下 5 个：属性/武器/饰品/技能/突破[页未建]），下标与 pages 对齐，突破页除外
    public Toggle[] pageToggles;
    //页面节点（Pages 下 4 个：StatPage/GunPage/TrinketPage/AbilityPage；Ascend 页预制体未建）
    public GameObject[] pages;

    [Header("属性页")]
    //属性页分区：基础信息/属性面板/升级操作
    public CultivateStatPageView statPage;

    [Header("武器页")]
    //武器页分区：武器信息/属性/特殊词条/升级操作
    public CultivateWeaponPageView weaponPage;

    [Header("装备页")]
    //装备页分区：三装备槽位/合奏展示
    public CultivateTrinketPageView trinketPage;

    [Header("角色展示区")]
    //角色模型展示分区：模型锚点与朝向点
    public CultivateCharacterDisplayView displayView;
}
