using UnityEngine;

/// <summary>
/// 战斗出击面板根视图（MVC 的 V）：持有 GearBar、MainPage 与 MainStagePage 的分区引用。
/// 挂在 CombatSelectPanel 预制体根节点上。
/// </summary>
public class CombatSelectPanelView : MonoBehaviour
{
    [Header("顶部状态栏")]
    // 顶部状态栏（GearBar 节点）
    public GearBar gearBar;

    [Header("页面分区")]
    // 主页面（Pages/MainPage）
    public CombatSelectMainPageView mainPage;
    // 主关卡页面（Pages/MainStagePage）
    public CombatSelectMainStagePageView mainStagePage;

    private void Awake()
    {
        EnsureBindings();
    }

    /// <summary>
    /// 自动绑定子节点引用，在未在 Inspector 中显式配置时作为安全兜底。
    /// </summary>
    public void EnsureBindings()
    {
        if (gearBar == null)
        {
            gearBar = GetComponentInChildren<GearBar>(true);
        }

        if (mainPage == null)
        {
            mainPage = GetComponentInChildren<CombatSelectMainPageView>(true);
        }

        if (mainStagePage == null)
        {
            mainStagePage = GetComponentInChildren<CombatSelectMainStagePageView>(true);
        }
    }
}
