---
tags: [project, interview, system-design, architecture]
created: 2026-06-20
updated: 2026-06-20
source: Miniature Garden 项目知识库提炼
---

# 什亭之箱 (Miniature Garden) — 面试项目参考

> 箱庭RPG · Unity 全案独立开发 · 2026/01-2026/04
> 对应简历项目，面试官会深挖系统设计和架构决策

## 项目概览

| 维度 | 内容 |
|------|------|
| 类型 | 箱庭RPG PVE |
| 引擎 | Unity + 纯 C# |
| 资源 | Addressables 唯一通道 |
| 持久化 | MessagePack 二进制 |
| 架构 | Framework/（跨项目复用） + Business/MiniatureGarden/（项目专属）双层 |
| 热更新 | Addressables（资源）+ 纯 C#（代码，无 Lua） |

---

## 6 项关键架构决策（面试高频）

**面试金句**: "项目做了 6 个关键架构决策，每个都有明确的取舍理由。"

| # | 决策 | 选择 | 放弃 | 理由 |
|---|------|------|------|------|
| 1 | 语言 | **纯 C#** | XLua | Lua 维护成本高，单人开发纯 C# 更可控；后期如需热更可引入 HybridCLR |
| 2 | 持久化 | **MessagePack** | BinaryFormatter/JSON | 二进制格式体积更小速度更快，BinaryFormatter 已被标记为不安全 |
| 3 | 资源 | **Addressables** | Resources + AssetBundle | 避免 Resources 拖慢启动、红黑树常驻内存；Addressables 异步加载+引用计数 |
| 4 | 状态机 | **HSM 接口驱动** | 硬编码类型绑定 | 接口驱动使角色和敌人都能复用同一套 HSM，不耦合具体类型 |
| 5 | 技能 | **SO + Timeline 数据驱动** | 硬编码技能逻辑 | 美术/策划可在 Timeline 编排技能表现，自定义轨道导出为 BehaviorClip SO |
| 6 | 架构分层 | **Framework/Business 分离** | 单层混在一起 | Framework 层（事件中心/音频/持久化/HSM）跨项目复用，Business 层项目专属 |

---

## 核心系统设计（面试会追问实现细节）

### 1. 技能/行为系统（最亮点 ⭐）

```
设计思路：数据驱动 + 时间轴执行 + 事件落地

Timeline 编辑（美术友好）
  ├─ Animation Track    → 动作预览
  ├─ Audio Track        → 声音预览  
  ├─ 自定义轨道
  │    ├─ Event Track   → 特效/投射物/音效事件
  │    ├─ Hitbox Track  → 攻击判定框
  │    └─ Transition Track → 行为间过渡
  │
  └─ 导出为 BehaviorClip SO
       ↓
BehaviorInterpreter 运行时执行
  ├─ 按时间轴推进 → 触发事件 → IBehaviorEventReceiver 落地
  ├─ Hitbox NonAlloc 物理查询（球/胶囊/盒）
  └─ hitGroupId 去重
```

**面试追问回答**:
- Q: "为什么不用 Animator 直接做？" → "Animator 管动画状态，不管攻击判定、特效、音效的编排。Timeline + 自定义轨道把这些都统一在一条时间轴上。"
- Q: "BehaviorInterpreter 每帧做什么？" → "推进时间→检查动画段切换→执行到期事件→Hitbox检测→完成后发事件通知状态机。"

### 2. 数值系统（三层架构）

```
CharacterAssetInformation (SO 静态配置)
  ├─ 公式: y = A × (level - 1) + C
  ├─ 基础暴击率/暴伤/增伤/穿透
  └─ CharacterAbilityNumericProfile（技能倍率表）
       ↓ 结合
CharacterData（玩家养成数据: level/attackLevel/talentLevel）
       ↓ 运行时装配
StatusData（最终运行时值: AttackPower/Defense/Crit/DamageBonus + 修正器）
```

**伤害公式**: `max(1, rawAtk × multiplier × (1 + bonus) - def × (1 - penetration))`

### 3. 效果/Buff 系统

```
行为/技能 → UnitEffectController.ApplyEffect()
  → GlobalEffectSystem（全局单例，统一推进所有运行时效果）
    → EffectDefinitionSO.ResolveTargets → BuildSchedule
    → RuntimeEffectInstance.Tick()
    → StatusData 改值落地
```

**关键设计**: 效果系统不自己持有"最终面板"，所有修改最终落在 StatusData 上。**单一真相源**。

**面试对比**: "旧方案是 BuffSystem + BonusPanel 两套数据，出现不同步 Bug。重构后统一以 UnitEffectController + GlobalEffectSystem 为准。"

### 4. 战斗系统

```
数据流: 输入/AI → HSM进入行为 → BehaviorInterpreter/ProjectileBase触发命中
  → ProjectileDamageResolver.CalculateDamage()
  → StatusData.ReceiveDamage()
  → TypedEventBus.Publish(UnitDiedEvent)
  → EventCenter.Trigger(UnitDeath) [兼容旧监听]

Hitbox: 球/胶囊/盒 | NonAlloc物理查询 | hitGroupId去重 | HashSet<int>缓存已命中
```

### 5. 任务系统

```
TaskSystem启动 → QuestConditionTracker监听事件
  事件源: UnitDeath / BagUpdated / AreaEntered / InteractionPerformed
    → QuestManager.CheckStageCompletion()
    → QuestDataSO 配置（ID/条件/奖励/对话）
    → BinaryDataManager 持久化
```

### 6. 交互系统

```
InteractionEmitter (场景物体) + InteractionReceiver (玩家)
  → IInteractable 接口（CanInteract/OnInteract/OnEnterRange）
  → 世界空间UI动态生成选项
  → 滚轮切换 + F键执行
  → Priority排序候选项
```

### 7. UI 系统

```
Canvas 分层: WorldSpace（跳字/血条）| Bot/Mid/Top/System（Root Canvas）
PanelManager: Addressables异步加载 + 实例缓存 + 面板栈 ESC返回
PanelBase: LoadInit / ComponentInit / OnUpdate 生命周期
```

### 8. 核心框架层

| 组件 | 职责 |
|------|------|
| EventCenter | 字符串事件，偏兼容和广播（无参/1参/2参） |
| TypedEventBus | struct 事件，偏跨系统通知，Subscribe/Publish |
| HSM | 轻量状态机，AddState/SwitchState/Tick + TransitionGuard |
| Blackboard | 持续输入 + 单帧输入分离，ResetFrameData 清单帧 |
| AudioManager | 句柄模式 + 绑定对象模式 |
| BinaryDataManager | MessagePack 读写统一入口 |
| AddressableManager | 资源加载唯一通道，句柄缓存+引用计数 |

---

## 旧项目教训（面试加分项）

> "这个项目是从旧项目重构来的，踩了很多坑。"

| 旧问题 | 当前方案 |
|--------|---------|
| PlayerControlModule 800行上帝对象 | **拆分为 5 个独立 Module** |
| HSM 硬编码绑定具体类型 | **接口驱动 HSM** |
| Buff 系统两套数据源不同步 | **统一 UnitEffectController + GlobalEffectSystem** |
| Resources + Addressables 混用 | **只保留 Addressables** |
| BinaryFormatter 序列化不安全 | **统一 MessagePack** |
| XLua 维护成本高 | **纯 C#** |
| EventCenter 裸字符串魔法值 | **EventNames 常量类** |
| DataCenter.Start() 混入测试代码 | **Editor 工具脚本注入** |

---

## 面试常见追问应对

**Q: 为什么不用 Lua 热更新？**
> "单人开发项目，纯 C# 维护成本更低。如果需要热更新，后续可以引入 HybridCLR。Addressables 已经解决了资源热更的问题。"

**Q: HSM 四层怎么划分的？**
> "参考简历描述：数据层（配置/状态数据）、转换层（状态切换条件）、执行层（行为表现）、策略层（AI决策）。四层分离使行为逻辑有较强的策略拓展性。"

**Q: 做过哪些性能优化？**
> "① 对象池管理高频物体/特效/音频空物体 ② AddressableManager 句柄缓存+引用计数 ③ 异步加载+协程分帧 ④ Hitbox 使用 NonAlloc 物理查询 ⑤ Shader 避免分支 ⑥ 跳字合批缓冲池降低 DrawCall ⑦ Stencil Test 实现统一后处理描边"

**Q: BehaviorClip SO 的设计为什么不用 AnimatorController 直接做？**
> "AnimatorController 只管动画状态转换，无法编排攻击判定、特效生成、音效触发、投射物生成等逻辑。Timeline + 自定义轨道把这些都统一在一条时间轴上，策划和美术可以可视化编辑，导出为 BehaviorClip 后运行时高效执行。"

---

## 关联

- [[interview-behavioral|面试场景与行为题]] — 项目经验回答框架（STAR 法则）
- [[design-patterns|设计模式]] — SOLID、ECS、状态机在项目中的应用
- [[performance-optimization|性能优化]] — 项目实际优化案例
