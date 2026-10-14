using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 养成面板属性页视图分区（MVC 的 V）：纯引用持有，不含逻辑。
/// 挂在 Pages/StatPage 上；技能书喂养格与按钮语义由 Control 绑定。
/// </summary>
public class CultivateStatPageView : MonoBehaviour
{
    [Header("基础信息")]
    //角色名（Left/Base/CHName）
    public TMP_Text chNameText;
    //星级（Left/Base/Stars 下 5 颗，按稀有度显隐；素材未配前仅占位）
    public Image[] stars;
    //等级文本（Left/Base/Exp/LvFromTo，模板样例 "Lv.80 > Lv.90"）：Control 写 "当前 > 目标"，无暂存目标为 "-"
    public TMP_Text levelText;
    //经验进度条（Left/Base/Exp/Progress/BG/Fill，左锚拉伸条，Control 按 anchorMax.x 驱动）
    public RectTransform expFillRect;
    //职业文案（Left/Archetype/Text）
    public TMP_Text archetypeText;
    //职业定位图标（Left/Archetype/Icon）
    public Image archetypeIcon;

    [Header("角色标签")]
    //标签列表容器（Left/Scroll View/Content，GridLayout；CHTag 经 ObjectsPool 出池入池）
    public RectTransform tagContent;

    [Header("属性面板")]
    //属性数值文本（Right/Stat/Content 下 7 格，顺序：生命/攻击/防御/充能/暴击率/暴击伤害/伤害加成；名称为静态）
    public TMP_Text[] statNumbers;

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
    //确认升级按钮（Bot/Right/LevelUp）：结算暂存的技能书
    public Button levelUpButton;
    //确认升级按钮文案（Bot/Right/LevelUp/Text (TMP)）：暂存 > 0 时显示 "Lv a-b" 预览，空暂存还原模板
    public TMP_Text levelUpButtonText;
    //突破执行按钮（Bot/Right/Breakthrough）
    public Button breakthroughButton;
}
