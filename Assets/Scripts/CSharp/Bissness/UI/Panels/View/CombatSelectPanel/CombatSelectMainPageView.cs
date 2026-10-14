using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出击面板主页面视图（MVC 的 V）：持有人物展示区与各副本入口按钮的引用。
/// 挂在 Pages/MainPage 节点上。
/// </summary>
public class CombatSelectMainPageView : MonoBehaviour
{
    [Header("角色展示区")]
    // 角色模型挂载锚点（CharacterDisplayArea/Pos）
    public Transform characterPos;
    // 角色展示区点击交互按钮（用于转发点击以触发 Reaction 动画）
    public Button characterClickButton;

    [Header("主要关卡入口（Top）")]
    // 主关卡入口按钮（Select/Top/MainLevels/Btn）
    public Button mainLevelsButton;

    [Header("挑战与特殊副本（BotLeft）")]
    // 挑战本按钮（Select/Bot/BotLeft/Content/Challenge/Mask/Button）
    public Button challengeButton;
    // 潮汐/特殊副本按钮（Select/Bot/BotLeft/Content/Tide/Mask/Button）
    public Button tideButton;
    // 更多副本按钮（Select/Bot/BotLeft/Content/More/Mask/Button）
    public Button moreLeftButton;

    [Header("材料副本（BotRight）")]
    // 经验材料副本按钮（Select/Bot/BotRight/Content/ExpMaterialDungeon/Mask/Button）
    public Button expDungeonButton;
    // 小怪材料副本按钮（Select/Bot/BotRight/Content/NormalMaterialDungeon/Mask/Button）
    public Button normalDungeonButton;
    // BOSS材料副本按钮（Select/Bot/BotRight/Content/BossMaterialDungeon/Mask/Button）
    public Button bossDungeonButton;
    // 周本材料副本按钮（Select/Bot/BotRight/Content/WeeklyMaterialDungeon/Mask/Button）
    public Button weeklyDungeonButton;
    // 未开放/更多材料副本按钮（Select/Bot/BotRight/Content/Morw/Mask/Button）
    public Button morwRightButton;

    private void Awake()
    {
        EnsureBindings();
    }

    /// <summary>
    /// 自动绑定子节点引用，在未在 Inspector 中显式配置时作为安全兜底。
    /// </summary>
    public void EnsureBindings()
    {
        if (characterPos == null)
        {
            characterPos = transform.Find("CharacterDisplayArea/Pos");
        }

        if (characterClickButton == null)
        {
            characterClickButton = transform.Find("CharacterDisplayArea")?.GetComponent<Button>();
        }

        if (mainLevelsButton == null)
        {
            mainLevelsButton = transform.Find("Select/Top/MainLevels/Btn")?.GetComponent<Button>();
        }

        // BotLeft 挑战系列
        if (challengeButton == null)
        {
            challengeButton = transform.Find("Select/Bot/BotLeft/Content/Challenge/Mask/Button")?.GetComponent<Button>();
        }
        if (tideButton == null)
        {
            tideButton = transform.Find("Select/Bot/BotLeft/Content/Tide/Mask/Button")?.GetComponent<Button>();
        }
        if (moreLeftButton == null)
        {
            moreLeftButton = transform.Find("Select/Bot/BotLeft/Content/More/Mask/Button")?.GetComponent<Button>();
        }

        // BotRight 材料系列
        if (expDungeonButton == null)
        {
            expDungeonButton = transform.Find("Select/Bot/BotRight/Content/ExpMaterialDungeon/Mask/Button")?.GetComponent<Button>();
        }
        if (normalDungeonButton == null)
        {
            normalDungeonButton = transform.Find("Select/Bot/BotRight/Content/NormalMaterialDungeon/Mask/Button")?.GetComponent<Button>();
        }
        if (bossDungeonButton == null)
        {
            bossDungeonButton = transform.Find("Select/Bot/BotRight/Content/BossMaterialDungeon/Mask/Button")?.GetComponent<Button>();
        }
        if (weeklyDungeonButton == null)
        {
            weeklyDungeonButton = transform.Find("Select/Bot/BotRight/Content/WeeklyMaterialDungeon/Mask/Button")?.GetComponent<Button>();
        }
        if (morwRightButton == null)
        {
            morwRightButton = transform.Find("Select/Bot/BotRight/Content/Morw/Mask/Button")?.GetComponent<Button>();
        }
    }
}
