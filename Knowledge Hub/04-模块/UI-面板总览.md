---
tags: [project, ui, panels]
created: 2026-10-14
updated: 2026-10-14
---

# UI 面板总览

代码根：`Assets/Scripts/CSharp/Bissness/UI/`（Control / View / Module / Item / UIPath）。UI 框架机制见 [[03-框架/UI框架-UISystem]]；新面板流程见 [[02-流程SOP/SOP-新加UI面板]]；模板见 [[06-模板/模板-UI-MVC三件套]]。

## 面板清单（2026-10-14）

| 面板 | Control（`Panels/Control/`） | UILayer | 打开方式 | 详文 |
|---|---|---|---|---|
| PreloadPanel（启动加载） | PreloadPanel.cs | Top | `Preloader.Start` 首开 | 本文 §启动流程 |
| HomePanel（主城） | HomePanel.cs | —（prefab 加载） | 场景激活后打开 | [[04-模块/面板-HomePanel]] |
| CombatSelectPanel（出战选择） | CombatSelectPanel.cs | — | 菜单栏 combat 入口 | 本文 §CombatSelectPanel |
| CombatReadinessPanel（战备） | Control 根挂（场景常驻 Preload.unity） | — | 尚未走 PanelManager 流程 | 本文 §CombatReadinessPanel |
| CharacterListPanel（角色列表） | CharacterListPanel.cs | Mid | 菜单栏 cultivated 入口 | 本文 §CharacterListPanel |
| CultivatePanel（角色养成） | CultivatePanel.cs | Top | 列表项点击 | [[04-模块/面板-CultivatePanel]] |
| NoticePanel（提示弹窗） | NoticePanel.cs | — | 代码请求（如 TrinketManager 拒绝提示） | 本文 §NoticePanel |
| FormationPanel（编队） | FormationPanel.cs | — | 未接入 | — |

View 分区脚本在 `Panels/View/<面板名>/`，复用组件在 `Panels/View/Generic/`（GearBar）。通用小组件在 `UI/Item/`（ToggleHelper/ToggleMarkList/SwitchScene/Tag）。

## MVC 三层惯例

| 层 | 位置 | 职责 |
|----|------|------|
| View | `View/<面板名>/` | 纯组件持有者，public 字段挂 UI 引用，**不含逻辑** |
| Control | `Control/<面板名>.cs` | 继承 `PanelBase`，声明 View 字段，数据只从 Model 读 |
| Module | `Module/<面板名>Model.cs` | 展示数据模型；Control 的唯一数据来源，数据源接入后只改这里 |

**架构约定修订（2026-09-28）**：可复用组件（GearBar 类）不受"View 只存引用不含逻辑"约束——更新逻辑写在组件自己的 View 脚本；一次性面板分区 View 仍守旧规。

完整代码骨架与检查单 → [[06-模板/模板-UI-MVC三件套]]。

## 面板共性运作流程（各面板同款，写新面板照抄）

1. **初始化顺序**：`ComponentInit`（缓存组件）→ `EventInit`（注册事件+按钮）→ `Bind`（接数据）
2. **事件成对**：`EventInit` 注册 ↔ `OnDestroy` 反注册（PanelBase 无 onClose 钩子，用 OnDestroy + base 释放 ResourceScope）
3. **池化刷新**：列表项不 Destroy——旧 Item 全部回池（立即移出内容区）→ 新排序逐个 `Get`+FillCell；Get 后强制 `localScale=one`；监听 `RemoveAllListeners` 后重绑（见 [[03-框架/对象池-ObjectsPool]] 四铁律）
4. **Bind 双路径**：`OpenPanel` 回调 + 已开时 `GetPanel` 探测直呼补挂（OpenPanel 重名去重会吞回调）
5. **异步回包竞态**：回包时校验单元格绑定 id 未改绑（池化复用防串数据）
6. **图标加载**：统一 IconLibrary（[[03-框架/资源加载-AddressableManager]]），取不到保留场景默认图
7. **文本英文**：代码流向 .text 的字符串一律英文（字体缺中文字形）；配置表/SO 来源的显示名除外
8. **接线**：预制体引用不手拖，走 AgentScripts 幂等脚本 SerializedObject 绑定 + 盘上回读断言（[[06-模板/模板-AgentScripts]]）

## Preload 启动流程

`Preloader.Start` → `PanelManager.OpenPanel<PreloadPanel>`（Top 层）→ **开场动画（Animator 默认状态，驱动溶解材质）播完后** → `LoadSceneManager.LoadSceneAsync(Home)` → 场景激活后打开 HomePanel。

- 加载开始时机由 `PreloadPanel.AddDisplayFinishedListener` 控制：面板在 `OnUpdate` 检测 Animator 默认状态（离开该状态或 normalizedTime ≥ 1 即完成），无 Animator 时兜底立即放行
- 加载进度经 `EventNames.LoadSceneProgress` 广播，PreloadPanel 刷新进度文本；进度满 → `ClosePanel`（退出动画 + `CloseDelay` 延迟销毁）
- 注意 `PanelManager.OpenPanel` 的 callback 是 `DisplayPanel()` 后同帧触发的，**不能**当"动画播完"用
- 新账号初始化：`Preloader.Start` 在 `MessageTool.Init()` 后、任何 UI 读档前调用 `PlayerDataCenter.EnsureNewAccountDefaults()`（[[04-模块/玩家数据中心]]）

## GearBar（自管理复用组件，2026-09-28 重构定稿）

代码位置：`Panels/View/Generic/GearBar.cs`（原 `GearBarInit` 扩展类已删除）。挂上即工作，宿主面板零接线：

- Awake 绑默认按钮（设置/回主页 TODO log 占位；**信用点/星光石加号 = 调试期添加模式** `InventoryManager.AddItem(Gold/StarlightStone, 数量)`，单次数量 Inspector 字段 `moneyAddAmount=10000`/`starLightStoneAddAmount=1000` 可配；体力加号 TODO log，体力域暂缓；BindDefault 空引用防御）
- OnEnable 注册 `BagUpdated` 并首刷货币文本（体力 0 占位）、OnDisable 反注册
- `RefreshCurrencyTexts()` / `RegisterGoBack(action, hint)` 为实例方法；**RegisterGoBack 已收窄单参数**（2026-10-14）
- CombatReadinessPanel 只剩 `RegisterGoBack` 一行接线
- **HomePanel_Status 仍是独立分区未并入 GearBar（归并待拍板）**，但主城添加模式已于 2026-10-06 在 Control 接线（字段名注意：主城 View 是小写 `starlightStoneAddButton`，GearBar 是大写 L）

## CombatReadinessPanel（战备界面，2026-09-26）

MVC 同惯例；场景常驻于 Preload.unity（尚未走 PanelManager Addressables 流程）。

- 结构：根节点挂 Control（兼拖拽事件处理器）；`CHList1/CHList2` = 左右两页根（`CHInfoList` 挂页根，整页 SetActive 切换，wrap 两页互换）；页内 `CH1~CH4` 槽位 = 展示层（LayerInBGUI/LayerGeaterThanCH）+ `CHInfo` 信息栏（`CharacterInformation`，兄弟节点）
- **拖拽换位**：根节点实现 IDragHandler 三件套靠 UGUI 冒泡接收全树拖拽；用 `EventSystem.RaycastAll` + 节点**向上**找 `tag=character`（子节点不继承 tag，不能只查命中节点）；隐藏页不被射线命中 → 左换左/右换右天然成立；元组赋值互换 position
- 信息栏跟随：标签打在展示层时信息栏不随角色走 → 元组互换两个信息栏 transform.position；标签打在槽位根（信息栏在子层级）则自动跳过，两种打标方式兼容
- **索引页签**：`InitTeamToggles()` 运行时保证单选（无 ToggleGroup 则补建、`allowSwitchOff=false`），默认选中第 1 套；`SwitchTeam(index)` 记录索引，配置 JSON 接入后在此加载刷新
- TODO：编队/战斗按钮、加号获取途径、设置面板、主城跳转、返回按钮接线后的真实关闭流程

## CharacterListPanel（角色列表面板，2026-10-06）

主城菜单栏"培养"入口 → `PanelManager.OpenPanel<CharacterListPanel>(UIPath.CHARACTERLISTPANEL, UILayer.Mid)`；Addressables 组 Prefab-UI-Panels-Normal（地址 `CharacterListPanel`，预制体 `Prefabs/UI/Character/`）。养成 UI 第一层；列表项点击进 CultivatePanel（[[04-模块/面板-CultivatePanel]]）。

- **MVC**：`Control/CharacterListPanel.cs`（PanelBase）+ `View/CharacterList/CHListView.cs`（挂 CHList 页根：scrollContent + sortType）+ `View/CharacterList/CHListItem.cs`（列表项卡片纯引用：rarityBg/iconImg/nameText/levelText）；列表项预制体 `CHListItem.prefab`（215×245，按 Content 格子尺寸生成，字体取面板 Title）
- **数据链**：`CharacterWarehouseManager.Snapshot()` × `CharacterTable`（DataTableManager 直载，表系统首个 UI 消费者）→ 名字/`Lv.{level}`/稀有度底图（rarityBgs 下标=稀有度-1，与 HomePanel 名片同口径）。角色图标 = 小头像 `IconLibrary.GetCharacterAvatarAsync(VariantSmall, unitId)`
- **行为**：排序下拉框（Default/Rarity/Level，选项运行时填充，变更重刷）；仓库事件（CharacterAcquired/CharacterDataUpdated）→ 列表自动跟随，OnDestroy 反注册；GearBar `RegisterGoBack` → ClosePanel
- **池化刷新**：见"共性运作流程"第 3 条（本面板是先例出处）
- **点击入口**：列表项 → `OpenCultivate(unitId)`——先 `GetPanel<CultivatePanel>` 探测（已开直接 `Bind(unitId)` 补挂），未开则 `OpenPanel<CultivatePanel>(UIPath.CULTIVATEPANEL, UILayer.Top, p => p.Bind(unitId))`
- **接线**：`AgentScripts/SetupCharacterListPanel.cs`（幂等）——生成 cell 预制体 + LoadPrefabContents 挂 CHListView/CharacterListPanel 并回写 gearBar/chListView/itemPrefab/avatarAtlas（Control 私有字段走 SerializedObject）

## CombatSelectPanel（出战选择，2026-10-13 前后落地）

`Control/CombatSelectPanel.cs` + View 五件（`View/CombatSelectPanel/`：CombatSelectPanelView / CombatSelectMainPageView / CombatSelectMainStagePageView / CombatSelectLevelItemView / CombatSelectCharacterAgent）。接线脚本 `AgentScripts/SetupCombatSelectPanel.cs`。关卡/出战玩法层未建，面板结构先行。

## NoticePanel（提示弹窗）

`Control/NoticePanel.cs` + `View/Notice/NoticePanelView.cs`。系统级提示通道：服务层拒绝类提示走 `NoticeRequested`（TrinketManager "Already worn" 先例），面板负责展示。

## 旧版式经验（HomePanel_Temp 迁移时仍适用）

- 页签切换：`Panels` 直接子节点才是可切换面板（子节点名 = ToggleHelper 的 `eventParameter`）；不能用 `GetComponentsInChildren<GameObject>`（GameObject 不是 Component，直接抛 ArgumentException）
- 初始 isOn 的页签不会触发 ToggleHelper 状态变化事件，需手动同步一次
- 旧版式根节点 `HomePanel_Temp` 暂留参考，删除时机由用户定

## 关联

- [[04-模块/面板-CultivatePanel]] / [[04-模块/面板-HomePanel]] —— 两复杂面板专篇
- [[08-踩坑与排查/池化与UI]] —— 池化/缩放/Canvas 坑汇总
