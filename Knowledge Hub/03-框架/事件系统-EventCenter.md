---
tags: [project, framework, event]
created: 2026-10-14
updated: 2026-10-14
---

# 事件系统（EventCenter）

代码位置：`Assets/Scripts/CSharp/Core/Gear/EventSystem/EventCenter.cs`。事件名常量集中 在 `EventNames`（按 Gameplay/系统区分区段，新事件加在对应区段末尾）。

## API 与用法

| API | 说明 |
|---|---|
| `AddEventListener<T>(事件名, 回调)` | 订阅 |
| `SetEventTrigger<T>(事件名, 参数)` | 发布 |
| `RemoveEventListener<T>(事件名, 回调)` | 退订 |

## 项目惯例与坑

- **注册/退订必须成对**：面板 `EventInit` 注册 ↔ `OnDestroy` 反注册（HomePanel/CombatReadinessPanel 先例）；自管理组件 `OnEnable` 注册 ↔ `OnDisable` 反注册（GearBar 先例）
- 面板刷新链：数据管理器变更即广播（如 `BagUpdated` / `CharacterDataUpdated` / `TrinketUpdated` / `LoadSceneProgress`）→ 面板订阅刷新，**不走轮询**
- 自定义 Action/delegate 事件 = 禁区，一切跨系统通知走 EventCenter
- 任务条件追踪（未开工）也设计为事件驱动：EventCenter 广播 → 条件计数器

## 关联

- [[04-模块/玩家数据中心]] —— 各存档域的事件名清单
- [[03-框架/UI框架-UISystem]] —— 面板生命周期与反注册时机
