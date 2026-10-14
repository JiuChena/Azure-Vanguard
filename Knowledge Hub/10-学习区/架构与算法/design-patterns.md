---
tags:
  - design-patterns
  - architecture
created: 2026-06-20
---

# 设计模式与架构

## SOLID 原则

SOLID 是面向对象设计的五大基本原则：

1. **S - 单一职责原则 (Single Responsibility Principle)**
   一个类应该只负责一项职责（只做一件事）。避免一个类承担过多不相关的功能，降低耦合，提高可维护性。

2. **O - 开闭原则 (Open/Closed Principle)**
   对扩展开放，对修改关闭。应通过扩展（继承、接口实现）来增加新功能，而不是修改已有代码。

3. **L - 里氏替换原则 (Liskov Substitution Principle)**
   子类应该能够替换父类而不引起程序行为异常。即派生类必须能完全替代基类。

4. **I - 接口隔离原则 (Interface Segregation Principle)**
   不应强迫客户端依赖其不使用的接口。应当拆分臃肿的接口为更小、更专一的接口。

5. **D - 依赖倒置原则 (Dependency Inversion Principle)**
   高层模块不应依赖低层模块，两者都应依赖抽象。抽象不应依赖细节，细节应依赖抽象。

> 遵循 SOLID 原则可以降低代码耦合度，提高可测试性、可维护性和可扩展性。

---

## 单例模式 (Singleton)

确保一个类只有一个实例，并提供一个全局访问点。

### 使用 Lazy\<T\> 实现线程安全单例

```csharp
public sealed class PerfectSingleton
{
    // 使用 Lazy<T> 保证线程安全，且延迟初始化
    private static readonly Lazy<PerfectSingleton> _instance =
        new Lazy<PerfectSingleton>(() => new PerfectSingleton(),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    public static PerfectSingleton Instance => _instance.Value;

    // 私有构造函数，防止外部实例化
    private PerfectSingleton()
    {
        Console.WriteLine("PerfectSingleton created");
        // 防止反射创建多个实例
        if (_instance.IsValueCreated)
            throw new InvalidOperationException("单例已存在，不允许重复创建");
    }
}
```

### Unity 中的单例

- 常用于 GameManager、AudioManager、UIManager 等全局管理器
- 使用 `DontDestroyOnLoad` 保持跨场景生命周期
- 注意场景切换时的重复单例问题

---

## 工厂模式 (Factory)

用于创建对象的模式，将对象创建逻辑与业务逻辑分离，方便切换到不同的子类实现。

### 适用场景

- 对象的创建过程复杂，需要集中管理
- 需要根据条件返回不同的子类实例
- 隔离客户端代码与具体类的依赖

---

## 观察者模式 (Observer)

定义对象间一对多的依赖关系，当一个对象状态发生变化时，所有依赖它的对象都会得到通知并自动更新。

### C# 中的实现方式

- **C# event 关键字**：基于委托的事件机制
- **UnityEvent**：可在 Inspector 中配置的 Unity 事件
- **Action / Func 委托**：轻量级回调机制

### 发布-订阅模式 (Publisher-Subscriber)

```
发布者 (Publisher) → 事件 → 订阅者A (Subscriber)
                         → 订阅者B (Subscriber)
                         → 订阅者C (Subscriber)
```

- 发布者与订阅者解耦
- 发布者不需要知道有哪些订阅者
- 订阅者只关心事件本身

### 常见使用场景

1. **UI 更新**: 数据变化时自动刷新界面显示（生命值、分数等）
2. **成就系统**: 游戏事件触发后，成就系统检测条件是否满足
3. **音效系统**: 各类事件触发对应音效的播放
4. **任务系统**: 监听玩家行为，判断任务进度
5. **物理回调**: Unity 物理碰撞的 `OnTriggerEnter` / `OnCollisionEnter`
6. **输入处理**: 输入事件分发给各个响应系统

### 委托链示例

```csharp
// 定义委托
delegate void AttackDelegate(int damage);

// 注册多个方法
AttackDelegate attackEffects = MeleeAttack;
attackEffects += PlaySound;
attackEffects += SpawnVFX;

// 调用委托链
attackEffects?.Invoke(50);
```

---

## 命令模式 (Command)

将请求封装为对象，从而可以用不同的请求对客户端进行参数化，支持请求的排队、记录日志、撤销/重做等操作。

### Unity 中的典型应用

- 技能系统：将技能封装为 Command
- 输入处理：将按键映射到命令
- 回放系统：记录和重放玩家操作
- 撤销系统：记录操作历史

---

## MVC / MVP / MVVM

### MVC (Model-View-Controller)

| 组件 | 职责 |
|------|------|
| Model | 数据与业务逻辑 |
| View | 界面展示 |
| Controller | 处理用户输入，协调 Model 和 View |

- View 读取 Model 数据展示
- Controller 接收用户输入更新 Model
- Model 通过观察者模式通知 View 更新

### MVP (Model-View-Presenter)

与 MVC 类似，但 Presenter 完全隔离 View 和 Model，View 不直接访问 Model。

### Facade 外观模式

在大型系统中，使用 Facade 提供统一的简化接口，隐藏子系统复杂性。常与 MVC 结合使用，降低模块间耦合。

---

## ECS / DOTS 概述

### ECS 架构

- **Entity**: 只是一个 ID，不包含任何数据或行为
- **Component**: 纯数据容器，不包含逻辑
- **System**: 处理拥有特定组件组合的实体

### 与传统 OOP 的区别

| 维度 | 传统 OOP (GameObject-Component) | ECS (DOTS) |
|------|------|------|
| 数据组织 | 数据分散在对象中 | 数据连续存储在内存中 |
| 处理方式 | 每个对象独立 Update | System 批量处理符合条件的所有实体 |
| 内存访问 | 随机访问，缓存不友好 | 连续内存，高缓存命中率 |
| 性能 | 适合中小规模 | 适合大规模实体 |

### Unity DOTS 组件

- **Entities**: 核心包，提供 Entity、World、Archetype
- **Burst Compiler**: 将 C# 代码编译为高度优化的原生代码
- **Jobs System**: 多线程并行处理
- **Collections**: 提供 NativeArray、NativeList 等高性能容器

---

## 依赖注入 (Dependency Injection)

依赖注入是一种实现控制反转（IoC）的技术，将依赖的创建和管理从类内部移到外部。

### 优势

- 降低模块间的耦合
- 提高代码可测试性（方便 Mock）
- 集中管理对象生命周期

### Unity 中的实现

- 使用第三方框架如 Zenject / VContainer
- 通过构造函数注入或属性注入
- 在游戏的 Composition Root 处完成依赖绑定

---

## 游戏开发中常见设计模式总结

| 模式 | Unity 典型场景 |
|------|---------------|
| Singleton | GameManager、AudioManager |
| Observer | 事件系统、UI 响应 |
| Factory | 敌人生成、技能创建 |
| State | 角色状态机（Idle/Move/Attack） |
| Object Pooling | 子弹、特效复用 |
| Command | 技能序列、撤销/重做 |
| MVC/Facade | 大型系统架构、UI 框架 |

---

## 工厂模式深入

工厂模式的核心思想是将"创建什么对象"与"怎么用对象"解耦。根据抽象程度分为三种：

| 变体 | 特点 | 适用场景 |
|------|------|----------|
| 简单工厂 | 一个工厂类根据参数创建不同产品 | 产品类型少，逻辑简单 |
| 工厂方法 | 每个产品对应一个工厂子类 | 需要扩展新类型而不改旧代码 |
| 抽象工厂 | 创建一组相关产品（产品族） | 跨平台 UI、不同主题成套切换 |

面试要点：工厂方法符合开闭原则（新增子类即可，不修改已有代码），简单工厂不符合（每增加新产品都要修改 switch）。Unity 中常用于敌人生成、特效实例化、不同平台 SDK 初始化。

```csharp
// === 工厂方法 ===
public interface IEnemyFactory { Enemy Create(); }
public class MeleeFactory : IEnemyFactory
{
    private GameObject _prefab;
    public MeleeFactory(GameObject prefab) => _prefab = prefab;
    public Enemy Create() => Object.Instantiate(_prefab).GetComponent<Enemy>();
}
public class RangedFactory : IEnemyFactory
{
    public Enemy Create()
    {
        var go = Object.Instantiate(_prefab);
        go.AddComponent<BowWeapon>(); // 额外初始化
        return go.GetComponent<Enemy>();
    }
}
// 客户端只依赖接口，切换工厂即可切换产出
IEnemyFactory factory = new MeleeFactory(meleePrefab);
var enemy = factory.Create();
```

```csharp
// === 抽象工厂 ===
public interface IUIFactory { IButton CreateButton(); IPanel CreatePanel(); }
public class WindowsUIFactory : IUIFactory { /* 返回 Windows 风格控件 */ }
public class MobileUIFactory : IUIFactory { /* 返回触控友好控件 */ }
```

---

## 命令模式在 Unity 中

命令模式把"执行者"和"请求者"彻底分开——调用方只持有一个命令对象并调用 `Execute()`，完全不知道谁在执行、怎么执行。

Unity 中的两大杀器级应用：
1. **技能系统**：技能 = 命令对象。角色按下按键时创建技能命令，技能序列/连招就是命令队列。每个命令自带冷却、消耗检查、执行和回滚。
2. **撤销系统**：每个操作实现 `Execute()` 和 `Undo()`，操作历史就是命令栈。编辑器工具、回合制游戏的回退都依赖此模式。

面试要点：与策略模式的区别——策略侧重算法互换（不同算法做同一件事），命令侧重请求封装（请求者和执行者解耦，支持撤销/排队/日志）。

```csharp
// 可撤销移动命令
public interface ICommand
{
    void Execute();
    void Undo();
}

public class MoveCommand : ICommand
{
    private Transform _target;
    private Vector3 _delta;
    public MoveCommand(Transform target, Vector3 delta)
    { _target = target; _delta = delta; }

    public void Execute() => _target.position += _delta;
    public void Undo() => _target.position -= _delta;
}

// 命令栈实现撤销
var history = new Stack<ICommand>();
var cmd = new MoveCommand(player, Vector3.forward * 5);
cmd.Execute(); history.Push(cmd);
// 撤销
if (history.Count > 0) history.Pop().Undo();
```

---

## MVP vs MVVM

两者都解决了 View 和 Model 的直接耦合问题，但定位不同：

| 维度 | MVP | MVVM |
|------|-----|------|
| 核心角色 | Presenter 持有 View 引用，主动调用 View 更新 | ViewModel 不持有 View 引用，通过数据绑定通知 |
| View 依赖 | View 定义接口 IPanelView，Presenter 依赖该接口 | View 通过绑定器自动订阅 ViewModel 属性变化 |
| 数据流 | Presenter → View.SetData() 单向推送 | View ↔ ViewModel 双向绑定 |
| 适合场景 | Unity UGUI（无原生绑定），逻辑复杂需精确控制 | WPF/前端框架，IUIToolkit 有 USS 数据绑定 |

Unity 中的选择：UGUI 项目首选 **MVP**。View 暴露接口（如 `void UpdateHP(int hp)`），Presenter 持有引用直接调用，简单可靠。MVVM 需要额外的绑定层（手动写 PropertyChanged 或用 UniRx），在 UGUI 下引入额外复杂度，仅在需要双向绑定的复杂表单场景有优势。

```csharp
// === MVP 在 Unity UGUI 中的应用 ===
// 1. View 接口
public interface IShopView
{
    void ShowItems(List<ItemData> items);
    void ShowWallet(int gold, int diamond);
}

// 2. View 实现 (挂载在 UI Prefab 上)
public class ShopPanel : MonoBehaviour, IShopView
{
    [SerializeField] private Transform _itemContainer;
    [SerializeField] private Text _goldText;
    public void ShowItems(List<ItemData> items) { /* 填充列表 */ }
    public void ShowWallet(int g, int d) { _goldText.text = g.ToString(); }
}

// 3. Presenter——持有 View 引用，协调 Model
public class ShopPresenter
{
    private IShopView _view;
    private ShopModel _model;
    public ShopPresenter(IShopView view, ShopModel model)
    { _view = view; _model = model; }

    public void OnOpen()
    {
        _view.ShowItems(_model.Items);
        _view.ShowWallet(_model.Gold, _model.Diamond);
    }
    public void OnBuy(int itemId)
    {
        if (_model.TryBuy(itemId))
            _view.ShowWallet(_model.Gold, _model.Diamond);
    }
}
```

---

## DOTS 实战案例

**DOTS 是什么**：Unity 的高性能架构组合包（Data-Oriented Technology Stack），包含 ECS（Entity Component System）+ Burst Compiler + Job System + Mathematics + Collections。核心理念是"数据驱动设计"——把数据按内存连续排列，用多线程批量处理，榨干 CPU 缓存和 SIMD。

**什么时候用 DOTS**：
- 同屏实体数量 > 1000（弹幕、RTS 小兵、群聚敌人）
- 需要多线程并行计算（寻路、物理碰撞、AI 行为）
- 移动端发热严重，需要降功耗

**什么时候不用 DOTS**：
- 常规 UI、对话系统、剧情管理——GameObject 足够，写 DOTS 反而耗时
- 团队不熟悉，排期紧张——DOTS 调试工具链不如 GameObject 成熟
- 需要复杂组件交互——ECS 的 System 间通信靠数据传递，不像 GameObject 的 `GetComponent` 直连

**与 GameObject 的关系**：不是取代，是共存。主逻辑可用 GameObject 做架构，性能瓶颈部分用 DOTS 子世界（SubScene）处理，通过 Entity 的 `ICleanupComponent` 或事件桥接。

```csharp
// DOTS 移动 System
using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;

public struct MoveSpeed : IComponentData { public float Value; }

[BurstCompile] // Burst 编译器加速
public partial struct MoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        // 批量处理所有拥有 LocalTransform + MoveSpeed 的 Entity
        foreach (var (transform, speed) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())
        {
            transform.ValueRW.Position +=
                transform.ValueRW.Forward() * speed.ValueRO.Value * dt;
        }
    }
}
```

---

## 依赖注入：Zenject / VContainer

依赖注入（DI）的核心："依赖不是自己 new 的，而是外部传进来的"。Unity 中两个主流框架：

| 框架 | 特点 | 适合 |
|------|------|------|
| Zenject / Extenject | 功能最全，支持场景绑定、MonoBehaviour 注入、SignalBus | 大型项目、完整 MVC/MVP 架构 |
| VContainer | 日本团队维护，基于 Roslyn 源码生成，GC 压力小，纯 C# 写法 | 重视性能、较新项目、不喜欢 XML/Inspector 绑定 |

**基本用法三步骤：**
1. **注册**：在 GameLifetimeScope 中 `Container.Bind<IXXX>().To<XXXImpl>().AsSingle()`
2. **注入**：通过构造函数参数或 `[Inject]` 标签
3. **解析**：在 EntryPoint 中用 `Container.Resolve<T>()`，或直接由容器自动构造

面试要点：DI 让代码可测试——单元测试时能注入 Mock 依赖；DI 容器管理生命周期（单例/瞬态/作用域），避免到处写 `FindObjectOfType`。

```csharp
// === VContainer 示例 ===
// 服务接口与实现
public interface IAudioService { void PlayBGM(string clipName); }
public class AudioService : IAudioService
{
    public void PlayBGM(string clipName) => Debug.Log($"播放: {clipName}");
}

// 消费者——通过构造函数注入
public class GameController
{
    private IAudioService _audio;
    // VContainer 自动解析参数
    public GameController(IAudioService audio) { _audio = audio; }
    public void Start() => _audio.PlayBGM("title_bgm");
}

// 注册（在 LifetimeScope 中）
public class GameLifetimeScope : VContainer.Unity.LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IAudioService, AudioService>(Lifetime.Singleton);
        builder.Register<GameController>(Lifetime.Singleton);
    }
}
// GameController 由容器自动构造，无需手动 new 或 FindObjectOfType
```

---

## 状态模式与 FSM

状态模式解决的核心问题：**消除巨型 switch-case / if-else 分支**。不用状态模式时，角色的 Idle / Move / Attack / Death 逻辑全部堆在 `Update()` 里，每个行为都要嵌套条件判断，加一个新状态就要改动这个中心文件——违反了开闭原则。

**状态模式的本质**：将每个状态独立为一个类，都实现 `IState` 接口（`Enter / Update / Exit`）。状态机本身只维护当前状态引用，所有行为委托给当前状态对象处理。

**为什么不用 switch-case**：
- switch-case 每加一个状态就改一次核心文件，状态多了难以维护
- 不同状态的变量（比如攻击的 combo 计数）无法隔离，全堆在角色类里
- 状态转换逻辑散落在 case 分支中，难以追踪和调试
- 面试常问"状态模式和策略模式的区别"——状态模式状态之间互相知道并驱动转换；策略模式各个策略互相不知道，由外部选择

```csharp
// 状态接口
public interface IState
{
    void Enter();
    void Update();
    void Exit();
}

// 空闲状态
public class IdleState : IState
{
    private PlayerController _player;
    public IdleState(PlayerController p) => _player = p;

    public void Enter() => _player.animator.Play("Idle");
    public void Update()
    {
        if (_player.HasInput) _player.ChangeState(new MoveState(_player));
    }
    public void Exit() { }
}

// 状态机——只持有当前状态
public class StateMachine
{
    private IState _current;
    public void ChangeState(IState newState)
    {
        _current?.Exit();
        _current = newState;
        _current.Enter();
    }
    public void Update() => _current?.Update();
}

// 相比之下，switch-case 的坏味道：
// void Update() {
//     switch(state) {
//         case Idle: if (hasInput) ... break;
//         case Move: move logic;  if (dead) ... break;
//         case Attack: combo logic; ... break;
//     }
// } // 每次加状态都要改这里，违反开闭原则
```

---

---

### 对象池模式

```csharp
public class ObjectPool<T> where T : Component {
    private Queue<T> _pool = new Queue<T>();
    private T _prefab;
    
    public ObjectPool(T prefab, int prewarm) {
        _prefab = prefab;
        for (int i = 0; i < prewarm; i++) 
            Return(Instantiate(prefab));
    }
    
    public T Get() {
        T obj = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_prefab);
        obj.gameObject.SetActive(true);
        return obj;
    }
    
    public void Return(T obj) {
        obj.gameObject.SetActive(false);
        _pool.Enqueue(obj);
    }
}
```
**要点**：预热避免运行时分配 | Queue 不是 List | 考虑上限防内存膨胀

### MVC/MVP 在 Unity 中的实践

| | MVC | MVP |
|---|-----|-----|
| Unity 适用 | 较少直接用 | **UGUI 推荐** |
| Model | 数据层（ScriptableObject/Json） | 同左 |
| View | UI 显示 | UI 显示（Presenter 推送数据） |
| Controller/Presenter | 处理输入，更新 Model 和 View | View 通过接口回调→Presenter 更新 Model |

**实战**：Model=SO配置+PlayerData | View=PanelBase子类 | Presenter=处理按钮点击+调用 Model+刷新 View

### 策略 vs 命令 vs 状态

| | 策略 | 命令 | 状态 |
|---|------|------|------|
| 核心 | 算法可互换 | 操作封装成对象 | 不同状态下不同行为 |
| 关注点 | **做什么** | **做什么+可撤销** | **在不同状态下怎么做** |
| Unity 例子 | 不同 AI 寻路算法可互换 | 技能释放+撤销、操作队列 | Animator 状态机、AI 状态 |
