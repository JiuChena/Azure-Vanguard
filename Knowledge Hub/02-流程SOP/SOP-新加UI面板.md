---
tags: [project, sop, ui, panel]
created: 2026-10-14
updated: 2026-10-14
---

# SOP-新加UI面板

> 从零接入一个新 UI 面板的全流程。**Agent 可按本 SOP 递归作业：预制体结构自己探查、View/Control 自己写、接线自己脚本化**——只有 📐/🖐 标注项等用户。
> 机制背景：[[03-框架/UI框架-UISystem]]；现有面板清单：[[04-模块/UI-面板总览]]；代码模板：[[06-模板/模板-UI-MVC三件套]]；接线脚本模板：[[06-模板/模板-AgentScripts]]。
> 样板先例：CharacterListPanel（最完整的"从预制体到面板"案例）、CultivatePanel（最复杂，七页）。

## 前置知识（读这三篇再动工）

1. [[06-模板/模板-UI-MVC三件套]] —— View/Model/Control 代码骨架与检查单
2. [[04-模块/UI-面板总览]] §面板共性运作流程 —— 八条共性（事件成对/池化四铁律/Bind 双路径/竞态 guard…）
3. [[06-模板/模板-AgentScripts]] —— 接线脚本怎么写

## 流程总览

| # | 步骤 | 分工 | 产出 |
|---|---|---|---|
| 1 | 前置拍板 | 📐 | 面板定位单（层级/入口/数据源） |
| 2 | 预制体结构探查 | 🤖 | 节点路径表（交用户核对口径） |
| 3 | 写 View | 🤖 | View 分区脚本 ×N |
| 4 | 写 Model + Control | 🤖 | Model + Control（PanelBase 子类） |
| 5 | 接线脚本 | 🤖 | AgentScripts/Setup<面板名>.cs |
| 6 | 注册 | 🤖（预制体出图 🖐） | Addressables 条目 + UIPath + 入口按钮 |
| 7 | 验证 | 🤖 | 编译三重 + Play 截图 |

## 步骤 1：前置拍板（📐）

开工前向用户确认四件事，确认结果写进面板文档（04-模块/）：

| 项 | 问题 | 参考 |
|---|---|---|
| UILayer | Bot/Mid/Top/System 哪层？ | [[03-框架/UI框架-UISystem]] |
| 入口 | 谁打开它？（按钮在哪个面板/场景常驻？） | UIPath 常量表 |
| 数据源 | 读哪个管理器/表？订阅哪些事件刷新？ | [[01-项目总览/快捷查询地图]] |
| 刷新时机 | 打开时拉一次 or 事件跟随 or 每帧？ | 共性流程第 2 条 |

## 步骤 2：预制体结构探查（🤖）

**Agent 自己递归查预制体，不问用户要结构**。两种手段：

1. **直接读 .prefab YAML**（首选，零 Unity 依赖）：
   - 预制体在 `Assets/Prefabs/UI/`（或用户指定路径）
   - `grep -n "m_Name:" <prefab>.yaml` 列节点名；Transform 的 `m_Father`/`m_Children` fileID 链重建层级
   - 组件类型：`m_Script: {guid: ...}` 反查——用 `grep -rn <guid> Assets/ --include="*.meta"` 找到脚本名；UGUI 内置组件（Image/Button/TMP_Text…）按 class id 识别（114=MonoBehaviour，另见 YAML 头部 `!u!` 标记）
   - 产出**节点路径表**：`节点全路径 → 组件 → 用途推测`（如 `Root/Bot/MenuBar/students → Button + Image → 菜单入口`）
2. **UnityCLI 探针脚本**（结构复杂/YAML 拿不准时）：AgentScripts 写 `Probe<面板名>.cs`，递归 `transform` 树 dump 路径+组件清单（先例：`AgentScripts/ProbeCultivateItemPrefab.cs`、`ProbeDisplayViewWiring.cs`）

**把节点路径表交给用户核对一遍口径**（哪个节点是什么用途），再进步骤 3——这是唯一一次结构确认，之后不再反复。

## 步骤 3：写 View（🤖）

照 [[06-模板/模板-UI-MVC三件套]] View 骨架：

- 按功能分区拆 View（每区一个类），不把整个面板塞一个 View
- 字段 = `[SerializeField]` 节点引用（Text/TMP/Button/Image/Transform…），**public 只读属性暴露**
- **View 不含逻辑**；Set 方法只写文本/显隐/填充
- 挂点：View 组件挂在对应分区根节点（先例：`HomePanel_MenuBar` 挂 Bot/MenuBar 节点）
- **using 清单按命名空间全量过**（UnityEngine.UI / TMPro / UnityEngine.U2D…漏一个 CS0246 泵一轮编译）

## 步骤 4：写 Model + Control（🤖）

- **Model**（`Module/<面板名>Model.cs`）：纯展示数据（构造注入/属性），Control 的唯一数据来源；占位数据返回 0/空串并注释 TODO
- **Control**（`Control/<面板名>.cs`）：继承 `PanelBase`，声明各 View 字段 + 需要的资源引用（avatarAtlas 等）
- 初始化三件顺序：`ComponentInit` → `EventInit` → `Bind`（或 InitAsync）
- 必须落实共性八条（[[04-模块/UI-面板总览]]）：事件注册/OnDestroy 反注册成对、列表池化四铁律、Bind 双路径（OpenPanel 回调 + GetPanel 探测补挂）、异步回包竞态校验、IconLibrary 加载、文本英文、ResourceScope 注入、热路径零分配

## 步骤 5：接线脚本（🤖）

**预制体引用不手拖**，写 `AgentScripts/Setup<面板名>.cs`（幂等，先例 `SetupCharacterListPanel.cs`）：

1. LoadPrefabContents 打开预制体
2. 挂 View/Control 组件（已挂则复用）
3. `SerializedObject` 逐字段写节点引用（私有字段也可写：`FindProperty("<字段名>")`）
4. `ApplyModifiedPropertiesWithoutUndo` + `SaveAsPrefabAsset`
5. **盘上回读逐项断言**（重开预制体 → 每个字段非空 → 打印 True 清单）——"接线成功"以盘上回读为准，不是以写入成功为准

可复用组件（GearBar 类）宿主只接一行（如 `RegisterGoBack`）。

## 步骤 6：注册（🤖 + 🖐 出图）

- **Addressables**：预制体入组 `Prefab-UI-Panels-Normal`（或面板图集所在组），地址 = 面板名（`AgentScripts` 建 AddressableAssetEntry，先例 SetupConfigTablesAddressables 的组操作模式）
- **UIPath**：`Bissness/UI/UIPath/UIPath.cs` 加常量
- **入口按钮**：上游面板 `EventInit` 绑 `OpenPanel<T>(UIPath.X, layer, callback)`
- **Prewarm**（如需常驻）：Preloader 预热清单加行
- 图标/美术：**一律 🖐 用户操作**，🤖 出需求表（命名/规格/组），见 [[02-流程SOP/SOP-新加角色]] 步骤 5 模式

## 步骤 7：验证（🤖）

1. **编译三重验证**（[[01-项目总览/工具与环境]]）：dll mtime 基线 → 刷新+符号 grep → console groundTruth
2. **Play 观察**：面板打开/数据展示/关闭释放；玩家可见变化截图存 `production/qa/evidence/`
3. **面板文档**：04-模块/ 新建或更新面板篇（MVC 构成/数据链/TODO），更新 [[04-模块/UI-面板总览]] 清单 + [[01-项目总览/快捷查询地图]]
4. **日志**：按 [[_规范/日志规范]] 写批次日志 + 索引

## 常见坑（动工前扫一眼）

| 坑 | 出处 |
|---|---|
| 漏 using（UI/U2D/Addressables 父命名空间） | [[08-踩坑与排查/UnityCLI工作流]] |
| 池化 Get 后 scale 108 倍放大 | [[08-踩坑与排查/池化与UI]] |
| 串图/串数据（异步回包竞态） | 同上 |
| Canvas renderMode 序列化值不可信（代码强设） | 同上 |
| SerializedProperty arraySize 扩容复制尾元素 | [[08-踩坑与排查/序列化与存档]] |
| OpenPanel 重名吞回调 / callback 同帧触发 | [[03-框架/UI框架-UISystem]] |
| 下拉模板项接音效同帧双响 | [[08-踩坑与排查/池化与UI]] |

## 版本记录

- **v1（2026-10-14）**：首版，自 CharacterListPanel/CultivatePanel/HomePanel 接入全过程提炼。
