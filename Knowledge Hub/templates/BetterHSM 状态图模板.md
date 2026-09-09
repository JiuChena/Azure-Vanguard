---
tags: [template, BetterHSM, state-machine, GAS]
created: 2026-09-09
updated: 2026-09-09
---

# BetterHSM 状态图模板

## 何时使用 BetterHSM

适合：角色、敌人或 NPC 存在互斥主状态，且状态间转换需要优先级仲裁，例如 Idle、Move、Attack、Hit、Dead。

不适合：

- 多个可并行的标记，如“中毒”“装备了护盾”；应放 RuntimeData 或独立 Ability。
- 短期一次性指令；应写成 Ability 请求或事件。
- 只有单一 boolean 的开关流程；不必建立状态图。

## 状态机规则

1. 每个实体都创建独立的 `BetterHSM<TContext>`、`TContext` 和 State 实例。
2. 状态类型可复用，状态实例不可跨实体共享。
3. 一个 HSM 的所有状态共享同一个 `TContext`。
4. `Interrupt()` 只判断是否应进入自己，必须无副作用。
5. 所有可用转换边都会评估；优先级数值越大越先切换。
6. 同优先级使用先注册的边，保证结果稳定。
7. 每帧最多切换一次；切换帧执行旧状态 `OnExit` 与新状态 `OnEnter`，不执行新状态 `OnUpdate`。
8. `Build` 后状态图封口；不能在运行时临时添加或删除边。

## 需要创建的文件

以角色 `CH1001` 为例，放在业务/角色目录，而不是 BetterHSM 核心目录：

```text
Assets/Scripts/CSharp/Gameplay/Characters/CH1001/StateMachine/
    CH1001StateContext.cs
    CH1001IdleState.cs
    CH1001MoveState.cs
    CH1001AttackState.cs
    CH1001BetterHSMRegistration.cs
```

状态图属于角色业务层。`Assets/Scripts/CSharp/Core/Gameplay/BetterHSM/` 只能放状态机通用机制，不能放 CH1001、攻击、跳跃或战斗业务。

## 1. 状态上下文

状态上下文为所有状态提供同一实体的数据访问门面。它应持有 `AbilityOwnerContext`，并把输入、组件、运行时数据的读取集中成有语义的方法。

```csharp
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Abilities.Input;

namespace Game.Characters.CH1001
{
    public sealed class CH1001StateContext
    {
        public CH1001StateContext(AbilityOwnerContext ownerContext)
        {
            OwnerContext = ownerContext;
        }

        public AbilityOwnerContext OwnerContext { get; }

        public bool HasMoveInput()
        {
            return OwnerContext.TryGet(
                AbilityRuntimeDataType.Input,
                out InputRuntimeData inputRuntimeData) &&
                inputRuntimeData.Move.sqrMagnitude > 0.0001f;
        }

        public bool HasJumpRequest()
        {
            return OwnerContext.TryGet(
                AbilityRuntimeDataType.Input,
                out InputRuntimeData inputRuntimeData) &&
                inputRuntimeData.WasPressed(InputButton.Jump);
        }

        public bool ConsumeJumpRequest()
        {
            return OwnerContext.TryGet(
                AbilityRuntimeDataType.Input,
                out InputRuntimeData inputRuntimeData) &&
                inputRuntimeData.ConsumePressed(InputButton.Jump);
        }
    }
}
```

这里的 `HasJumpRequest` 只读；真正的 `ConsumeJumpRequest` 由 JumpState 的 `OnEnter` 或跳跃 Ability 调用。

## 2. 状态类

```csharp
using Core.Gear;

namespace Game.Characters.CH1001
{
    public sealed class CH1001MoveState : StateBase<CH1001StateContext>
    {
        public override bool Interrupt()
        {
            return Context.HasMoveInput();
        }

        public override void OnEnter()
        {
            // 切入移动表现；不要在这里创建另一套状态机。
        }

        public override void OnUpdate()
        {
            // 当前仍处于移动状态且本帧未切换时执行。
        }

        public override void OnExit()
        {
            // 清理本轮移动临时状态。
        }
    }
}
```

`StateBase<TContext>` 已提供 `Context`。不需要也不允许自行 new Context。

## 3. 注册工厂

注册方案是一个静态工厂，签名固定为：

```csharp
Func<AbilityOwnerContext, BetterHSMAbilityContext>
```

示例：

```csharp
using Core.Gear;
using Framework.Gameplay.Abilities;

namespace Game.Characters.CH1001
{
    public static class CH1001BetterHSMRegistration
    {
        public static BetterHSMAbilityContext Create(AbilityOwnerContext ownerContext)
        {
            var stateContext = new CH1001StateContext(ownerContext);
            var betterHsm = new BetterHSM<CH1001StateContext>(stateContext);

            var idleState = new CH1001IdleState();
            var moveState = new CH1001MoveState();
            var attackState = new CH1001AttackState();

            StateGraphBuilder<CH1001StateContext> graph = betterHsm.BeginBuild();
            graph.AddState(idleState)
                .AddState(moveState)
                .AddState(attackState);

            graph.From(idleState)
                .To(moveState, moveState.Interrupt, priority: 10)
                .To(attackState, attackState.Interrupt, priority: 100);

            graph.From(moveState)
                .To(idleState, idleState.Interrupt, priority: 10)
                .To(attackState, attackState.Interrupt, priority: 100);

            graph.From(attackState)
                .To(idleState, idleState.Interrupt, priority: 10);

            graph.Build(idleState);
            return new BetterHSMAbilityContext<CH1001StateContext>(stateContext, betterHsm);
        }
    }
}
```

`Build(idleState)` 显式指定默认状态，和 `AddState` 顺序无关。

## 4. 注册到 GAS 目录

### 枚举

在 `BetterHSMRegistrationId` 增加：

```csharp
CH1001,
```

### 静态目录

在 `BetterHSMAbilitySchemeCatalog` 的对应顶层分类里增加一行：

```csharp
{
    BetterHSMRegistrationId.CH1001,
    CH1001BetterHSMRegistration.Create
},
```

角色放 `Character`，敌人放 `Enemy`，NPC 放 `NPC`。不要动态 Register，不要反射扫描，也不要在角色 Awake 中偷偷改静态字典。

### SO 配置

创建 `BetterHSMAbilitySO` 后选择：

```text
Entity Category = Character
Registration Id = CH1001
```

把该 SO 挂到 CH1001 的 GAS 列表，且在 Input Ability 后。

## 外部中断条件

转换条件是 `Func<bool>`，可以来自目标状态、外部服务、静态方法或闭包。下面是合法的外部注入：

```csharp
graph.From(moveState)
    .To(hitState, damageReceiver.HasPendingHit, priority: 500);
```

要求：

- 函数必须无副作用。
- 函数必须能在每帧被安全调用。
- 不能消费按钮、清空队列、切换状态或销毁对象。
- 若它读共享数据，生产者 Ability 必须位于 BHSM Ability 前。

## 不要这样做

```csharp
// 错误：Interrupt 中消费输入，可能影响后续边的判断。
public override bool Interrupt()
{
    return Context.ConsumeJumpRequest();
}

// 错误：状态主动手动调用另一个状态生命周期。
otherState.OnEnter();

// 错误：运行时修改已封口的图。
graph.From(idleState).To(moveState, moveState.Interrupt, 10);
```

正确做法是：在 `Interrupt` 里 `WasPressed`，进入目标状态后再 `ConsumePressed`；所有边均在注册工厂中一次性声明。

## 状态图完成检查

1. 每个实体创建独立 Context、HSM、状态实例。
2. `Interrupt` 无副作用。
3. 每条边有明确优先级，业务最强制的状态（死亡、硬直等）优先级更高。
4. 没有任何状态直接操作另一个状态或 HSM 内部字段。
5. `OnInitialize` 只做一次长期依赖缓存；`OnEnter/Exit` 只管理本轮状态；`OnDispose` 释放长期资源。
6. 已配置枚举、目录映射与 BetterHSM SO。
7. GAS 列表顺序能保证本帧数据先写后读。
