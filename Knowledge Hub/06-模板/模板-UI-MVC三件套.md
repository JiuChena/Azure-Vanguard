---
tags: [project, template, ui, mvc]
created: 2026-10-14
updated: 2026-10-14
---

# 模板-UI-MVC三件套

> 新 UI 面板的代码骨架。流程见 [[02-流程SOP/SOP-新加UI面板]]；先例实现：`Bissness/UI/Panels/` 下 CharacterListPanel（简）/ CultivatePanel（繁）/ HomePanel（中）。
> 命名空间惯例：Bissness 层类型全局；Core 层类型挂 `Framework.*`（引用先 grep `^namespace`）。

## View（纯引用分区）

```csharp
// View/MyPanel/MyPanel_Status.cs —— 挂在对应分区根节点
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>状态栏分区引用。只存引用与写入，不含逻辑。</summary>
public class MyPanel_Status : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private Button moneyAddButton;
    [SerializeField] private Button settingsButton;

    public TMP_Text MoneyText => moneyText;
    public TMP_Text StaminaText => staminaText;
    public Button MoneyAddButton => moneyAddButton;
    public Button SettingsButton => settingsButton;

    /// <summary>只做文本/显隐写入。</summary>
    public void SetMoney(long value) => moneyText.SetText("{0}", value);
}
```

要点：字段 private `[SerializeField]` + 只读属性暴露；一个分区一个 View；Set 方法只写文本/显隐/填充。

## Model（展示数据模型）

```csharp
// Module/MyPanelModel.cs
/// <summary>展示数据。Control 的唯一数据来源；数据源接入后只改这里。</summary>
public class MyPanelModel
{
    public long Money { get; set; }          // TODO: InventoryManager.GetCount(CurrencyIds.Gold)
    public int Stamina { get; set; }         // TODO: 体力域接入（占位 0）

    public string LevelText => $"Lv.{Level}";
    public int Level { get; set; } = 1;

    // 计算属性放这里，Control 不做算术
    public float ExpProgress => MaxExp <= 0 ? 0f : (float)Exp / MaxExp;
    public int Exp { get; set; }
    public int MaxExp { get; set; } = 1;
}
```

## Control（PanelBase 子类）

```csharp
// Control/MyPanel.cs
using System;
using Framework.Gameplay.Inventory;   // Core 层类型：先 grep ^namespace 确认
using UnityEngine;

/// <summary>XXX 面板。MVC 惯例见知识库 06-模板。</summary>
public class MyPanel : PanelBase
{
    [SerializeField] private MyPanel_Status status;   // View 分区引用
    private MyPanelModel model = new MyPanelModel();

    #region 生命周期
    protected override void ComponentInit()
    {
        base.ComponentInit();
        // 缓存组件、icon 图集等
    }

    protected override void EventInit()
    {
        base.EventInit();
        status.MoneyAddButton.onClick.AddListener(OnMoneyAddClick);
        EventCenter.Instance.AddEventListener<long>(EventNames.BagUpdated, RefreshStatus);
        // ⚠️ 必须在 OnDestroy 反注册（PanelBase 无 onClose 钩子）
    }
    #endregion

    #region 数据绑定（Bind 双路径）
    /// <summary>OpenPanel 回调或已开面板补挂都走这里。</summary>
    public void Bind(string unitId)
    {
        // Start 前仅记录参数，InitAsync 再取数
    }

    private async void InitAsync()
    {
        // await 服务层数据 → 填 model → 刷新 View
        // 异步回包竞态：回包时校验绑定 id 未改绑再写 UI
    }
    #endregion

    #region 刷新
    private void RefreshStatus(long _)
    {
        model.Money = InventoryManager.Instance.GetCount(CurrencyIds.Gold);
        status.SetMoney(model.Money);
        // 文本一律英文（字体缺中文字形）；配置表/SO 来源显示名除外
    }
    #endregion

    private void OnMoneyAddClick()
    {
        // 调试期添加模式 / 正式走服务端请求
    }

    protected override void OnDestroy()
    {
        EventCenter.Instance.RemoveEventListener<long>(EventNames.BagUpdated, RefreshStatus);
        status.MoneyAddButton.onClick.RemoveListener(OnMoneyAddClick);
        base.OnDestroy();   // base 释放 ResourceScope
    }
}
```

## 列表/格子（池化四铁律）

```csharp
// 池化单元格：必须自带绑定数据字段
public class MyListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    public string unitId { get; private set; }   // 竞态校验用

    public void FillCell(string id, string name)
    {
        unitId = id;
        nameText.text = name;
    }
}

// Control 侧刷新（排序/数据变更时）：
private void RefreshList()
{
    // 1. 旧 Item 全部回池（立即移出内容区，杜绝 Destroy 帧末延迟幽灵态）
    foreach (Transform child in content) { ObjectsPool.Instance.Put(child.gameObject); }
    // 2. 按新数据逐个 Get + FillCell
    foreach (var row in sortedRows)
    {
        var go = ObjectsPool.Instance.Get(cellTemplate, content);
        go.transform.localScale = Vector3.one;           // 铁律 2：强制归一
        var cell = go.GetComponent<MyListItem>();
        cell.FillCell(row.Id, row.Name);
        // 3. 点击监听 RemoveAllListeners 后重绑（旧闭包必须清）
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OpenDetail(cell.unitId));  // 用 cell.unitId，不用闭包 row
    }
}
```

## 代码检查单（写完逐条过）

- [ ] **using 按命名空间全量过**：每个用到的类型列 namespace（UnityEngine.UI / TMPro / UnityEngine.U2D / UnityEditor.AddressableAssets / Framework.Gameplay.*）；Core 层类型先 grep `^namespace`
- [ ] View 零逻辑；Control 不做算术（计算属性在 Model）
- [ ] **文本一律英文**（代码流向 .text 的字符串）；配置表/SO 显示名除外
- [ ] 事件注册/反注册成对（EventInit ↔ OnDestroy；组件 OnEnable ↔ OnDisable）
- [ ] 池化四铁律：回池移出内容区 / Get 后 scale 归一 / RemoveAllListeners 重绑 / 单元格自带绑定 id + 竞态校验
- [ ] Bind 双路径（OpenPanel 回调 + GetPanel 探测补挂）
- [ ] 图标走 IconLibrary，取不到保留默认图；异步回包校验 cell.unitId 未改绑
- [ ] ResourceScope 传面板自己的 scope；不传时 Lease 手动 Dispose
- [ ] 热路径零分配（OnUpdate 不 new / 不 GetComponent / 不 Find；文本刷新做时间戳比较）
- [ ] 拖拽引用优先——接线一律 AgentScripts 幂等脚本，禁按名 Find
- [ ] 下拉/列表模板项**不接音效**（模板 Toggle 与 Dropdown 自身事件同帧双响叠加变响；只接 Dropdown 节点）
