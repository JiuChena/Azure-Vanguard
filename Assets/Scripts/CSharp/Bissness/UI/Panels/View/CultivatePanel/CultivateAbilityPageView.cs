using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成面板技能页视图分区（MVC 的 V）：纯引用持有，不含逻辑。
/// 挂在 Pages/AbilityPage 上；技能定位/按等级取值/描述替换由 Control 经 SkillDataLibrary 绑定。
/// </summary>
public class CultivateAbilityPageView : MonoBehaviour
{
    [Header("技能格")]
    //技能格开关（Abilities 下逐 Item 的 Icon Toggle，ToggleGroup 单选，Mark 由 Toggle transition 驱动；
    //绑定顺序 = 普攻/天赋/E/Q，多于技能行数的格由 Control 隐藏）
    public Toggle[] skillToggles;

    [Header("升级区")]
    //升级消耗列表内容区（Bot/Left/Breakthrough/Viewport/Content，属性页突破列表同构，池化 CostItem）
    public RectTransform upgradeContent;

    //升级按钮（Bot/Right/LevelUp，直接执行：材料足够即整体扣耗，无需手动填充）
    public Button upgradeButton;

    [Header("详情区")]
    //技能图标（Content/Container/IconContainer/Icon 的 Image，随选中格换图；CharacterSkillRow.icon → SkillIcon 组地址）
    public Image detailIcon;

    //技能名与等级文本（Content/Container/Name；等级段富文本色随预制体静态样例）
    public TMP_Text nameText;

    //技能描述文本（Content/Container/SkillContext/Container/View/Text，滚动区内容）
    public TMP_Text descText;
}
