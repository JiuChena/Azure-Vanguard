using Core.Gear;
using Framework.Gameplay.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 通用状态栏 View（MVC 的 V）：货币文本/加号/设置/回主页的引用集合 + 自管理更新逻辑。
/// 复用组件约定：挂上即工作——货币文本随 BagUpdated 自动刷新（体力为占位），默认按钮行为自带，宿主面板零接线；
/// 信用点/星光石加号为调试期添加模式（单次添加数量 Inspector 可配），体力加号与获取途径跳转仍为 TODO；
/// 返回按钮回调各面板语义不同，由面板调用 <see cref="RegisterGoBack"/> 手动注册。
/// 面板标题（PanelName）在编辑模式下定死，任何面板运行时一律不改写（2026-10-07 拍板）。
/// </summary>
public class GearBar : MonoBehaviour
{
    [Header("返回")]
    //返回上一级按钮：回调由各面板通过 RegisterGoBack 手动注册
    public Button goBackButton;
    //面板标题（节点 PanelName；历史命名 goBackHintText，两预制体已接线故字段名保留）：
    //文本由编辑模式定死，运行时一律不改写（2026-10-07 拍板）——字段仅作引用持有
    public TMP_Text goBackHintText;

    [Header("货币")]
    //体力文本：启用与 BagUpdated 时自动刷新
    public TMP_Text staminaText;
    //信用点文本：启用与 BagUpdated 时自动刷新
    public TMP_Text moneyText;
    //星光石文本：启用与 BagUpdated 时自动刷新
    public TMP_Text starLightStoneText;
    //信用点物品 id（ItemTable 主键，Inspector 可调）——货币种类以物品表为准，代码不设常量表
    public string moneyItemId = "Currency_CreditPoint";
    //星光石物品 id（ItemTable 主键，Inspector 可调）
    public string starLightStoneItemId = "Currency_StarlightStone";

    [Header("快捷按钮")]
    //体力加号按钮：Awake 注册默认行为
    public Button staminaAddButton;
    //信用点加号按钮：Awake 注册默认行为
    public Button moneyAddButton;
    //星光石加号按钮：Awake 注册默认行为
    public Button starLightStoneAddButton;
    //设置按钮：Awake 注册默认行为
    public Button settingButton;
    //回主页按钮：Awake 注册默认行为
    public Button homeButton;

    [Header("添加模式")]
    //信用点加号单次添加数量：点击一次 moneyAddButton 直接增加的背包持有量，调试期用，Inspector 可调
    public int moneyAddAmount = 10000;
    //星光石加号单次添加数量：点击一次 starLightStoneAddButton 直接增加的背包持有量，调试期用，Inspector 可调
    public int starLightStoneAddAmount = 1000;

    private void Awake()
    {
        //TODO 获取途径面板就绪后，加号统一替换为 PanelManager.OpenPanel 跳转对应获取途径（当前信用点/星光石为调试期添加模式）
        BindDefault(staminaAddButton, () => Debug.Log("[GearBar] 打开体力获取途径（TODO 体力系统与获取途径面板均未接入）"));
        //添加模式：直接加背包持有量，变更即存档并广播 BagUpdated，货币文本自动跟随
        BindDefault(moneyAddButton, () => InventoryManager.Instance.AddItem(moneyItemId, moneyAddAmount));
        BindDefault(starLightStoneAddButton, () => InventoryManager.Instance.AddItem(starLightStoneItemId, starLightStoneAddAmount));

        //TODO 设置面板就绪后替换为 PanelManager.OpenPanel 打开设置面板
        BindDefault(settingButton, () => Debug.Log("[GearBar] 打开设置面板（TODO 设置面板未接入）"));

        //TODO 面板纳入 PanelManager 管理后，改为关闭当前面板并 OpenPanel(UIPath.HOMEPANEL) 返回主城
        BindDefault(homeButton, () => Debug.Log("[GearBar] 返回主城（TODO 主城跳转未接入）"));
    }

    private void OnEnable()
    {
        //背包变更 → 货币文本自动跟随；启用即先刷一次首屏
        EventCenter.Instance.Register(EventNames.BagUpdated, RefreshCurrencyTexts);
        RefreshCurrencyTexts();
    }

    private void OnDisable()
    {
        EventCenter.Instance.Unregister(EventNames.BagUpdated, RefreshCurrencyTexts);
    }

    /// <summary>
    /// 手动注册返回按钮回调；重复调用会覆盖先前注册的回调。
    /// 面板标题（goBackHintText → PanelName）不在此设置——编辑模式下定死，运行时不改写。
    /// </summary>
    /// <param name="goBackAction">返回回调，如关闭当前面板回到上级界面。</param>
    public void RegisterGoBack(UnityAction goBackAction)
    {
        if (goBackButton == null) return;

        //RemoveAllListeners 只清运行时监听，Inspector 中配置的持久监听不受影响
        goBackButton.onClick.RemoveAllListeners();
        goBackButton.onClick.AddListener(goBackAction);
    }

    /// <summary>
    /// 刷新三栏货币文本：体力为玩家字段（TODO 体力系统），金钱/星光石实时取背包持有量。
    /// 由 OnEnable 与 BagUpdated 自动调用；宿主面板一般无需手动调用。
    /// </summary>
    public void RefreshCurrencyTexts()
    {
        if (staminaText == null || moneyText == null || starLightStoneText == null) return;

        //TODO 体力：接入体力系统后改为真实值（含上限与恢复倒计时）
        staminaText.text = "0";
        //金钱/星光石：货币是物品，余额 = 背包持有量
        moneyText.text = InventoryManager.Instance.GetCount(moneyItemId).ToString();
        starLightStoneText.text = InventoryManager.Instance.GetCount(starLightStoneItemId).ToString();
    }

    /// <summary>按钮默认行为绑定辅助：未挂引用时跳过，避免 Awake 阶段空引用。</summary>
    private void BindDefault(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
    }
}
