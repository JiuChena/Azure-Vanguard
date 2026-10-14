---
tags: [project, debugging, objectpool, ui]
created: 2026-10-14
updated: 2026-10-14
---

# 池化与 UI 踩坑

> 池化四铁律见 [[03-框架/对象池-ObjectsPool]]，面板 MVC 惯例见 [[04-模块/UI-面板总览]]、[[06-模板/模板-UI-MVC三件套]]。本篇只记事故。

## 108 倍放大（ObjectsPool 取出路径）

**现象**：养成面板左列角色 Item 被放大 108 倍（预制体出厂干净，全文件无异常 scale 值）。

**根因**：`ObjectsPool.Get` 池空时 `Instantiate(模板)` 在**无父状态**克隆——Unity 把源对象的**局部**变换值落成实例的**世界**变换；随后 `SetParent(parent, true)` 保世界尺度挂回，局部尺度 = 模板局部 / 挂点链 lossyScale。养成面板挂点链 lossyScale ≈ 1/108，新实例正好放大 108 倍。池里有旧实例的复用路径无此问题（世界尺度往返抵消），**只有"池空首取"的新实例中招**。

**修复（用户拍板）**：业务取用点在 `ObjectsPool.Get` 之后 `transform.localScale = Vector3.one`（+位置归零）。GridLayout 不管理 scale，位置自校正，尺度必须显式归一。治本方案（框架 `SetParent(parent, false)`）影响 VFX 等全部消费方，动框架另议（挂账 [[05-玩法设计/设计挂账]]）。

## 幽灵态（"排序切换旧 Item 未清"根因）

旧 `CharacterListPanel.RefreshList` 用 `Destroy` 清旧 Item——Destroy 是**帧末延迟**，同帧再 `Get` 新 Item 出现**新旧并存的幽灵态**。修复：旧 Item 全部 `ObjectsPool.Put`（**立即移出内容区**）→ 按新排序 `Get` 重新 FillCell。

同时：点击监听 `RemoveAllListeners` 后重绑（池化单元格复用，旧闭包 unitId 必须清）——回池立即移出 + 重绑清闭包，正是四铁律的前两条。

## 串图竞态（异步图标落在复用单元格上）

单元格是池化复用的，异步图标回包时格子可能已改绑别的数据。三层防护（先例齐全）：

1. **单元格自带绑定数据**：CHListItem 运行时绑定 `unitId`、CostItemView 绑定 `itemId`，回包校验未改绑才写 sprite
2. **切角/切页弃写**：await 回来时令牌/选择已变直接弃写，且**绝不二次入池**——新刷新已接管容器，二次 Put 会把正在用的格子收走
3. **图集一次常驻**：高频图（角色标签 Tag 图）`AcquirePersistentAssetAsync<SpriteAtlas>` 常驻 + 任务去重，图集就位后循环内无 await，天然无竞态窗口（先例 RefreshTagsAsync）

## Canvas renderMode 解码异常（未解之谜，已绕行）

**现象**：Canvas.prefab YAML 明确 `m_RenderMode: 1`（ScreenSpaceCamera），但运行时/CLI 新进程 `AssetDatabase` 直读解码为 **Overlay**，PanelManager 判定分支恒走"非 SSC"不挂相机。

**已证伪**：枚举编码（roundtrip 实证 SSC 序列化=1 与文件一致）、Addressables 旧资产（溯源 servedGuid=目标预制体）、Play Mode 打包通道（磁盘无 bundle/catalog）、运行时改动（全项目 grep 无 renderMode 写入）。原因未解。

**结案口径（用户改定）**：PanelManager 不再判定模式，**实例化后无条件 `worldCamera = CameraManager.Get(UI)`**。Overlay 渲染路径不使用 worldCamera，强挂零副作用；若将来真落在 SSC 上，相机已在位，两头通吃。

**教训：勿信预制体序列化值承载行为语义**——代码侧按需强设（相机/模式代码强设，勿信序列化值）。

## 下拉模板项禁接音效（同帧双响 +6dB）

批接线扫描含未激活节点，把 SortType（TMP_Dropdown）**模板里的 Item Toggle** 也接了 helper。选中选项瞬间：模板 `Toggle.onValueChanged` 与 Dropdown 自身 `onValueChanged` **同帧各响一次**，同相叠加 ≈ +6dB，主观"明显大一声"。

**修复**：模板项调用清空（SerializedObject `onValueChanged.m_PersistentCalls.m_Calls` arraySize=0）+ 无引用 helper 回收；保留 Dropdown 节点调用（值变化响一次，语义正确）。**下拉模板项接线此后为禁区**。

## 陈旧预制体缓存（改脚本默认值 ≠ 改资产）

改脚本字段默认值后，**磁盘未变的预制体内存里永远留旧默认**——资产不重导，序列化值不会跟脚本默认走。修复：`AssetDatabase.ImportAsset(path, ForceUpdate)` 重导入或重启编辑器。凡"我明明改了默认值为什么还是旧值"，先查预制体磁盘上的序列化值。

## 进度条驱动先查子图锚点

`Fill.anchorMax.x` 驱动 ≠ 视觉生效：手搭进度条常见结构是**容器拉伸 + 子图固定宽**，改容器 anchor 不动子图。接代码前先倒结构：确认 Fill 是哪种驱动方式（锚点拉伸 or 固定宽 + 代码改尺寸）再接。

## 关联

- [[03-框架/对象池-ObjectsPool]] — 四铁律原文
- [[04-模块/UI-面板总览]] / [[04-模块/面板-CultivatePanel]] — 事故面板现状
- [[08-踩坑与排查/UnityCLI工作流]] — 排查时的 CLI 手段
