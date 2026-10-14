---
tags: [project, template, betterhsm, state-machine]
created: 2026-10-14
updated: 2026-10-14
---

# 模板-BHSM状态

> BetterHSM 状态类 + Registration 工厂骨架。流程见 [[02-流程SOP/SOP-新加状态机方案]]；分层语义与硬约束见 [[04-模块/GAS-能力与状态机]]；先例：`GAS/BetterHSM/Test/CH0221/CH0221TestHierarchicalRegistration.cs`。
> **命名空间 `Core.Gear`**——漏 using 高发（BetterHsmSmoke 曾 CS0246 全红）。

## 状态类骨架

```csharp
using Core.Gear;   // BetterHSM/StateBase/StateGraphBuilder 全在此

/// <summary>叶子状态基类（项目内可再抽象公共字段）。</summary>
public abstract class MyStateBase : StateBase<MyContext>
{
    protected MyStateBase(string name) : base(name) { }
}

// ⚠️ 硬约束：同一 HSM 内每个节点必须是独立 C# 类（框架按状态类型唯一注册）。
//    两个节点不能共用一个状态类——重复注册抛 InvalidOperationException。

/// <summary>复合状态（结构节点）：无 OnUpdate、不可为边目标/初始状态。</summary>
public sealed class SLocomotion : MyStateBase
{
    public SLocomotion() : base("Locomotion") { }
}

/// <summary>叶子状态：带行为键（UnitConfig 行为表对齐）。</summary>
public sealed class SIdle : MyStateBase
{
    public const string BehaviorKey = "Idle";           // 对齐 UnitConfig.behaviorClips 键
    public SIdle() : base("Idle") { }

    public override void OnEnter(MyContext ctx) { /* Play(BehaviorKey) 由桥接层做 */ }
    public override void OnUpdate(MyContext ctx) { }
    public override void OnExit(MyContext ctx) { }
    public override bool Interrupt(MyContext ctx) => false;   // virtual 默认 false，按需覆写
}

/// <summary>状态上下文：每帧数据与请求注入。</summary>
public class MyContext
{
    public bool HasMoveRequest;    // 数据变更处调对应状态 MarkDirty()（若该层启用门控）
}
```

## Registration 工厂骨架

```csharp
public static class MyHierarchicalRegistration
{
    public static BetterHSM<MyContext> Create()
    {
        var betterHsm = new BetterHSM<MyContext>();
        StateGraphBuilder<MyContext> graph = betterHsm.BeginBuild();

        // 状态实例（每节点独立类）
        var locomotion = new SLocomotion();
        var combat     = new SCombat();
        var idle  = new SIdle();
        var move  = new SMove();
        var attack = new SAttack();

        // 1. 根先注册
        graph.AddState(locomotion).AddState(combat);
        // 2. 父子（父必须已注册、child 必须未注册）
        graph.AddChild(locomotion, idle).AddChild(locomotion, move);
        graph.AddChild(combat, attack);
        // 3. 边：From(源).To(目标叶子, 谓词, priority)——priority 只在同层竞争
        graph.From(idle).To(move, move.Interrupt, priority: 10);           // 叶子层边
        graph.From(locomotion).To(idle, idle.Interrupt, priority: 10);     // 父层边（先于叶子评估）
        graph.From(locomotion).To(attack, ctx => ctx.HasCombatRequest, priority: 50); // 跨树边
        // 4.（可选）门控：启用后仅 MarkDirty() 之后的帧才评估该层中断
        graph.EnableInterruptGating(locomotion);
        // 5. 构建：初始状态必须是叶子
        graph.Build(idle);

        return betterHsm;
    }
}
```

## 注册到目录（两处）

```csharp
// 1) BetterHSMRegistrationId.cs —— 枚举加值
public enum BetterHSMRegistrationId { /* ... */ CH0221TestHierarchical, MyNewScheme }

// 2) BetterHSMAbilitySchemeCatalog.cs —— 对应 EntityCategory 字典加映射
{ BetterHSMRegistrationId.MyNewScheme, MyHierarchicalRegistration.Create }
```

## 检查单

- [ ] `using Core.Gear`
- [ ] 每节点独立类（类型唯一注册约束）
- [ ] 复合状态：无 OnUpdate / 不做边目标 / 不做初始状态
- [ ] 边目标全是叶子；初始状态是叶子
- [ ] priority 只在同层语义下分配；父层边优先于子层评估是有意设计才写
- [ ] 启用门控的层：数据变更处有对应 `MarkDirty()` 调用
- [ ] 叶子 BehaviorKey 与 UnitConfig.behaviorClips 键对齐
- [ ] 枚举 + SchemeCatalog 两处都登记
- [ ] smoke 断言覆盖：同根 LCA / 父层切子 / 门控 / 跨树级联 / 负例（先例 BetterHsmSmoke.cs A-G）
