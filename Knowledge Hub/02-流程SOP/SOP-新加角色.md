---
tags: [project, sop, onboarding, character]
created: 2026-10-08
updated: 2026-10-14
---

# SOP-新加角色

> **活动文档**：每接入一个新角色按此流程走；流程随系统叠加持续修订（版本记录在文末）。
> 分工三类：**🤖 Claude 自动化**（可直接做）/ **🖐 用户手调**（必须亲自操作）/ **📐 设计拍板**（用户给方案后才能做）。
> 技术背景见 [[04-模块/角色养成系统]] 与 [[04-模块/GAS-能力与状态机]]，设计语境见 [[05-玩法设计/玩法规则]]，首个样板 = CH0209（2026-10-08 接入）。

## 身份对齐铁律

unitId 一处定、六方对齐：

**unitId = SO 资产名 = CharacterTable 主键 = 头像 sprite 名 = 仓库键 = Addressables 地址（SO 组与模型组同址不同型）**

派生 id 编码（角色数字码 = unitId 后三位数字，如 CH0209 → 209）：

| 层 | 编码 | 例（CH0209） |
|---|---|---|
| skillId | 数字码×100 + 槽位（1 普攻 / 2 E / 3 Q） | 20901 / 20902 / 20903 |
| effectId | skillId×100 + 序号 | 2090101、2090201… |
| nodeId | 数字段×10 + 序号（2091x 天赋 / 2092x 加成） | 20911、20921… |

## 流程总览

| # | 步骤 | 分工 | 依赖 |
|---|---|---|---|
| 1 | 四表数据 + 导出 | 🤖（数值/技能设计需 📐） | 技能、数值方案 |
| 2 | UnitConfig SO + Addressables 登记 | 🤖 | 步骤 1 |
| 3 | 默认持有角色列表 | 🤖（是否默认持有 📐） | 步骤 1 |
| 4 | 模型预制体接线 + 登记 | 🤖（预制体/TimelineClips 🖐） | 用户备好预制体 |
| 5 | 头像/图标 | 🖐（🤖 只列需求表） | 步骤 1 |
| 6 | 端到端验证 | 🤖 | 全部完成 |

## 步骤 1：数据表（🤖）

- 源表：`Assets/Module/Unit/Character/Excel/*.xlsx`（**唯一真源**，手改 xlsx，不直接改 json/bytes）。
- 插行工具：`dotnet run --project Tools/TableRowInserter -- <xlsx路径> <rows.json>`（按表头字段名/类型行写单元格，主键幂等跳过）。
- 重导出：`dotnet run --project Tools/ExcelTableExporter`（生成 `ConfigTables/*.bytes + *.json`）。
- 每个新角色四表各加：
  - **CharacterTable** 1 行（id/name/rarity/profession/camp/cost/hp/atk/def/atkInterval/range/moveSpeed/normalAtkId/skillEId/skillQId/talentIds/bonusIds）
  - **SkillTable** 3 行（普攻/E/Q：cd、能量消耗、效果 id 列表）
  - **SkillEffectTable** 每技能 1~2 行（effectType/damageType/range/ratio/growth/duration 等）
  - **UnlockNodeTable** 2 天赋 + 4 加成（type/effectKey/effectValue/cost）
- **📐 接入前需用户给**：定位（profession/camp）、稀有度、费用、基础数值、攻速/射程/移速、技能设计（名称/CD/消耗/效果类型与数值）。
- **临时政策**：加成节点 cost 必须非空（独立解锁口径）——绕开共享突破表缺陷（见「设计挂账」）；天赋节点可空 cost（随突破解锁）。

## 步骤 2：UnitConfig SO + Addressables（🤖）

- `AgentScripts/SetupUnitConfigsAddressables.cs`：NameMap 追加 `(assetName, 显示名)` 一行 → 跑脚本：
  - 创建/复用 `Assets/Configurations/Unit/Character/<unitId>.asset`，写 unitId/unitName；
  - 接 6 张表引用（CharacterTable/LevelTable/SkillTable/SkillEffectTable/UnlockNodeTable + BreakthroughTable）；
  - 入组 **Configuration-UnitConfig**，地址 = unitId（⚠️ 组名不是 "UnitConfigs"——2026-10-08 纠正）。
- 显示名占位时：定名后改 NameMap 该行 + CharacterTable.name 列，重跑脚本即可（脚本幂等）。

## 步骤 3：默认持有（🤖）

- `PlayerDataCenter.DefaultCharacterIds` 追加 unitId。是否默认持有由 📐 定。
- ⚠️ **默认列表只对建新号生效**（`EnsureNewAccountDefaults` 判 `PlayerData/` 目录不存在才发，老档幂等跳过）——已有存档看不到新角色时，需一次性探针给仓库 `AddCharacter(unitId)` 补授（变更即存；防御：仓库快照为空 = Load 失败兜底对象，拒绝写入防覆盖真档；跑完即删脚本）。

## 步骤 4：模型预制体（🤖 接线 + 🖐 素材/摆放）

**🖐 用户先备好**：
- 非战斗预制体 `Assets/Prefabs/Unit/Character/Non-Combat/<unitId>/<unitId>.prefab`（根挂 PlayableDirector）；
- TimelineClips：`Non-Combat/<unitId>/TimelineClips/*.playable`（每页配什么动作由用户自定）。

**🤖 随后**（模式脚本 `SetupCh0209ModelPrefab`，新角色复制改路径）：
- CultivateModelDisplay 挂根 + director 引用 + **playOnAwake 关**（播放由面板切页驱动）；
- pages 按「**数组下标即页号**」语义接线（0 属性/1 武器/2 饰品/3 技能，与 CultivatePanelView.pages 对齐）；未配置页留 null = 停播静立不转向；
- HaloHelper 预接线：followTarget = 头骨（统一骨骼路径 `bone_root/Bip001/…/Bip001 Head`）、offsetSpace = **Bip001**（身体朝向承载者是 Bip001，指模型根会在换动作时方向错）；不动绑定数据；
- 入组 **Prefab-UI-CultivatePanel-Characters**：地址 = unitId，标签 **UI + Non-Combat**（与 SO 同址不同型，加载走 `AcquireAssetWithLabelsAsync<GameObject>(unitId, ["UI","Non-Combat"])` 标签交集过滤）。

**🖐 用户后手调**：
- pages[0] 属性页 Timeline 选择（Inspector 自配）；
- 光环位置摆放 + HaloHelper「绑定当前偏移」定稿。

> ⚠️ SerializedProperty 陷阱：`arraySize` 扩容会把**最后一个旧元素复制进新槽**——扩页数组后必须显式写每个新槽，不能假设新槽为默认值。

## 步骤 5：头像/图标（🖐）

**图全部由用户自己操作**（出图、放目录、登记 Addressables）；🤖 只在接入完成后列需求表。

**单图 Addressables 约定（2026-10-08 拍板，替代图集方案）**——图集整页常驻在 300 角色口径下撑不住（头像 404×456 一页 2048² 只装 16 张，300 张 ≈ 19 页 ≈ 55-70MB 常驻 + 每加角色整图集重打），改为单图按需 + 缓存：

- 命名：`CHAvatar_<变体>_<unitId>.png`（Small 252×204 / Collection 404×456 / SpecialPopup / PlayerAvatar）
- 登记：按变体入 Addressables 组 **Sprite-Character-Avatar-{Small,Collection,SpecialPopup,PlayerAvatar}**（用户重组后盘上真名），地址 = 文件名去扩展
- 加载：代码统一走 `IconLibrary.GetCharacterAvatarAsync(IconLibrary.VariantSmall, unitId)`（png 子资源走「名[名]」子地址语法；缓存 + 并发去重 + Persistent Lease，失败返回 null 保留默认图）
- 池化单元格异步回包必须校验 `cell.unitId` 未改绑（防串图）
- 武器图标（2026-10-08 实装）：同一套通道，命名 `CHWeapon_<unitId>`，组 **Sprite-Character-Weapon-Icon**；加载走 `IconLibrary.GetAsync($"CHWeapon_{unitId}")`

| 用途 | 命名 / 规格 | 状态 |
|---|---|---|
| 列表/培养小头像 | `CHAvatar_Small_<unitId>`（252×204） | CH0221/CH0209 已登记 ✓ |
| 收藏立绘 | `CHAvatar_Collection_<unitId>`（404×456） | 文件就位（Common/Avatars/），收藏 UI 接入时登记 |
| 好感/名片头像 | `CHAvatar_Favor/PlayerAvatar_<unitId>` | 组已建；名片暂用 Student_Portrait_Airi_Small（已登记 PlayerAvatar 组，命名统一时替换） |
| 武器图标 | `CHWeapon_<unitId>`（组 Sprite-Character-Weapon-Icon） | CH0221/CH0209 已登记 ✓（Gun 页同步消费） |

新需求出现时追加行，本表随版本迭代。

## 步骤 6：端到端验证（🤖）

- 模式脚本 `VerifyCh0209Onboarding`（新角色复制改常量）：四表行/引用链 → SO → 两组同址条目（+标签）→ 预制体 SerializedObject 回读；全绿输出 `[完成] XX 接入端到端验证通过`。
- 涉及玩家可见变化的，按 run-and-observe 落截图到 `production/qa/evidence/`。

## 设计挂账（📐 未拍板，接入时按临时口径绕行）

| 问题 | 现状 | 临时口径 |
|---|---|---|
| 突破表无角色维度 | BreakthroughTable 共享表 unlockNodeIds 写死 CH0221 的 22121-22124，任意角色突破都会解锁这些节点 | 属性不受影响（Calculator 只认本角色 bonusIds）；新角色加成节点一律非空 cost 独立解锁；per-character 突破关系待设计（[[05-玩法设计/设计挂账]] #10） |
| 星级乘区 | v1 恒 1，抽卡接入后扩展 | — |

## 版本记录

- **v1（2026-10-08）**：首版，自 CH0209 第二角色接入全过程提炼。含：TableRowInserter 工具、Configuration-UnitConfig 组名纠正、pages 下标即页号语义、图片需求表规则（图一律用户操作）。
- **v1.1（2026-10-08）**：步骤 3 补老档补授口径；步骤 5 重写为单图 Addressables 约定（CHAvatar_<变体>_<unitId> + 变体分组 + IconLibrary，弃图集），预挂武器图标同通道。
- **v1.2（2026-10-14）**：迁移入 02-流程SOP 目录，链接指向新知识库结构。
