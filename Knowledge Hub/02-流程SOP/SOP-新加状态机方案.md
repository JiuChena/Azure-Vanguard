---
tags: [project, sop, betterhsm, state-machine]
created: 2026-10-14
updated: 2026-10-14
---

# SOP-新加状态机方案（BetterHSM）

> 给一个单位（角色/敌人）接入 BetterHSM 状态机方案的全流程。
> 机制与分层语义详解：[[04-模块/GAS-能力与状态机]]；状态类/注册工厂代码模板：[[06-模板/模板-BHSM状态]]。
> 样板：扁平版 `CH0221TestRegistration`（深度 1）；分层版 `CH0221TestHierarchicalRegistration`（GAS/BetterHSM/Test/CH0221/）。

## 前置知识

- BetterHSM 在 `Assets/Scripts/CSharp/Core/Gameplay/GAS/BetterHSM/`，**命名空间 `Core.Gear`**（漏 using 高发）
- 分层语义速记：状态树 root→leaf 激活链；**层级即优先序**（父层先评）；切换 = LCA 级联；复合状态无 OnUpdate、不可为边目标/初始状态；门控默认关（`EnableInterruptGating` + `MarkDirty`）
- **硬约束：框架按状态类型唯一注册**——同一 HSM 内每个节点必须是独立 C# 类，两个节点不能共用一个状态类
- **硬约束（2026-11-05）：角色战斗 BehaviorClip 必须经 BHSM 状态播放**——状态 `OnEnter` 调 `Context.PlayBehavior(BehaviorKey)`；**禁止**在 `CombatManager` / MonoBehaviour / 其他系统直接调 `BehaviorExecutor.Play` / `BehaviorAbilityRuntimeData.Play` 播放角色战斗 clip。一次性 clip（如进场 `Talent`）也必须建 HSM 状态（Once）+ 完成度门禁，不能用外部定时器补播/回 Idle。验证脚本测试夹具除外

## 流程总览

| # | 步骤 | 分工 | 产出 |
|---|---|---|---|
| 1 | 状态图设计 | 📐（结构）+🤖（梳理成边表） | 状态/边/优先级表 |
| 2 | 写状态类 + Registration | 🤖 | Registration 静态类 |
| 3 | 枚举 + 目录注册 | 🤖 | BetterHSMRegistrationId + SchemeCatalog |
| 4 | 能力 SO 资产 | 🤖 | Assets/Settings/Ability Configurations/*.asset |
| 5 | 预制体接线 | 🤖 挂能力 + 🖐 摆放验证 | prefab 能力列表 |
| 6 | 验证 | 🤖 | smoke/断言脚本 + Play 截图 |

## 步骤 1：状态图设计（📐→🤖）

向用户要（或从需求梳理后交确认）：

| 项 | 说明 |
|---|---|
| 状态清单 | 哪些叶子状态（Idle/Move/Attack…）；哪些复合状态（Locomotion/Combat…） |
| 边 | 源状态 → 目标（叶子）→ 触发条件（Interrupt 谓词）→ priority（**同层内**才有意义） |
| 层级 | AddChild 父子关系；哪些层启用门控（数据变更频率低的中断层收益大） |
| 初始状态 | 必须是叶子 |

产出边表（写进 Registration 注释），例见 [[04-模块/GAS-能力与状态机]] 嵌套注册示例。

## 步骤 2：写状态类 + Registration（🤖）

照 [[06-模板/模板-BHSM状态]]：

- 每节点一个独立 C# 类（叶子继承自定 StateBase 子类或框架 StateBase；**行为键**：叶子带 `public const string BehaviorKey` 对齐 UnitConfig 行为表）
- Registration 静态类：`BeginBuild` → AddState（根先注册）→ AddChild（父先子后）→ From/To（叶子层边与父层边）→ EnableInterruptGating → Build(初始叶子)

## 步骤 3：枚举 + 目录注册（🤖）

1. `BetterHSMRegistrationId.cs` 枚举加值（如 `CH0221TestHierarchical`）
2. `BetterHSMAbilitySchemeCatalog.cs` 对应 EntityCategory 字典加映射 `{ 枚举值, <Registration>.Create }`

## 步骤 4：能力 SO 资产（🤖）

`AgentScripts/Setup<名>Hsm.cs`（幂等，先例 `SetupCH0221HierHsm.cs`）：

- 创建 `Assets/Settings/Ability Configurations/<名>BetterHSMAbility.asset`
- `_entityCategory`（enumValueIndex）、`_registrationId`（enumValueIndex）
- **盘上回读确认**（读 .asset YAML 检查 `_entityCategory: 0` / `_registrationId: 1`）

## 步骤 5：预制体接线（🤖 + 🖐）

- 🤖：prefab 能力列表追加该 SO（LoadPrefabContents + SerializedObject，先例 `SetupCH0221Gas.cs`）——或交用户在 Inspector 拖
- 🖐：CombatTest 场景摆放/Play 观察由用户协调

## 步骤 6：验证（🤖）

1. **smoke/断言脚本**（AgentScripts，run_script 驱动）：按状态图写生命周期断言（先例 `BetterHsmSmoke.cs`——A 同根 LCA / B 父层切子 / C 门控四段 / D no-op / E 跨树级联 / F 优先级 / G 构建负例）。**期望值硬编码独立推，不抄实现**
2. 编译三重验证（[[01-项目总览/工具与环境]]）
3. Play 观察截图 → `production/qa/evidence/`
4. 文档：[[04-模块/GAS-能力与状态机]] 更新 + 日志（[[_规范/日志规范]]）

## 常见坑

| 坑 | 后果 |
|---|---|
| 漏 `using Core.Gear` | BetterHSM/StateBase/StateGraphBuilder 全 CS0246 |
| 两节点共用一个状态类 | InvalidOperationException：不允许重复注册状态类型 |
| 复合状态当边目标/初始状态 | 构建期抛错 |
| 复合状态写 OnUpdate | 不生效（复合状态无 OnUpdate，勿绕） |
| 跨层比较 priority | 无效——priority 只在同层竞争 |
| 忘 MarkDirty（启用门控后） | 该层中断永不评估（门控默认关，不用不踩） |

## 版本记录

- **v1（2026-10-14）**：首版，自 BHSM 分层改造（枚举/目录/SO/smoke 全链）提炼。
