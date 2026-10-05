using Core.Gear;
using Framework.Gameplay.Inventory;
using Framework.Gameplay.Item;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 通用状态栏 View（MVC 的 V）：货币文本/加号/设置/回主页的引用集合 + 自管理更新逻辑。
/// 复用组件约定：挂上即工作——货币文本随 BagUpdated 自动刷新（体力为占位），默认按钮行为自带，宿主面板零接线；
/// 返回按钮与提示文本各面板语义不同，由面板调用 <see cref="RegisterGoBack"/> 手动注册。
/// </summary>
public class GearBar : MonoBehaviour
{
    [Header("返回")]
    //返回上一级按钮：回调由各面板通过 RegisterGoBack 手动注册
    public Button goBackButton;
    //返回上一级提示文本：随返回目标变化，在 RegisterGoBack 中一并设置
    public TMP_Text goBackHintText;

    [Header("货币")]
    //体力文本：启用与 BagUpdated 时自动刷新
    public TMP_Text staminaText;
    //信用点文本：启用与 BagUpdated 时自动刷新
    public TMP_Text moneyText;
    //星光石文本：启用与 BagUpdated 时自动刷新
    public TMP_Text starLightStoneText;

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

    private void Awake()
    {
        //TODO 获取途径面板就绪后，三个加号替换为 PanelManager.OpenPanel 跳转对应获取途径
        BindDefault(staminaAddButton, () => Debug.Log("[GearBar] 打开体力获取途径（TODO 获取途径面板未接入）"));
        BindDefault(moneyAddButton, () => Debug.Log("[GearBar] 打开信用点获取途径（TODO 获取途径面板未接入）"));
        BindDefault(starLightStoneAddButton, () => Debug.Log("[GearBar] 打开星光石获取途径（TODO 获取途径面板未接入）"));

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
    /// 手动注册返回按钮回调并设置返回提示文本；重复调用会覆盖先前注册的回调。
    /// </summary>
    /// <param name="goBackAction">返回回调，如关闭当前面板回到上级界面。</param>
    /// <param name="hintText">返回提示文本内容，与回调语义一致；传 null 不改动文本。</param>
    public void RegisterGoBack(UnityAction goBackAction, string hintText)
    {
        if (goBackButton == null) return;

        //RemoveAllListeners 只清运行时监听，Inspector 中配置的持久监听不受影响
        goBackButton.onClick.RemoveAllListeners();
        goBackButton.onClick.AddListener(goBackAction);

        if (goBackHintText != null && hintText != null) goBackHintText.text = hintText;
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
        moneyText.text = InventoryManager.Instance.GetCount(CurrencyIds.Gold).ToString();
        starLightStoneText.text = InventoryManager.Instance.GetCount(CurrencyIds.StarlightStone).ToString();
    }

    /// <summary>按钮默认行为绑定辅助：未挂引用时跳过，避免 Awake 阶段空引用。</summary>
    private void BindDefault(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
    }
}
