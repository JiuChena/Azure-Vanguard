---
tags: [project, gas, betterhsm, state-machine]
created: 2026-10-14
updated: 2026-10-14
---

# GAS 能力与状态机（GAS × BE × BHSM）

代码位置：`Assets/Scripts/CSharp/Core/Gameplay/GAS/`（Core/UnitConfig、UnitConfigLibrary、Behavior/、Abilities/Configuration/）+ `Core/Gameplay/GAS/BetterHSM/`。SO 资产：`Assets/Settings/Ability Configurations/` 与 `Assets/Configurations/Unit/Character/`。GAS 模板 → [[06-模板/模板-GAS能力]]、[[06-模板/模板-BHSM状态]]；新状态机方案流程 → [[02-流程SOP/SOP-新加状态机方案]]；BE 表现层 → [[03-框架/行为表现-BehaviorEditor]]。

## 桥接总纲（2026-09-26 定稿）

GAS（Mono 壳）作为 BE（纯 C# BehaviorExecutor）的每帧输出窗口，状态图（BetterHSM）经状态上下文按键驱动行为播放。

- **`UnitConfig`**（SO，挂 GAS 组件"单位配置"）：`string unitId` + `unitName`（显示名准绳）+ `SerializableDictionary<string, BehaviorClip>` 行为表；键 = 叶子状态类 `public const string BehaviorKey`。纯数据（图标/阵营/数值）一律进 Excel 配置表不进 SO；角色资产用 `CharacterUnitConfig`（含养成/突破表引用，[[04-模块/角色养成系统]]）
- **`Core.Gear.SerializableDictionary<TKey,TValue>`**：双平行列表 + 懒构建查询字典（长度不齐/重复键有 Log 校验），替代 Unity 不可序列化的原生 Dictionary
- **Behavior 能力**：`BehaviorAbilitySO`（`_logBehaviorFlow`）→ `BehaviorAbilityRuntime`（建执行器、常开 Tick、注册 RuntimeData）→ `BehaviorAbilityRuntimeData.Play(key)/Stop()`（表查 clip → executor.Play；缺失分级 Log 并忽略，不崩逻辑层）
- **播放生命周期约定**：状态 OnEnter Play、OnExit Stop；BE RuntimeData.Reset 空实现
- **框架增量**：`AbilityRuntimeDataType.Behavior` 键、`AbilityOwnerContext` 可选 UnitConfig 参数
- **反向事件（无需桥接）**：BE 自带完整事件执行链（[[03-框架/行为表现-BehaviorEditor]]）；需要触达玩法时按需写项目侧 ExecuteSO 子类

**新增角色三步**：`BetterHSMRegistrationId` 枚举加值 → `BetterHSMAbilitySchemeCatalog` 加映射 → 写 Registration 静态类；叶子状态带 BehaviorKey，UnitConfig 表对齐键名。

**CH0221 已落地**：`Settings/Unit Configurations/CH0221UnitConfig.asset`（键 Idle/Move 待补 clip）+ `Settings/Ability Configurations/{BehaviorAbility, CH0221BetterHSMAbility}.asset`；`CH0221.prefab` 已挂 GAS，能力列表 [InputListener, CH0221BetterHSMAbility, BehaviorAbility] + UnitConfig 引用。落地走 `AgentScripts/SetupCH0221Gas.cs`（幂等）。

## BetterHSM 分层状态图（2026-10-14 分层改造落地）

BetterHSM 从扁平图升级为真·分层图（`Core.Gear` 五文件重写，CH0221 原版 demo 零改动兼容——扁平图 = 深度 1 分层图）。smoke 断言 A-G 全绿（`AgentScripts/BetterHsmSmoke.cs`，run_script 驱动）。

- **层级与激活链**：状态经 `AddChild(parent, child)` 构成树，激活链恒为 root→leaf 单路径。复合状态是结构节点——只经链收 OnEnter/OnExit，**无 OnUpdate**，且**不可作为边目标或初始状态**（构建期抛错）；"进父状态"没有默认子语义，需要时另议 DefaultChild
- **层级即优先序**：每帧沿激活链自根向叶逐层仲裁中断，父层先评、命中即止；`priority` 数值只在**同层**（同一来源状态的边）内竞争，跨层先后由激活链决定。容器 Seal 时按 priority 降序 + 注册序升序一次性排序，运行期首真即停（零分配，无运行时堆）
- **切换 = LCA 级联**：目标必为叶子；退出叶→LCA、进入 LCA→叶（自上而下）；跨树（多根）整链全退全进。每帧最多一次切换，切换帧不调任何 OnUpdate；`CurrentState` 恒为叶子
- **脏标记门控（默认关闭）**：`graph.EnableInterruptGating(state)` 启用后，该层仅 `MarkDirty()` 之后的帧才评估中断，未标脏整层跳过（叶子 OnUpdate 照常）。业务在数据变更处调 `state.MarkDirty()`（事件式驱动，与状态是否激活无关），框架消费即清，同帧多次合并。**未启用门控 = 每帧照常评估**（向后兼容）
- **约束（重要）**：①框架按状态**类型**唯一注册——同一 HSM 内每个节点必须是独立 C# 类（两个节点不能共用一个状态类，重复注册抛 InvalidOperationException）；②`AddChild` 父必须先注册、child 必须未注册（父先子后 + 每态一次 ⇒ 环结构性不可能）；③`Interrupt()` 由 abstract 放宽为 virtual 默认 false——复合状态不再被迫实现

### 嵌套注册示例（分层 demo `GAS/BetterHSM/Test/CH0221/CH0221TestHierarchicalRegistration.cs`）

```csharp
StateGraphBuilder<Context> graph = betterHsm.BeginBuild();
graph.AddState(locomotionState).AddState(combatState);            // 根先注册
graph.AddChild(locomotionState, idleState).AddChild(locomotionState, moveState);
graph.AddChild(combatState, attackState);
graph.From(idleState).To(moveState, moveState.Interrupt, priority: 10);       // 叶子层边
graph.From(locomotionState).To(idleState, idleState.Interrupt, priority: 10); // 父层边（先于叶子评估）
graph.From(locomotionState).To(attackState, stateContext.HasCombatRequest, priority: 50); // 跨树边
graph.EnableInterruptGating(locomotionState);                     // 数据变更处调 locomotionState.MarkDirty()
graph.Build(idleState);                                           // 初始状态必须是叶子
```

### 分层 demo 资产

`Settings/Ability Configurations/CH0221HierarchicalBetterHSMAbility.asset`（`_entityCategory=0 Character`，`_registrationId=1 CH0221TestHierarchical`，落地脚本 `AgentScripts/SetupCH0221HierHsm.cs`）；**预制体接线待用户**（Play 回归挂起）。

## 单位 ID string 化（2026-10-07）

单位 id 全链路从 int（221）改为字符串 `CH0221`，六方一致：**SO 资产名 = UnitConfig.unitId = CharacterTable 主键 = 存档仓库键 = Addressables 地址 = 头像图集 sprite 名**。技能 id（22101…）与节点 id（22111…）保持 int。

- `UnitConfig`：`unitId` 改 string + `unitName` 序列化字段 + `public string Name => unitName`（字段名避 Object.name 遮蔽 CS0108）。SO 资产 `Assets/Configurations/Unit/Character/CH0221.asset`（`AgentScripts/SetupUnitConfigsAddressables.cs` 幂等）
- **Addressables**：组 **Configuration-UnitConfig**，地址 = unitId；新角色资产在 SetupUnitConfigsAddressables 的 NameMap 追加显示名即可全套落地
- **数据链**：CharacterRow→IConfigTableRow&lt;string&gt;；CharacterEntry [Key(0)] unitId string（[Key] 序号不变，旧档反序列化失败自动降级空档）；仓库 14 签名 string；ProgressionManager 角色表泛型 string（Level/Breakthrough/UnlockNode 三表仍 int）；两面板 + Model + CultivateCharacterItem 全 string，排序平局比较 string.CompareOrdinal
- **名字准绳**：UI 显示角色名以 **SO.Name 为准**（列表页 PreloadNamesAsync 批量预热；养成页解析进 model.UnitName），加载失败/空值回退表行 Name
- **头像**：图集 sprite 名 = 单位 ID（CH0221 已打包进 CharacterListPanel.spriteatlasv2）；后续新角色头像走单图 IconLibrary 通道（[[02-流程SOP/SOP-新加角色]] 步骤 5）

## 关联

- [[02-流程SOP/SOP-新加状态机方案]] / [[02-流程SOP/SOP-新加角色]]
- [[04-模块/角色养成系统]] —— UnitConfig 聚合根
