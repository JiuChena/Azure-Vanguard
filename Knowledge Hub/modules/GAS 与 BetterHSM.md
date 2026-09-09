---
tags: [module, GAS, BetterHSM, input, state]
created: 2026-09-09
updated: 2026-09-09
---

# GAS 与 BetterHSM

## 目的与边界

GAS 解决“一个场景实体如何组合并驱动多种能力”；BetterHSM 解决“一个实体内部如何持有状态实例并仲裁状态转换”。两者结合，但不互相吞掉职责。

```text
GameplayAbilitySystem
    负责创建、排序驱动、禁用和销毁 Ability Runtime。

Ability Runtime
    负责自己的组件依赖、数据注册和玩法逻辑。

BetterHSM Ability Runtime
    负责创建和唯一驱动 BetterHSM。

BetterHSM
    负责当前状态、转换仲裁及状态生命周期。
```

`GameplayAbilitySystem` 位于：

```text
Assets/Scripts/CSharp/Core/Gameplay/GAS/Core/Component/GameplayAbilitySystem.cs
```

它是实体能力的唯一组件入口。不要为每个 Ability 单独创建一个 MonoBehaviour 容器。

## 使用 GAS

### 1. 在实体上挂载组件

在角色、敌人或 NPC 的同一 GameObject 上挂 `GameplayAbilitySystem`。Inspector 的“能力配置”列表接受 `AbilityDefinitionSO`。

每个列表项会在 `Awake` 创建一个独占的纯 C# Runtime：

```csharp
AbilityRuntime runtime = definition.CreateRuntime();
runtime.AbilityInit(ownerContext);
```

### 2. 配置顺序

列表就是生命周期调用顺序。生产数据的 Ability 在前，依赖数据的 Ability 在后。

推荐角色基础顺序：

```text
InputListenerAbilitySO
BetterHSMAbilitySO
Move / Jump / Combat 等业务 AbilitySO
```

如果 Move Ability 直接读取输入但不需要 BHSM，可以放在 Input 后；如果它还读取状态，放在 BHSM 后。

### 3. 生命周期

| GAS 回调 | Runtime 回调 | 用途 |
| --- | --- | --- |
| `Awake` | `AbilityInit(ownerContext)` | 获取/创建并缓存组件，注册 RuntimeData。 |
| `OnEnable` | `AbilityOnEnable()` | 恢复监听、清理禁用期间状态。 |
| `Start` | `AbilityStart()` | 依赖其他能力初始化完成后的启动工作。 |
| `Update` | `AbilityUpdate(deltaTime)` | 普通帧数据读取、状态决策、非物理逻辑。 |
| `FixedUpdate` | `AbilityFixedUpdate(fixedDeltaTime)` | 物理驱动、刚体或 CC 相关固定步逻辑。 |
| `LateUpdate` | `AbilityLateUpdate(deltaTime)` | 跟随、最终表现修正。 |
| `OnDisable` | `AbilityOnDisable()`，逆序 | 停止监听，清理短期状态。 |
| `OnDestroy` | `AbilityDispose()`，逆序 | 注销数据、解绑事件、释放对象。 |

`AbilityInit` 是一次性初始化；不要在 `AbilityUpdate` 中反复 `GetComponent`、重新查找 InputAction 或重新注册数据。

## AbilityOwnerContext：能力共享数据

每个 GAS 实体只有一个 `AbilityOwnerContext`。它保存 Owner、Transform 与运行时数据表：

```text
AbilityRuntimeDataType -> IAbilityRuntimeData
```

### 数据生产者

```csharp
_runtimeData = new CombatRuntimeData();
OwnerContext.Register(AbilityRuntimeDataType.Combat, _runtimeData);
```

### 数据消费者

```csharp
if (!OwnerContext.TryGet(
        AbilityRuntimeDataType.Input,
        out InputRuntimeData inputRuntimeData))
{
    return;
}
```

### 必须遵守的规则

- `AbilityRuntimeDataType` 是语义唯一键；同一个实体内同一键只能注册一份数据。
- 数据类型必须实现 `IAbilityRuntimeData.Reset()`。
- Runtime 销毁时使用同一个实例注销，避免误注销其他能力后来注册的数据。
- RuntimeData 是实体独占、可变的运行时数据；不要保存到 Ability SO。
- 读取其他能力数据，不要保存对其他 Ability Runtime 的引用。

当前 `AbilityRuntimeDataType` 已包含 `Input` 与 `BetterHSM`。新建公共 RuntimeData 前，应先增加枚举项，再生成 Ability 模板或手写 Runtime。

## 输入能力

### 配置

`InputListenerAbilitySO` 保存：

- 自动创建 `PlayerInput` 时使用的 `InputActionAsset`；
- 直接引用的移动 `InputActionReference`；
- `InputButton -> InputAction` 的按钮映射表。

配置资产位于：

```text
Assets/Settings/Ability Configurations/InputListenerAbility.asset
```

动作资产位于：

```text
Assets/Settings/Input Settings/Player.inputactions
```

`InputListenerAbilityRuntime` 会获取同对象上的 `PlayerInput`；不存在时按 SO 的动作资产创建并绑定。它每帧只读取初始化阶段缓存的 Action，并写入 `InputRuntimeData`。

### InputRuntimeData

| 成员 | 使用时机 |
| --- | --- |
| `Move` | 连续移动值；已归一化。 |
| `IsHeld(button)` | 持续状态，如蓄力、冲刺按住。 |
| `WasPressed(button)` | 判断未消费的按下边沿。 |
| `WasReleased(button)` | 判断未消费的松开边沿。 |
| `ConsumePressed(button)` | 真正拥有该输入的 Ability 在执行时消费。 |
| `ConsumeReleased(button)` | 同上，用于松开事件。 |
| `GetWorldMoveDirection(reference)` | 按参考 Transform 转成世界平面方向。 |

`InputButton` 当前只有：

```csharp
None = 0,
Jump = 1,
Sprint = 2,
```

需要新增通用按钮时，增加枚举值并在 Input SO 的映射表中配置对应 Action。每个按钮只配置一次。键盘键、鼠标键、手柄键或虚拟按键，只要业务需要明确的 Held/Pressed/Released 语义，都可以归为 `InputButton`；持续二维或一维模拟量应作为独立数据字段，不应伪装成按钮。

## BetterHSM 接入

### 三个上下文

| 名称 | 数量 | 职责 |
| --- | --- | --- |
| `AbilityOwnerContext` | 每个 GAS 实体一个 | GAS 能力共享数据与 Owner。 |
| `TStateContext` | 每个 BetterHSM 一个 | 状态专属访问门面；同一 HSM 的全部状态共享。 |
| `BetterHSMAbilityContext` | 每个 BHSM Ability 一个 | 包装强类型 HSM 与状态上下文，供 Runtime 驱动和外部只读。 |

状态本身不各自创建 Context。一个 `BetterHSM<PlayerStateContext>` 下的 Idle、Move、Attack 状态共享同一个 `PlayerStateContext`。

### 创建流程

`BetterHSMAbilityRuntime.AbilityInit` 会调用：

```csharp
BetterHSMAbilitySchemeCatalog.Create(
    configuration.EntityCategory,
    configuration.RegistrationId,
    ownerContext);
```

静态目录按顶层分类保存注册方案：

```text
Character / Enemy / NPC
    -> BetterHSMRegistrationId
        -> Func<AbilityOwnerContext, BetterHSMAbilityContext>
```

目录不支持运行时动态注册和反射扫描。这样能让每个角色的状态图注册点显式、可查、无启动时不确定性。

### 当前状态

当前 `BetterHSMRegistrationId` 尚无正式枚举项，`BetterHSMAbilitySchemeCatalog` 的分类字典也没有正式工厂。这是刻意保留的上层框架，等待实际角色状态图实现后再填入；在没有配置工厂前，不要把 `BetterHSMAbilitySO` 挂进可运行实体的 GAS 列表，否则初始化会抛出配置错误。

### 注册一个正式角色方案

完整模板见 [[BetterHSM 状态图模板]]。概念流程：

```text
1. 在 BetterHSMRegistrationId 增加正式角色 ID。
2. 编写该角色的 StateContext、State 类和静态 Registration 工厂。
3. 在 BetterHSMAbilitySchemeCatalog 对应分类字典中写入一行 ID -> 工厂映射。
4. 创建 BetterHSMAbilitySO，并在 Inspector 选择分类与 ID。
5. 将该 SO 放在实体 GAS 的输入能力之后。
```

## BHSM 如何读取玩家输入

状态不应直接 `GetComponent<PlayerInput>()`。推荐通过状态上下文包装读取：

```csharp
public bool HasMoveInput()
{
    return OwnerContext.TryGet(
        AbilityRuntimeDataType.Input,
        out InputRuntimeData inputRuntimeData) &&
        inputRuntimeData.Move.sqrMagnitude > 0.0001f;
}
```

状态的 `Interrupt()` 只调用 `Context.HasMoveInput()`。这样输入来源可替换为 PlayerInput、网络同步、AI、录像回放或测试注入，而状态图不需要重写。

外部 Ability 读取当前状态时：

```csharp
if (OwnerContext.TryGet(
        AbilityRuntimeDataType.BetterHSM,
        out BetterHSMAbilityRuntimeData hsmRuntimeData))
{
    Type currentStateType = hsmRuntimeData.AbilityContext.CurrentStateType;
}
```

只能读取状态和状态上下文；禁止从外部调用 HSM 的 `Update()`、`Dispose()`、`OnEnter()` 或 `OnExit()`。需要请求状态切换时，向共享 RuntimeData 写请求，或在初始化阶段注入外部 `Func<bool>` 条件，再让 BHSM 正常仲裁。
