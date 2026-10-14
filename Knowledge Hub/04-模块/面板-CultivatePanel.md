---
tags: [project, ui, cultivatepanel]
created: 2026-10-14
updated: 2026-10-14
---

# 面板-CultivatePanel（角色养成面板）

养成 UI 第二层：`CharacterListPanel` 列表项点击 → `OpenPanel<CultivatePanel>(UIPath.CULTIVATEPANEL, UILayer.Top)`；Addressables 组 Prefab-UI-Panels-Normal（地址 `CultivatePanel`；过期条目 `CharacterCultivatePanel` 已移除）。养成服务侧见 [[04-模块/角色养成系统]]；共性流程见 [[04-模块/UI-面板总览]]。

## MVC 构成

- **Control**：`Control/CultivatePanel.cs`（PanelBase）
- **Model**：`Module/CultivatePanelModel.cs`——UnitId/Row/Entry/Stats + LevelCap/ExpToNext/ExpProgress 计算属性，Control 选中后填充
- **View**（`View/CultivatePanel/`）：`CultivatePanelView`（根汇总）/ `CultivateCharacterBarView`（content+模板）/ `CultivateCharacterItem`（toggle/icon/markList/**unitId**——池化单元格必须自带绑定数据）/ `CultivateStatPageView`（名字/星级×5/等级/经验条/职业/7 属性格/Up/Fill/InputField/Upgrade）/ `CultivateCharacterDisplayView` + `CultivateModelDisplay`（模型展示，pages 数组下标即页号）/ `CultivateWeaponPageView` / `CultivateTrinketPageView`（+ TrinketBagView/TrinketSlotView/TrinketBagItemView/TrinketFilterItemView）/ `CultivateAbilityPageView`

## 数据链

`CharacterProgressionManager.EnsureReadyAsync` → 仓库条目 × CharacterTable × `CalculateStatsAsync`。属性七格顺序=预制体（生命/攻击/防御/充能/暴击率/暴击伤害/伤害加成），Recharge/DmgBonus 未实装显示"-"；星级=稀有度显隐（素材未配占位）；经验条按 `expFillRect.anchorMax.x` 驱动（预制体无 Filled Image）。

## 关键机制

- **角色选择列表**：ObjectsPool 池化（Get 后强制 scale 归一）；Toggle 无 ToggleGroup，互斥由 Control `SetIsOnWithoutNotify` 维护（不触发 onValueChanged 无回环），取消选中强制弹回（单选语义）
- **Bind 双路径**：OpenPanel 回调（Start 前仅记录，InitAsync 按此选中）；面板已开时 `GetPanel` 探测后直呼补挂（OpenPanel 重名去重会吞回调）
- **升级操作（调试期直喂经验，正式道具链路接 InventoryManager 后替换）**：Up=`expPerClick`（Inspector 可配，对齐 GearBar 添加模式先例）/ Fill=补满本级（`ExpToNext(level)-exp`）/ Upgrade=底部 InputField 数量；全走 `AddExpAsync` 真实结算，cap 溢出丢弃有日志，展示经 CharacterDataUpdated 广播回流
- **突破无 UI 出口**（第 5 页签 Ascend 页预制体未建，Control 占位 TODO），服务侧 `BreakthroughAsync` 就绪待接
- **Gun/Trinket/Ability 三页**：页签可切、页面可激活；武器图标已接 IconLibrary（Gun 页消费 `CHWeapon_<unitId>`）；Trinket 页布局待用户出（无升级段，与 Stat/Gun 不同构）
- **角色名准绳**：SO.Name 为准（`UnitConfigLibrary` 预热 nameOverrides；加载失败/空值回退表行 Name）

## 接线（AgentScripts 幂等）

`AgentScripts/SetupCultivatePanel.cs`（挂 View+Control 回读全 True）；`WireCultivatePanelAtlas.cs`（avatarAtlas 补接）；`WireCultivateAbilityPage.cs`（Ability 页）；`WireCultivateModelChain.cs` / `SetupCh0209ModelPrefab`（模型展示链，新角色复制改路径）。

两处层级坑（以 dump 缩进为准别想当然）：经验条 Fill 实际在 `Progress/BG/Fill`（不是 Progress 直下）；`InputField (TMP)` 嵌在 `Text (TMP)` 标签节点内（"Upgrade From Lv.{0}[输入框]" 内联式样）。

## 关联

- [[04-模块/角色养成系统]] —— 服务层/公式/表
- [[04-模块/饰品系统]] —— Trinket 页数据源
- [[08-踩坑与排查/池化与UI]] —— 池化四铁律
