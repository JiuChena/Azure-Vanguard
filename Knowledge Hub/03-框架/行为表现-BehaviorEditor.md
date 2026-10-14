---
tags: [project, framework, behavioreditor, presentation]
created: 2026-10-14
updated: 2026-10-14
---

# 行为表现（BehaviorEditor，BE）

代码位置：`Assets/Scripts/CSharp/Core/Gameplay/BehaviorEditor/`——`Data/`（BehaviorClip 数据）/ `Tracks/`（轨道）/ `Runtime/`（BehaviorExecutor 等执行器）。**纯 C# 表现层，不依赖 UnityEngine 游戏逻辑**；GAS 侧驱动方式见 [[04-模块/GAS-能力与状态机]]。

## 定位

- BE 负责"一个行为片段（BehaviorClip）怎么播"：轨道数据 + 到点分发。`.playable`/TimelineClips 由用户在编辑器制作（养成页动画先例：`Non-Combat/<unitId>/TimelineClips/*.playable`，每页配什么动作由用户自定）
- 行为数据（clip）挂在 UnitConfig SO 的行为表上：键 = 叶子状态类 `public const string BehaviorKey`（见 GAS 篇）

## 播放生命周期约定（GAS×BE 桥接口径）

- 状态 `OnEnter` 调 `Play(key)`、`OnExit` 调 `Stop()`
- `BehaviorAbilityRuntimeData.Reset` 空实现——通用重置不打断表现

## 反向事件（BE → 玩法）

BE 自带完整事件执行链：`EventTrackExecutor` 排序/到点分发/骨骼位姿解析 → `BehaviorEventExecuteSO.Execute`；GAS **不另做事件系统**。

- 需要触达玩法时：按需写项目侧 `ExecuteSO` 子类——Context 已含 `OwnerGameObject`/`OwnerTransform`，`GetComponent` 即达 GAS
- 纯表现类事件子类不碰 GAS

## 关联

- [[04-模块/GAS-能力与状态机]] —— 谁在何时播哪个 clip
- [[02-流程SOP/SOP-新加角色]] —— 步骤 4 模型预制体（TimelineClips 用户手调）
