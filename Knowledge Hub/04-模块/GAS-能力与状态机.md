---
tags: [project, gas, betterhsm, state-machine]
created: 2026-10-14
updated: 2026-10-24
---

# GAS 能力与状态机（GAS × BE × BHSM）

代码位置：`Assets/Scripts/CSharp/Core/Gameplay/GAS/`（Core/UnitConfig、UnitConfigLibrary、Behavior/、Abilities/Configuration/、Display/）+ `Core/Gameplay/GAS/BetterHSM/`。SO 资产：`Assets/Settings/Ability Configurations/` 与 `Assets/Configurations/Unit/Character/`。GAS 模板 → [[06-模板/模板-GAS能力]]、[[06-模板/模板-BHSM状态]]；新状态机方案流程 → [[02-流程SOP/SOP-新加状态机方案]]；BE 表现层 → [[03-框架/行为表现-BehaviorEditor]]。

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

## 显示能力 DisplayAbility（2026-10-22 交付）

单位信息显示面板（屏幕固定面板）的 GAS 能力：把单位属性（攻击/生命/暴击等）+ buff 汇总显示给玩家；敌人与角色显示字段不同，将来其他对象（守点等）可复用。**范围拆分**：本次交付能力层 + 选中逻辑 + View/Control 脚本代码（绑定留空）；面板预制体由用户自制，对接挂账待面板就绪。

代码位置：`Assets/Scripts/CSharp/Core/Gameplay/GAS/Display/`（5 文件）+ 选中 `Core/Gameplay/Battle/BattleSelection.cs`、`Bissness/Combat/CombatSelectionPicker.cs` + 面板脚本 `Bissness/UI/Panels/Control/UnitInfoPanel.cs`、`View/UnitInfoPanel/UnitInfoPanelView.cs`。验证脚本 `AgentScripts/VerifyDisplayAbility.cs`（**DISPLAY-ALL-PASS 39/39**）。能力模板 → [[06-模板/模板-GAS能力]]。

- **架构 = 统一能力（SO + 单 Runtime），变异放 SO 配置**：`DisplayAbilitySO`（`fieldEntries` 字段条目列表 + `buffSection` 汇总段配置）→ Runtime 遍历条目产显示模型，**不做按种类的代码 Switch**——敌我差异 = 各自 SO 条目不同（角色含 CritRate/CritDmg，敌人不含），同单位喂敌人 SO → 字段集跟随 SO（验证 D 段实证）；基类非 sealed 留 virtual 逃生门；加新显示种类 = 新建一份 SO + 复用面板
- **显示源/风格双枚举正交**：`DisplaySource`（Stat/CurrentHp/MaxHp/HpPercent/DisplayName/UnitId/Camp/Vital）× `DisplayStyle`（Number/Float/Percent/Text/Bar）纯配置组合，非行为分派；`DisplayFieldEntry` = key + label + source + statType（source==Stat 时有效）+ style
- **数据链（对齐 Stat 懒就绪先例）**：`DisplayAbilityRuntime` 在 `AbilityUpdate` 惰性等 **BattleUnit + Stat** 两键注册后 `new DisplayRuntimeData(config, unit.Unit, statData)` → `OwnerContext.Register(Display, _displayData)`，此后每帧零开销（BuildModel 由面板拉取调用）；未预热报错一次后停摆，不阻断其他能力。`AbilityRuntimeDataType` 新增 `Display` 键
- **`BuildModel` 纯函数**：`DisplayRuntimeData.BuildModel()` 每次读当前值产全新模型（HP 每帧变，面板本就逐帧读），零缓存无脏标记。头部恒产（CurrentHp=unit.Hp / MaxHp=Stat final 且 >0 才用、否则回退 unit.Stats.MaxHp / HpRatio=Clamp01）；数值条目 `ApplyNumeric`（Number=FloorToInt、Float=F1、Percent=×100 整数百分比、Bar=Ratio+百分数文本）；`Modified` 标记 = Stat 源且存在同 Type modifier（面板高亮被 buff 影响的行）
- **显示名异步回填**：`RefreshDisplayNameAsync` fire-and-forget——角色走 `UnitConfigLibrary.GetAsync` 取 config.Name；敌人走 `DataTableManager` 查 EnemyTable row.Name；未回填前回退 UnitId；失败吞异常只告警。跨 run_script 进程缓存不定，验证脚本 B 段直构 DisplayRuntimeData 手动注入做确定性断言
- **选中与面板**：`BattleSelection`（静态 Current，选中态比面板长寿——ESC 关面板后可重开）+ `CombatSelectionPicker`（左键点单位 → `CombatManager.TryGetUnitByView` 反查；`IsWorldClick` 点空白清选中；UI 悬停 `IsPointerOverGameObject` 不穿透）。`UnitInfoPanel`（PanelBase MVC 的 C）逐帧轮询：点击选中/清选 + `RefreshReadout`（数据链：SelectedView → GAS → OwnerContext TryGet(Display) → BuildModel → WriteText 节流渲染）；**无 EventCenter 订阅**（对齐 HsmDebugPanel）。`UnitInfoPanelView` 序列化绑定留空 + `EnsureBindings()` transform.Find 兜底，节点路径约定写注释供用户自制面板对齐
- **buff 汇总段（v1 关闸恒空）**：`DisplayBuffSectionConfig`（enabled 默认 false + headerLabel + style + maxShown）→ `BuildBuffs`：经 `StatRuntimeData.Modifiers` 只读出口按 SourceId 分组，StackCount=同源条数，每属性 `MarginalDelta` = 剔除本来源前后 final 差（`StatMath.Evaluate` 两遍——PercentAdd/Mul 非线性不能简单求和），maxShown>0 截断。真 buff 系统（时长/图标/名称/BuffApplied/Removed 事件）挂账

**挂账待对接（用户面板就绪后）**：面板预制体（节点名按 View 注释约定 / 根 active=true / BG raycastTarget=false）；Addressables 注册 address=`UnitInfoPanel` 入 Prefab-UI-Panels-Normal；两份 DisplayAbility .asset 实例 + 字段清单定稿；prefab GAS `_abilityDefinitions` 接线（CH0221 / EN_Solider_AR_0001）；View/Control 拖拽绑定；**CombatManager 改动整体挂账**（Start 建 BattleSelection / AssembleAsync 开面板 UILayer.Top / Update F2 切换+点击探针 / EnsureSelectCollider 敌人补 CapsuleCollider 半径 0.3/高 1/中心 y0.5）；点击射线联调；buff 系统本体。

## Buff 能力 BuffAbility（2026-10-24 交付）

管控单位身上 Buff 的 GAS 能力：注册、帧轮询、注销、访问，并为 UnitStatPanel 的 **Effect 页**提供 buff 展示数据源。Buff 类逻辑自洽（每类自带生效/失效/逐帧/到期判定），宿主只做注册、轮询、注销驱动。**范围拆分**：能力层 + Effect 展示代码交付（buff 元数据 2026-11-01 起并入 SkillEffectTable，BuffTable 已退役删除）；战斗 prefab 接线（SetupBuffAbility）、事件广播、伤害/治疗 buff、图标资产挂账。

代码位置：`Assets/Scripts/CSharp/Core/Gameplay/GAS/Buff/`（IBuff + BuffRuntimeData + BuffAbilityRuntime + BuffAbilitySO，4 文件）+ 展示 `Bissness/UI/Panels/View/UnitStatPanel/UnitStatPanelView.cs`、`Control/UnitStatPanel.cs`。验证脚本 `AgentScripts/VerifyBuff.cs`（**BUFF-ALL-PASS 19/19**，2026-11-01 BuffTable 退役后 E/F3 表段移除）。

- **能力 SO 零可配置参数**：Buff 实例由应用方 `new` 并经 `BuffRuntimeData.ApplyBuff` 注入（不入资产、不进 SO），`BuffAbilitySO` 只是装配占位——用户拍板"Buff 类逻辑自洽，SO 无需任何参数"
- **IBuff 契约（Buff 类必须实现四方法 + BuffId）**：`int BuffId { get; }`（效果行主键 SkillEffectTable.effectId，注册时宿主据此登记索引 + 展示按它查元数据）；`RegisterCallback(BuffRuntimeData host)`（入列前调用，生效副作用=加属性，推荐 `AddModifier("buff_{BuffId}", ...)`，**禁在方法内调 host.RemoveBuff**——注销由宿主统一驱动会递归）；`UnregisterCallback()`（撤销生效副作用，`RemoveBySource("buff_{BuffId}")` 整组撤回，幂等）；`OnUpdate(float dt)`（存续期逐帧内部逻辑，不得私自改宿主列表）；`DoneCallback(float dt)`（**帧中断判断，bool 返回**：true=到期，宿主同帧统一注销，本帧绝不再收 OnUpdate）
- **Tick 三段时序（对齐"帧中断先于帧轮询"语义）**：阶段一【帧中断·只读】正向遍历 `_buffs` 调 `DoneCallback(dt)`，true 收进本地 expired（零删改）；阶段二【统一注销】遍历 expired 快照：先 `UnregisterCallback()`（撤 StatModifier）再移出 `_buffs` + `_byBuffId`；阶段三【帧轮询】对 `_buffs` 做一次性快照，调用前 `_buffs.Contains` 校验命中才 `OnUpdate(dt)`——本帧到期绝不再收本帧轮询，OnUpdate 内可安全 RemoveBuff
- **数据链（对齐 Stat/Display 懒就绪先例）**：`BuffAbilityRuntime.AbilityUpdate` 惰性等 **BattleUnit + Stat** 两键注册后 `new BuffRuntimeData(OwnerContext, unitData.Unit, statData)` → `OwnerContext.Register(Buff, _buffData)`，此后每帧 `_buffData.Tick(deltaTime)`；未就绪报错一次后停摆。`AbilityRuntimeDataType` 新增 `Buff` 键
- **Buff↔Stat 联动约定**：`SourceId = $"buff_{BuffId}"`——同 buffId 叠层共用同一来源，`RemoveBySource` 一次撤净（首条到期余条仍注册但加成已整组撤，验证 B3 实证）；`StatRuntimeData.AddModifier` 拒空 SourceId
- **buff 元数据（2026-11-01 起）**：并入 `SkillEffectTable` 效果行（effectId/effectType/target/value/growth/statType/duration/name/icon/desc），施加与展示同源；`BuffDataLibrary`/`BuffRow`/`BuffTable` 已删除，查询走 `BattleDataModule.TryGetSkillEffect(effectId, out SkillEffectRow)`（战斗 `PreloadAsync` 已加载该表）
- **UnitStatPanel Effect 页展示**：同 Display 路径读 Buff 键（独立于 Display，Buff 可能先就绪）→ 逐帧 `BuildEffectSignature`（active buffs 的 BuffId 序列含叠层）**签名门控**（面板打开战斗已暂停 buff 不 tick，签名未变不重刷）→ 签名变化回池现有行 + 池化克隆 `EffectRowTemplate`（`ObjectsPool.Get` 后归一 localScale/localPosition/pivot 左上/z 归零）+ `EffectItemView` 直接填名称/描述（`BattleDataModule.TryGetSkillEffect` 查 SkillEffectTable 效果行）+ 图标异步 `IconLibrary.GetAsync`（icon 空跳过，回包前签名校验防竞态弃写）；模板缺失告警一次不刷，模板实例仍激活首见失活防幻影行
- **验证口径**：E 段（BuffTable 加载）与 F3（BuffRow 映射）随表退役移除；buff 数值/时长/成长路径由 `VerifySkillEffect` D 段覆盖（D1-D7：属性映射 / 10s·12s 时长 / 等级成长 / Stat 合成 / 到期注销）

**挂账待对接（buff 系统本体下一轮）**：事件广播 `BuffApplied/BuffRemoved`（EventNames 常量已在，全库零消费者）；Display buff 汇总段回填（`DisplayRuntimeData.BuildBuffs` 仍关闸，真 buff 就绪后 DisplayName 从 SourceId 兜底升级 SkillEffectRow.Name）；剩余时间/叠层数 UI（v1 同 id 叠层各占一行无 xN）；伤害/治疗 buff（Hp internal set，走 BE 事件链）；**战斗 prefab GAS 接线**（SetupBuffAbility 挂账，未接线 Effect 页空转不报错）；buff 施放来源（无 gameplay 代码 ApplyBuff，Effect 页 v1 恒空属预期）；buff 图标资产（SkillEffectRow.Icon 指向的 sprite 待美术补，缺图保留默认图）。

## 关联

- [[02-流程SOP/SOP-新加状态机方案]] / [[02-流程SOP/SOP-新加角色]]
- [[04-模块/角色养成系统]] —— UnitConfig 聚合根
