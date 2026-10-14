using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成面板武器页视图分区（MVC 的 V）：纯引用持有，不含逻辑。
/// 挂在 Pages/GunPage 上；技能书喂养格与按钮语义由 Control 绑定。
/// 武器与角色 1:1 绑定（不可更换），页面只做展示与喂养升级操作。
/// </summary>
public class CultivateWeaponPageView : MonoBehaviour
{
    [Header("基础信息")]
    //武器图标（Base/Container/Icon；单图按需 CHWeapon_<unitId>，Control 异步填充）
    public Image iconImg;
    //武器名（Base/Container/Name）
    public TMP_Text nameText;
    //星级（Base/Container/Stars 下 5 颗，按角色稀有度显隐，与属性页同口径）
    public Image[] stars;
    //武器等级文本（Base/Container/Level，模板样例 "Lv.80 > Lv.90"）：Control 写 "当前 > 目标"，无暂存目标为 "-"
    public TMP_Text levelText;
    //武器经验进度条（Base/Container/Progress/BG/Fill，左锚拉伸条，Control 按 anchorMax.x 驱动）
    public RectTransform progressFillRect;

    [Header("属性")]
    //基础白值属性图标（Base/BasicProperty/Top/Icon）
    public Image baseStatIcon;
    //白值数值（Base/BasicProperty/Top/Text；Top 标题为静态 "Base Stats"，Control 只写数值）
    public TMP_Text baseStatText;
    //主属性图标（Base/BasicProperty/Bot/Icon）
    public Image mainStatIcon;
    //主属性名（Base/BasicProperty/Bot/PropertyText；Control 按 MainStatType 写英文键名）
    public TMP_Text mainStatNameText;
    //主属性数值（Base/BasicProperty/Bot/Number）
    public TMP_Text mainStatValueText;

    [Header("特殊词条")]
    //特殊词条展示文本（SpecialEffect/Container/View/Text；战斗框架落地前只展示不生效）
    public TMP_Text specialEffectText;

    [Header("升级操作（技能书喂养）")]
    //升级区根节点（Bot/Left/LevelUp）：升级态显示，与突破区互斥（二选一）
    public RectTransform levelUpSection;
    //书格容器（升级区 Content）：格子由 Control 按 bookItemIds 从 costItemPrefab 动态生成（GridLayout 自动排列，固定 4 格不入池）
    public RectTransform bookContent;
    //突破区根节点（Bot/Left/Breakthrough）：等级达上限且还有突破阶段时显示，与升级区互斥
    public RectTransform breakthroughSection;
    //突破材料只读列表容器（突破区 Content；CostItem 经 ObjectsPool 出池入池，展示持有/需要）
    public RectTransform breakthroughContent;
    //自动填充按钮（Bot/Right/Fill）：按持有量预暂存到可达最高级
    public Button fillButton;
    //确认升级按钮（Bot/Right/LevelUp）：结算暂存的武器技能书
    public Button levelUpButton;
    //确认升级按钮文案（Bot/Right/LevelUp/Text (TMP)）：暂存 > 0 时显示 "Lv a-b" 预览，空暂存还原模板
    public TMP_Text levelUpButtonText;
    //突破执行按钮（Bot/Right/Breakthrough）：突破属角色维度，武器页与属性页同执行角色突破
    public Button breakthroughButton;
}
