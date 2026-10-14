---
tags: [project, ui, homepanel]
created: 2026-10-14
updated: 2026-10-14
---

# 面板-HomePanel（主城界面）

代码位置：`Assets/Scripts/CSharp/Bissness/UI/Panels/`——`Control/HomePanel.cs`、`View/HomePanel/`（5 分区）、`Module/HomePanelModel.cs`。MVC 结构 2026-09-20 起重构；新版式在 Home.unity 的 **Canvas/HomePanel** 下（非场景根）。MVC 惯例与共性流程见 [[04-模块/UI-面板总览]]。

## View 分区（5 个）

| View | 场景节点 | 字段 |
|------|----------|------|
| `HomePanel_BG` | BG | bgVideoPlayer / bgRenderTexture / bgRawImage / bgAudioSource |
| `HomePanel_PlayerDisplay` | Top/Left | avatarImg / playerName / levelText / levelImg（经验填充）/ avatarBtn |
| `HomePanel_MenuBar` | Bot/MenuBar | students / cultivated / store / gacha / task 五入口 + timeText |
| `HomePanel_Status` | Top/Right | 体力/金钱/星光石文本 + 对应加号按钮 + settingsButton + bgExpandButton |
| `HomePanel_Gameplay` | Bot/Right | combatButton / activitiesButton |

五个 View 的字段均已接好场景引用（`HomePanel_MenuBar` 组件补挂在 Bot/MenuBar 节点）。Control 另持 `avatarAtlas` + `rarityBgs`。

## Control 已实现 / TODO

- ✅ `RefreshPlayerDisplay()`：头像（图集 GetSprite，取不到保留默认图）、昵称、`Lv.{level}`、经验条（`levelImg.rectTransform.anchorMax.x` = 进度，左侧拉伸方案；素材改 Filled 类型后换 `fillAmount`）
- ✅ 货币接真实余额（2026-09-27）：`HomePanelModel.Money/StarlightStone` 走 `InventoryManager.GetCount(CurrencyIds.*)`，Control 走 `RefreshStatus()`；加号 = 调试期添加模式（`statusMoneyAddAmount=10000`/`statusStarlightStoneAddAmount=1000`），体力仍 0 占位
- ✅ `EventInit` 注册 `BagUpdated` 刷新、OnDestroy 反注册
- ✅ `menuBar.cultivatedButton` → OpenPanel CharacterListPanel（HomePanel 为 prefab 加载非场景常驻）
- ⬜ TODO（位置都在 `EventInit` / `ComponentInit` / `OnUpdate`）：菜单栏其余入口（students/store/gacha/task）打开对应面板；状态栏加号跳转获取途径；settings/bgExpand；combat/activities；avatarBtn → 头像选择 → `PlayerInfoManager.SetAvatar`；顶部时间按秒刷新（时间戳比较，避免每帧写 text）；背景视频播放策略
- ⬜ Model 数据源：`Stamina` / `MenuTimeText` 目前占位返回 0/空串，接入后只改 `HomePanelModel`

## 待接入数据

- 角色系统落地后：名片展示列表由 `PlayerInfo` 持久化（Key 6 起追加），图标名/稀有度走角色配置表，`SetDisplayCharacter` 入参改角色 ID
- 等级曲线表接入后，在 `PlayerInfoManager.AddExp` 中结算升级后经验上限（PlayerInfo.maxExp 暂不变化）
- 体力接入 `HomePanelModel`（体力域留位见 [[04-模块/玩家数据中心]]）

## 关联

- [[04-模块/UI-面板总览]] —— 共性流程/面板清单
- [[04-模块/玩家数据中心]] —— PlayerInfo/货币数据源
