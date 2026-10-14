---
tags:
  - csharp
  - programming
  - fundamentals
created: 2026-06-20
updated: 2026-06-20
---

# C# 基础与进阶

## 概述

C#（C Sharp）是由 Microsoft 开发的面向对象编程语言，运行于 .NET Framework / .NET Core 平台之上。具备强类型、命令式、声明式、面向组件编程等特性。名称灵感来源于音乐符号，"#" 表示音高需高出半音。Anders Hejlsberg 是 C# 语言开发的关键贡献者，1999 年组建团队开发（当时代号 "Cool"），2000 年在 .NET 开发者大会上正式发布。

C# 的设计原则：简单、现代、通用、面向对象；支持软件工程原则；是构建托管和嵌入式系统应用程序的理想选择。

---

## 一、C++ 与 C# 的核心区别

### 1.1 编译与运行

| 维度 | C++ | C# |
|------|-----|-----|
| 编译产物 | 编译为机器代码（Native Code） | 编译为 IL（CIL/MSIL），由 CLR 在运行时 JIT 编译 |
| 内存管理 | 需要手动管理内存（new/delete） | 自动内存管理（GC 垃圾回收） |
| 运行环境 | 直接运行于操作系统 | 运行于 CLR（公共语言运行时）之上 |
| 跨平台 | 可在多种平台运行（Unix、Linux 等） | 传统上限 Windows，.NET Core 后跨平台 |

### 1.2 语言特性对比

| 维度 | C++ | C# |
|------|-----|-----|
| 语言类型 | 低级/中级语言，底层控制力强 | 高级语言，类型安全 |
| 垃圾回收 | 不支持 GC（需手动管理） | 支持 GC（自动垃圾回收） |
| 多重继承 | 支持多重继承 | 不支持（用接口替代） |
| foreach 语句 | 旧版本不支持（自 C++11 支持） | 原生支持 foreach |
| 指针使用 | 可在任意位置使用指针 | 仅在不安全模式（unsafe）下使用 |
| switch 语句 | 变量不能是字符串 | 支持字符串匹配 |
| 编译后代码 | 直接生成机器代码 | 编译为中间语言（CIL）再 JIT 编译 |

### 1.3 C++ 简述

C++ 是一种计算机编程语言，融合了 C 语言和 Simula67（第一个面向对象语言）的特性。由 AT&T 贝尔实验室的 Bjarne Stroustrup 开发，目标是创建支持面向对象特性同时保留 C 语言高效性的语言。

### 1.4 C# 历史

- 1999 年 Anders Hejlsberg 组建设计团队，项目代号 "Cool"
- 2000 年在 .NET 开发者大会上正式发布
- 后更名为 C#

---

## 二、值类型与引用类型

### 2.1 类型体系

C# 类型系统分为两大类：

```
C# Data Types
├── 值类型 (Value Types)
│   ├── 整数类型: byte, short, ushort, int, uint, long, ulong
│   ├── 浮点类型: float, double, decimal
│   ├── bool (true / false)
│   ├── char (16 位 Unicode 字符)
│   ├── struct (结构体)
│   └── enum (枚举)
│
└── 引用类型 (Reference Types)
    ├── class (类)
    ├── interface (接口)
    ├── array (数组)
    ├── delegate (委托)
    └── string (字符串)
```

### 2.2 数值类型范围一览

| 类型 | 大小 | 范围 |
|------|------|------|
| `byte` | 8 位 | 0 ~ 255 |
| `short` | 16 位 | -32,768 ~ 32,767 |
| `ushort` | 16 位 | 0 ~ 65,535 |
| `int` | 32 位 | -2,147,483,648 ~ 2,147,483,647 |
| `uint` | 32 位 | 0 ~ 4,294,967,295 |
| `long` | 64 位 | -9,223,372,036,854,775,808 ~ 9,223,372,036,854,775,807 |
| `ulong` | 64 位 | 0 ~ 18,446,744,073,709,551,615 |
| `float` | 32 位 | 单精度浮点 |
| `double` | 64 位 | 双精度浮点 |
| `decimal` | 128 位 | 高精度十进制 |

### 2.3 核心区别

| 维度 | 值类型 (Value Type) | 引用类型 (Reference Type) |
|------|---------------------|--------------------------|
| 基类 | `System.ValueType` → `System.Object` | 直接继承 `System.Object` |
| 内存分配 | 分配在栈上（局部变量时），结构体成员嵌入其容器 | 对象分配在托管堆上，栈上仅存储引用（指针） |
| 赋值行为 | 复制整个值，产生独立副本 | 复制引用，指向同一对象 |
| 默认值 | 不能为 null（Nullable 除外） | 可以为 null |
| 生命周期 | 离开作用域自动释放 | 由 GC（垃圾回收器）管理 |
| 继承 | 不能继承（struct 隐式 sealed） | 支持继承（class） |

### 2.4 内存分布示意

```
值类型（栈上）:
┌──────────────┐
│ int a = 42   │  ← 值直接存储在栈上
│ int b = a    │  ← 复制值，b 独立于 a
└──────────────┘

引用类型（堆上）:
Stack                    Heap
┌──────────┐            ┌─────────────────┐
│ obj ref   │ ────────→ │ MyClass 实例     │
└──────────┘            │ { Value = 42 }   │
                        └─────────────────┘
```

### 2.5 基类关系

```
System.Object
├── System.ValueType（抽象类，值类型的基类）
│   ├── System.Int32 (int)
│   ├── System.Int64 (long)
│   ├── System.Single (float)
│   ├── System.Double (double)
│   ├── System.Char (char)
│   ├── System.Boolean (bool)
│   └── 自定义 struct
│
└── 引用类型（直接继承 System.Object）
    ├── System.String (string)
    ├── 自定义 class
    ├── 自定义 interface
    ├── System.Array
    └── System.Delegate
```

> `System.ValueType` 重写了 `System.Object` 的虚方法（如 `ToString()`、`Equals()`），提供基于值的相等性比较。

---

## 三、装箱（Boxing）与拆箱（Unboxing）

### 3.1 什么是装箱

装箱是将**值类型**转换为**引用类型**（`object` 或 `System.ValueType`）的过程。CLR 会在托管堆上分配内存，复制值类型的数据到新对象中。

```csharp
int i = 42;                        // 值在栈上
object o = i;                      // 装箱：在堆上分配对象，复制值
Console.WriteLine(o.GetType());    // 输出: System.Int32
```

**底层实现：**
1. 在托管堆上分配内存（大小 = 值类型大小 + 对象头 SyncBlock + 类型指针 MethodTable）
2. 将值类型的数据复制到堆上的新对象
3. 返回新分配对象的引用

**内存示意：**
```
装箱前 (Stack)          装箱后 (Stack + Heap)
┌──────┐                Stack              Heap
│ i=42 │                ┌────────┐        ┌──────────────┐
└──────┘                │ o (ref) │──────→ │ object (42)  │
                        └────────┘        └──────────────┘
```

### 3.2 什么是拆箱

拆箱是将 `object` 类型的引用**显式转换**回值类型的过程。需要**强制类型转换**，类型不匹配会抛出 `InvalidCastException`。

```csharp
int i = 42;
object obj = i;                    // 装箱
int unboxedValue = (int)obj;      // 拆箱：显式转换
Console.WriteLine(unboxedValue);  // 输出: 42

// 错误示例：
object wrongObj = "This is a string";
try
{
    MyValueType invalidUnbox = (MyValueType)wrongObj; // InvalidCastException!
}
catch (InvalidCastException ex)
{
    Console.WriteLine(ex.Message);
}
```

### 3.3 代码示例（使用 struct）

```csharp
struct MyValueType
{
    public int Value;
}

class Program
{
    static void Main()
    {
        // 创建值类型
        MyValueType value = new MyValueType { Value = 42 };
        
        // 装箱: 将 MyValueType 转为 object
        object obj = value;  // 发生装箱，堆上分配
        
        // 拆箱: 从 object 转回 MyValueType
        MyValueType unboxedValue = (MyValueType)obj;  // 拆箱
        
        Console.WriteLine(unboxedValue.Value);  // 输出: 42
        
        // 错误类型拆箱
        object wrongObj = "This is a string";
        try
        {
            MyValueType invalidUnbox = (MyValueType)wrongObj;
        }
        catch (InvalidCastException ex)
        {
            Console.WriteLine(ex.Message);  // 捕获异常
        }
    }
}
```

### 3.4 性能影响

装箱操作有显著性能开销：
- **堆分配**：在托管堆上分配内存，触发 GC 压力
- **数据复制**：值类型数据从栈复制到堆
- **拆箱验证**：运行时类型检查，失败抛异常

> ⚠️ **注意**：频繁的装箱拆箱会导致 GC 压力增大，在 Unity 等对帧率敏感的场景中尤为关键。

---

## 四、如何减少装箱拆箱操作

### 4.1 使用泛型集合代替非泛型集合

```csharp
// ❌ 差 - 使用 ArrayList（object 类型，会装箱）
ArrayList list = new ArrayList();
list.Add(42);  // int → object，装箱!

// ✅ 好 - 使用 List<T>（泛型，无装箱）
List<int> intList = new List<int>();
intList.Add(42);  // 无装箱
```

> 用 `List<T>`、`Dictionary<TKey, TValue>` 等泛型集合代替 `ArrayList`、`Hashtable`。

### 4.2 使用泛型 Dictionary 实现事件系统

```csharp
public static class MessageSystem
{
    private static class MessageHandler<TEvent, TParam> where TEvent : struct
    {
        public static readonly Dictionary<TEvent, Action<TParam>> eventTable =
            new Dictionary<TEvent, Action<TParam>>(32);
    }

    // 注册
    public static void Register<TEvent, TParam>(TEvent eventType, Action<TParam> handler)
        where TEvent : struct
    {
        var handlers = MessageHandler<TEvent, TParam>.eventTable;
        if (handlers.TryGetValue(eventType, out var existing))
        {
            existing += handler;
            handlers[eventType] = existing;
        }
        else
        {
            handlers.Add(eventType, handler);
        }
    }

    // 触发
    public static void Trigger<TEvent, TParam>(TEvent eventType, TParam param)
        where TEvent : struct
    {
        if (MessageHandler<TEvent, TParam>.eventTable.TryGetValue(eventType, out var handlers))
        {
            handlers?.Invoke(param);
        }
    }
}
```

### 4.3 使用 struct 实现接口

```csharp
interface IBehavior
{
    void Execute();
}

// 使用 struct 实现接口（需通过泛型约束避免装箱）
struct BehaviorA : IBehavior { /*...*/ }
struct BehaviorB : IBehavior { /*...*/ }

// 使用泛型约束避免装箱
void ProcessBehavior<T>(T behavior) where T : IBehavior
{
    behavior.Execute();  // 无装箱调用
}
```

> ⚠️ **关键**：只有通过泛型约束调用接口方法才无装箱。若将 struct 直接转为接口类型 `IBehavior b = new BehaviorA()`，仍会发生装箱。

### 4.4 其他优化措施

1. **重写 ToString() / GetType() 等方法**：struct 中重写 `ToString()`，避免调用 `object` 基类方法时的装箱
```csharp
struct MyStruct
{
    public int Value;

    // 重写 ToString 避免装箱
    public override string ToString()
    {
        return Value.ToString();
    }
}
```

2. **使用 `in` 参数修饰符**：传递大型 struct 时使用 `in` 关键字，按只读引用传递，避免复制和装箱
```csharp
void ProcessLargeStruct(in BigStruct data)
{
    // 按只读引用传递，无复制开销
}
```

3. **缓存已装箱的值**：如果某个值需要反复装箱为 object，可预先装箱一次并缓存
```csharp
object cachedBoxedInt = 42;  // 只装箱一次
Method(cachedBoxedInt);      // 复用
Method(cachedBoxedInt);      // 复用
```

4. **使用 `yield return null` 替换 `yield return 0`**：在 Unity 协程中，`yield return null` 无需装箱，而 `yield return 0` 会将 0 装箱

5. **调用 `GetHashCode()` / `Equals()` / `ToString()` 时注意**：对 struct 直接调用这些方法如果未重写，会触发装箱。务必为常用 struct 重写这些方法。

---

## 五、委托（Delegate）与事件（Event）

### 5.1 什么是委托

委托是 C# 中的**函数指针类型**，用于封装方法的引用。它是类型安全的，可以持有对静态方法或实例方法的引用。

在 Unity 中常用：
- `Action` / `Func` 泛型委托
- `event` 关键字
- `UnityEvent`（可在 Inspector 中绑定）

### 5.2 委托链（Delegate Chain）

委托支持**多播**（Multicast），可以将多个方法合并为一个调用链：

```csharp
// 定义委托链
delegate void AttackDelegate(int damage);

// 在 Unity 中使用
AttackDelegate attackEffects = MeleeAttack;
attackEffects += PlaySound;      // 添加音效
attackEffects += SpawnVFX;       // 添加特效
attackEffects?.Invoke(50);       // 一次性调用所有注册的方法
```

执行流程：
```
Player 攻击 (50 damage)
├── MeleeAttack(50)    → 执行近战攻击
├── PlaySound(50)      → 播放音效
└── SpawnVFX(50)       → 生成特效
```

### 5.3 什么是事件

事件（Event）是委托的**封装包装器**，用于实现发布-订阅（Publisher-Subscriber）模式。它对委托施加访问限制，使得外部只能 `+=` 订阅或 `-=` 取消订阅。

```
事件机制:
┌─────────────┐   触发事件    ┌────────────┐
│  发布者      │ ──────────→ │  订阅者 A   │
│ (Publisher) │             └────────────┘
│             │             ┌────────────┐
│  触发事件    │ ──────────→ │  订阅者 B   │
└─────────────┘             └────────────┘
```

**事件的关键特性：**
- **发布者**（Publisher）：定义事件，持有委托实例，决定何时触发事件
- **订阅者**（Subscriber）：通过 `+=` 注册处理程序，通过 `-=` 取消注册
- 外部类只能订阅/取消订阅，不能直接调用或重置事件的调用列表
- 防止了委托被外部意外清空或直接调用的问题

### 5.4 事件在 Unity 中的应用示例

```csharp
// 发布者
public class PlayerHealth : Subject<PlayerHurtData>
{
    public void TakeDamage(float damage, GameObject source)
    {
        Notify(new PlayerHurtData(damage, source, gameObject));
    }
}

// 订阅者 A - UI 更新
public class HealthUI : IGameEventListener<PlayerHurtData>
{
    public void OnEvent(PlayerHurtData data)
    {
        // 更新血条 Slider 和 Text
        Debug.Log($"[UI] 伤害: {data.Damage}, 来源: {data.Source.name}");
    }
}

// 订阅者 B - 成就系统
public class AchievementSystem : IGameEventListener<PlayerHurtData>
{
    public void OnEvent(PlayerHurtData data)
    {
        if (data.Damage > 20f)
        {
            Debug.Log($"[成就] 重击: {data.Damage}");
        }
    }
}
```

---

## 六、接口（Interface）与抽象类（Abstract Class）

### 6.1 核心区别

| 维度 | 接口 (Interface) | 抽象类 (Abstract Class) |
|------|-----------------|------------------------|
| 本质 | "Can-Do" 能力约定 | "Is-A" 继承关系 |
| 成员实现 | 无实现（C# 8.0+ 可有默认实现） | 可以有部分实现 |
| 访问修饰符 | 成员默认 public | 可以有 public/protected/private |
| 字段 | 不能包含字段 | 可以包含字段 |
| 构造函数 | 无 | 有 |
| 多继承 | 一个类可实现多个接口 | 一个类只能继承一个抽象类 |
| 虚方法 | 无需 virtual/override | 支持 virtual/override |

### 6.2 接口中可以定义属性

接口中可以定义属性（包括 get/set 访问器），但不提供实现：

```csharp
public interface IPerson
{
    string Name { get; set; }    // 可读写属性
    int Age { get; }             // 只读属性
}

public class Employee : IPerson
{
    public string Name { get; set; }
    public int Age { get; private set; }
}
```

通过接口暴露属性，实现类可以自行决定 setter 的访问级别（如 `private set`）。

---

## 七、GC（垃圾回收）基础

### 7.1 GC 算法流程

C# 使用**分代垃圾回收**（Generational Collection），基于以下观察：
- 新创建的对象生命周期通常较短
- 存活时间越长的对象，越可能继续存活

**分代模型：**

| 代 | 说明 |
|----|------|
| Generation 0 | 最新分配的对象，GC 最频繁 |
| Generation 1 | 从 Gen 0 存活下来的对象，作为 Gen 0 和 Gen 2 之间的缓冲区 |
| Generation 2 | 长期存活的对象（如静态数据、单例），GC 最不频繁 |
| LOH (Large Object Heap) | 大于 85,000 字节的对象，仅在 Full GC 时回收，不会压缩 |

### 7.2 GC 工作流程

**标记-清除-压缩（Mark-Sweep-Compact）算法：**

1. **标记阶段（Mark Phase）**：从根引用（GC Roots：静态字段、栈上局部变量、CPU 寄存器等）出发，遍历所有可达对象并标记为存活
2. **清除阶段（Sweep Phase）**：回收所有未标记的对象内存
3. **压缩阶段（Compacting）**：移动存活对象，消除内存碎片

```
GC 阶段示意:
[Mark] → [Sweep] → [Compact]
 标记      清除      压缩碎片
```

### 7.3 Unity 中的 GC

Unity 不同版本使用不同的 GC 实现：

| Unity 版本 | 运行时 (Runtime) | GC 实现 |
|-----------|-----------------|--------|
| 早期版本 | Mono | Boehm GC（不分代） |
| 2019+ / IL2CPP | IL2CPP | 增量 GC（Incremental GC） |
| 2021 LTS+ | .NET Core / CoreCLR | 现代分代 GC |

**Unity GC 注意点：**

- Boehm GC 不分代，每次 GC 都扫描整个堆（Stop-the-World）
- IL2CPP 的增量 GC 将 GC 工作分散到多帧执行，减少卡顿
- **Managed Heap**（C#/Mono 侧）和 **Native Memory**（引擎侧 Unity Engine 内部）是分开管理的
- `Update()` 中频繁 `new` 对象（如 `FindObjectOfType<>()`、`GetComponent<>()` 等）会在 Managed Heap 上产生垃圾
- 使用 Unity Profiler 分析 GC Alloc 和 GC.Collect 调用

### 7.4 Unity GC 优化策略

1. **使用对象池（Object Pool）**：避免频繁创建和销毁对象
```csharp
public void ReturnBullet(GameObject bullet)
{
    bullet.SetActive(false);  // 回收而非销毁
}
```

2. **使用 struct 替代小型 class 数据**
```csharp
// ❌ 使用 class（堆分配，GC 压力）
public class BulletData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Speed;
}

// ✅ 使用 struct（栈分配，零 GC）
public struct BulletData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Speed;
}
```

3. **预分配数组代替 List 动态扩容**
```csharp
// ❌ List 动态扩容产生垃圾
private List<Enemy> enemies = new List<Enemy>();

// ✅ 预分配固定数组
private Enemy[] enemyArray = new Enemy[120];
private int enemyCount = 0;
```

4. **使用 Profiler 定位 GC 热点**
```csharp
void Update()
{
    Unity.Profiling.Profiler.BeginSample("CombatUpdate");
    // 你的代码
    Unity.Profiling.Profiler.EndSample();
}
// Window > Analysis > Profiler
// 关注: GC Alloc、Reserved (GC Reserved)
```

5. **Unity 2021+ 优化新特性**
   - **Burst Compiler**：将 C# 代码编译为高性能 Native 代码
   - **Collections Package**：`NativeList<T>`、`NativeArray<T>` 等非托管容器

### 7.5 面试回答框架

> Unity 中 GC 核心要点：
> 1. Unity 使用 Boehm GC（Mono）/ 增量 GC（IL2CPP），不分代或分代取决于运行时
> 2. Managed Heap（托管堆）存放 C# 对象，Native Memory 存放引擎内部数据
> 3. 通过对象池、struct、预分配等手段减少堆分配
> 4. 使用 Unity Profiler 监控 GC Alloc 和 GC.Collect，定位问题

---

## 八、CLR 与虚拟机

### 8.1 什么是 CLR

CLR（Common Language Runtime，公共语言运行时）是 Microsoft .NET Framework 的虚拟机/运行时环境。它包括以下核心功能：

1. **代码管理**：加载并执行 CIL（Common Intermediate Language）
2. **MSIL（Microsoft Intermediate Language）**：中间语言，由编译器生成，由 CLR JIT 编译为机器码
3. **垃圾回收（GC）**：自动内存管理
4. **异常处理**：统一的异常处理机制
5. **类型安全**：编译时和运行时类型检查
6. **线程管理**：托管线程池
7. **安全性**：代码访问安全（CAS）

### 8.2 C# 编译运行流程

```
源代码 (C#)
    ↓ [C# 编译器]
CIL / MSIL (Common Intermediate Language)
    ↓ [CLR: JIT (Just-In-Time) 编译]
Native Code (机器码)
    ↓ [执行]
程序运行
```

**详细流程：**
1. C# 源代码经 C# 编译器（csc）编译为 CIL（通用中间语言）——生成 `.dll` / `.exe` 文件
2. 运行时，CLR 将 CIL 通过 **JIT（Just-In-Time）编译** 转换为 Native Code
3. JIT 编译是按方法粒度进行的：每个方法首次调用时才编译，编译结果缓存复用

### 8.3 CLR vs JVM

| 维度 | CLR (.NET) | JVM (Java) |
|------|-----------|------------|
| 中间语言 | CIL (MSIL) | Bytecode |
| 运行时 | CLR (Common Language Runtime) | JVM (Java Virtual Machine) |
| JIT | 支持（RyuJIT） | 支持（HotSpot） |
| 跨语言 | 多种语言（C#, VB.NET, F# 等）编译到同一 CIL | 主要面向 Java |

### 8.4 .NET Framework vs Mono

- **Mono** 是 .NET Framework 的开源实现，最初用于 Linux 平台
- Mono 实现了大部分 .NET Framework API，但在 **CLR 实现细节上有差异**
- **Unity3D** 内嵌了 Mono 运行时来运行 C# 脚本代码
  - Unity 使用 **IL2CPP** 将 CIL 转为 C++ 源码，再用平台编译器编译为 Native Code
  - IL2CPP VM：比 Mono VM 性能更高、更安全（无法动态生成 IL）
  - WebGL 等平台必须使用 IL2CPP

### 8.5 Unity 中的 C# 运行时

```
Unity 早期:
  C# 源码 → CIL → Mono VM (解释 / JIT)

Unity 现代 (IL2CPP):
  C# 源码 → CIL → IL2CPP → C++ 源码 → 平台编译器 → Native Code

优势：
  - 性能提升（全 AOT 编译）
  - 平台兼容性好（iOS 禁止 JIT）
  - 安全性高（无运行时 IL 注入）
```

### 8.6 什么是虚拟机

虚拟机（VM）是模拟物理计算机的软件。提供了一套与平台无关的执行环境：
- 开发者编写代码时针对 VM 编程，而非特定硬件
- VM 负责将中间语言翻译为本地机器码（JIT 或 AOT）
- 提供 GC、安全检查、类型安全等托管服务

---

## 九、堆（Heap）与栈（Stack）内存

### 9.1 栈内存

- **自动分配与释放**：局部变量在进入作用域时分配，离开时自动释放
- **分配速度快**：仅移动栈指针
- **容量有限**：通常为 1MB，递归过深或大量局部变量可能导致 `StackOverflowException`
- 值类型局部变量通常分配在栈上

### 9.2 堆内存

- **托管堆**（Managed Heap）：通过 `new` 关键字分配，由 GC 管理生命周期
- **LOH**（Large Object Heap）：存放大于 85KB 的对象
- **非托管堆**（Unmanaged Heap）：通过 `Marshal.AllocHGlobal` / `Marshal.AllocCoTaskMem` 分配，需手动释放（`FreeHGlobal` / `FreeCoTaskMem`）

### 9.3 栈上分配（stackalloc）

```csharp
// 普通值类型 - 默认在栈上
int x = 42;
var pt = new MyStruct(1, 2);  // struct 在栈上（局部变量时）

// unsafe + stackalloc - 在栈上分配数组
unsafe
{
    int* p = stackalloc int[128]; // 分配 128 个 int 在栈上
    Span<int> buf = stackalloc int[64];  // 使用 Span<T> 包装
    buf[0] = 4;
}
```

> ⚠️ `stackalloc` 仅在 unsafe 上下文或 `Span<T>` 语法中可用。`Span<T>` / `ReadOnlySpan<T>` 是安全的栈内存包装。

### 9.4 unsafe 代码中的注意事项

```csharp
unsafe
{
    int x = 42;
    // 将局部变量地址传给其他线程是危险的！
    new Thread(() =>
    {
        Console.WriteLine($"from other thread: {*(int*)(&x)}");
    }).Start();
    Thread.Sleep(100);
}
// x 可能已经被释放，导致未定义行为
```

> **闭包陷阱**：Lambda 捕获的局部变量会被提升为堆上的字段，闭包会延长变量生命周期。这可能导致非预期的内存分配。
```csharp
void Demo()
{
    int a = 100;  // 被闭包捕获，会被提升到堆上
    Task.Run(() =>
    {
        Console.WriteLine(a);  // a 实际访问的是堆上的字段
    });
}
```

---

## 十、Dictionary 底层原理与哈希冲突

### 10.1 Dictionary 内部结构

`Dictionary<TKey, TValue>` 内部由三个核心数据结构组成：

```
Dictionary<TKey, TValue>
├── buckets : int[]          (哈希桶数组，存储 entry 索引)
├── entries : Entry[]        (条目数组，实际存储键值对)
│   ├── int hashCode         (键的哈希码，-1 表示空槽)
│   ├── int next             (链表下一个 entry 索引，-1 表示末尾)
│   ├── TKey key             (键)
│   └── TValue value         (值)
├── count : int              (当前元素数量)
├── freeList : int           (空闲条目链表头)
└── freeCount : int          (空闲条目数量)
```

### 10.2 添加元素流程（Add）

1. 计算键的 `GetHashCode()`，取其模 `hashCode % buckets.Length` 得到目标桶索引 `targetBucket`
2. 检查 `buckets[targetBucket]` 是否已有条目（拉链法处理冲突）
3. 若 `freeList` 有空闲条目，复用之；否则在 `entries` 末尾新增
4. 设置新条目的 `next` 指向 `buckets[targetBucket]` 的原值（拉链）
5. 更新 `buckets[targetBucket]` 指向新条目索引

### 10.3 查找元素流程（ContainsKey / FindEntry）

1. 计算哈希码，定位目标桶 `buckets[hashCode % buckets.Length]`
2. 沿拉链遍历：比较 `hashCode` 和 `Equals()` 方法
3. 找到返回 `true`；未找到返回 `false`
4. 平均时间复杂度 O(1)，最坏 O(n)（全部冲突在一链）

### 10.4 删除元素流程（Remove）

1. 使用类似 `ContainsKey` 的方式查找，记录前驱 `last`
2. 调整链表指针：跳过被删除的条目
3. 将删除条目的 `hashCode` 设为 -1，`next` 指向 `freeList`，加入空闲链表
4. `freeCount++`

### 10.5 扩容（Resize）

- 触发条件：当 `count` 接近 `entries` 容量时（通常负载因子 ~0.72）
- 重新分配更大的 `buckets` 和 `entries`
- 重新计算所有条目的哈希位置，重建拉链
- 扩容操作 O(n)，可能发生在 `Add` 时（单次 Add 摊还 O(1)）

### 10.6 Dictionary vs Hashtable

| 维度 | Hashtable | Dictionary<TKey, TValue> |
|------|-----------|--------------------------|
| 类型安全 | 非泛型（object 类型） | 泛型，编译时类型安全 |
| 装箱 | 值类型键/值会装箱 | 无装箱 |
| 性能 | 较慢（装箱+类型检查） | 更快 |
| 线程安全 | 部分方法有 SyncRoot | 无内置线程安全 |

### 10.7 哈希冲突（Hash Collision）

当两个不同的键产生相同的哈希码（或模同余）时，发生哈希冲突。

**为什么哈希冲突不可避免？**

哈希函数将无限可能的值映射到有限的桶空间，根据鸽巢原理，冲突在理论上无法避免。

**冲突解决方式（拉链法 / 开放寻址法）：**

Dictionary 使用的是**拉链法**（Separate Chaining）：
```
buckets[3]:
  Entry[0]: key=23, hashCode=23, next=-1 (链尾)
  Entry[2]: key=43, hashCode=43, next=0  (指向 Entry[0])
  Entry[5]: key=33, hashCode=33, next=2  (链表头，buckets[3]→5)

  H(23)=3, H(33)=3, H(43)=3 — 三个键映射到同一桶
```

**C# 中减少哈希冲突的策略：**
1. **高质量哈希函数**：`GetHashCode()` 应尽量均匀分布
2. **重写 Equals**：与 `GetHashCode` 保持一致（相等对象哈希码必须相同，反之不必然）
3. **使用素数容量**：减少模运算导致的规律性冲突
4. **使用 `IEqualityComparer<T>`**：自定义比较器

---

## 十一、String 与 StringBuilder

### 11.1 String 的不可变性

C# 中的 `string` 是**不可变**（Immutable）的引用类型。对字符串的任何修改操作（拼接、替换、截取等）都会创建新的字符串对象，原字符串保持不变。

### 11.2 StringBuilder 的应用

在需要大量字符串拼接的场景（如循环中拼接日志、生成大量文本），应使用 `StringBuilder` 以避免创建大量临时字符串对象。

```csharp
// ❌ 低效 - 每次拼接创建新 string
string result = "";
for (int i = 0; i < 1000; i++)
{
    result += i.ToString();  // 每次创建新对象
}

// ✅ 高效 - 使用 StringBuilder
StringBuilder sb = new StringBuilder();
for (int i = 0; i < 1000; i++)
{
    sb.Append(i);
}
string result = sb.ToString();
```

> **String 是 immutable（不可变）类型**：每次拼接/替换/截取都在堆上创建新字符串对象，原字符串不变。**StringBuilder 内部维护可变 char[] 缓冲区**，Append/Insert/Replace 直接修改缓冲区，仅在 ToString() 时才生成最终字符串。因此在循环或大量拼接场景中，StringBuilder 避免了创建大量临时字符串对象，显著降低内存分配和 GC 压力。两者对比：string 适用于少量拼接、键值查找等场景（线程安全、驻留优化）；StringBuilder 适用于频繁修改（日志拼接、模板生成等）。

---

## 十二、单例模式（Singleton Pattern）

### 12.1 基本实现

```csharp
public sealed class PerfectSingleton
{
    // 使用 Lazy<T> 实现线程安全的延迟初始化
    private static readonly Lazy<PerfectSingleton> _instance =
        new Lazy<PerfectSingleton>(() => new PerfectSingleton());

    // 私有构造函数防止外部实例化
    private PerfectSingleton()
    {
        // 初始化
    }

    public static PerfectSingleton Instance => _instance.Value;

    public void ShowMessage(string message)
    {
        Console.WriteLine($"[PerfectSingleton] {message}");
    }

    public int Calculate(int a, int b)
    {
        return a + b;
    }
}

class Program
{
    static void Main()
    {
        PerfectSingleton.Instance.ShowMessage("Hello, Singleton!");

        var result = PerfectSingleton.Instance.Calculate(3, 7);
        PerfectSingleton.Instance.ShowMessage($"3 + 7 = {result}");

        // 验证单例唯一性
        var instance1 = PerfectSingleton.Instance;
        var instance2 = PerfectSingleton.Instance;
        Console.WriteLine($"同一实例: {object.ReferenceEquals(instance1, instance2)}"); // True
    }
}
```

### 12.2 实现要点

1. **sealed 密封类**：防止派生类破坏单例
2. **私有构造函数**：防止外部 new 实例化
3. **Lazy\<T\>**：线程安全的延迟初始化（优于双重检查锁定 + volatile）
4. **静态只读字段**：CLR 保证只初始化一次

### 12.3 单例模式的缺陷

1. **全局状态与紧耦合**
   - 单例引入全局可变状态，使代码依赖关系隐式化
   - 难以追踪：任何代码都能访问单例，形成"上帝对象"

2. **可测试性差**
   - 难以用 Mock/Stub 替换单例进行单元测试
   - 测试间的状态污染问题

3. **多线程问题**
   - 若实现不当（未使用 `Lazy<T>` 或 `volatile`），可能导致多个实例或部分初始化

4. **生命周期不可控**
   - 静态单例存活到 AppDomain 卸载，无法主动释放
   - 在 Unity 中可能跨越场景生命周期

### 12.4 替代方案

- **依赖注入（Dependency Injection）**：通过构造函数或属性注入依赖，由 DI 容器管理生命周期
- **服务定位器 + 接口**：降低耦合度
- **合理使用单例**：仅在确实需要全局唯一访问点且无可替代方案时使用

---

## 十三、其他 C# 高频面试知识点

### 13.1 using 语句

`using` 语句确保实现了 `IDisposable` 接口的对象在使用后被正确释放：

```csharp
using (var stream = new FileStream("data.txt", FileMode.Open))
{
    // 使用 stream
}
// stream.Dispose() 自动调用（即使发生异常）
```

### 13.2 ArrayList vs List\<T\>

| 维度 | ArrayList | List\<T\> |
|------|-----------|-----------|
| 类型安全 | 存储 object，任何类型可混入 | 编译时类型安全 |
| 装箱 | 值类型会装箱 | 无装箱 |
| 性能 | 较慢 | 更快 |

### 13.3 C# 中的 GC（垃圾回收）

GC 是 C# 的自动内存管理机制，无需手动释放内存。核心原理是跟踪从 GC Roots 可达的对象，回收不可达对象的内存。（详见第七章）

### 13.4 struct 与 class 的区别

| 维度 | struct | class |
|------|--------|-------|
| 类型 | 值类型 | 引用类型 |
| 存储位置 | 栈（局部变量时） | 堆 |
| 继承 | 不支持继承（隐式 sealed） | 支持继承 |
| 默认值 | 不能为 null | 可以为 null |
| 构造函数 | 不能有无参构造函数（C# 10.0 前） | 可以有 |
| 适用场景 | 小型数据、高性能、零 GC | 复杂对象、需要继承 |

---

## 十四、Unity 相关 C# 实践

### 14.1 PlayerPrefs - 轻量级存储

```csharp
// 保存
PlayerPrefs.SetInt("HighScore", 300);
PlayerPrefs.SetString("PlayerName", "Hero");
PlayerPrefs.SetFloat("Volume", 0.8f);
PlayerPrefs.Save();  // 强制写入磁盘

// 读取
int score = PlayerPrefs.GetInt("HighScore", 0);
string name = PlayerPrefs.GetString("PlayerName", "Default");

// 检查是否存在
if (PlayerPrefs.HasKey("HighScore"))
{
    Debug.Log("高分记录已存在");
}

// 删除
PlayerPrefs.DeleteKey("HighScore");  // 删除单个键
// PlayerPrefs.DeleteAll();  // 删除全部键
```

### 14.2 ScriptableObject - 配置数据

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "配置/游戏设置")]
public class GameSettings : ScriptableObject
{
    public float MouseSensitivity = 1.0f;
    public bool EnableSound = true;
    public int MaxEnemies = 50;
    public string[] DifficultyLevels = { "简单", "普通", "困难" };
}

// 使用
public class SettingsManager : MonoBehaviour
{
    public GameSettings settings;

    void Start()
    {
        Debug.Log($"鼠标灵敏度: {settings.MouseSensitivity}");
        Debug.Log($"难度: {settings.DifficultyLevels[1]}");
    }
}
```

### 14.3 二进制加密存档

```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class SecureSaveSystem : MonoBehaviour
{
    private string savePath;
    private static readonly string key = "dmi016(U@8#8123"; // AES 密钥

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "secure_save.dat");
        SaveEncryptedData();
        LoadEncryptedData();
    }

    void SaveEncryptedData()
    {
        GameData data = new GameData();
        data.CurrentLevel = 10;
        data.PlayerHealth = 100;
        data.PlayerName = "Hero";

        string json = JsonUtility.ToJson(data);
        string encryptedJson = Encrypt(json);
        File.WriteAllText(savePath, encryptedJson);
    }

    void LoadEncryptedData()
    {
        if (File.Exists(savePath))
        {
            string encryptedJson = File.ReadAllText(savePath);
            string decryptedJson = Decrypt(encryptedJson);
            GameData data = JsonUtility.FromJson<GameData>(decryptedJson);
            Debug.Log($"存档读取 - 关卡: {data.CurrentLevel}");
        }
    }

    string Encrypt(string plainText)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = new byte[16]; // 实际应用中应使用随机 IV

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    string Decrypt(string cipherText)
    {
        byte[] buffer = Convert.FromBase64String(cipherText);
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = new byte[16];

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }
}
```

### 14.4 Unity 协程与自定义等待

```csharp
// 自定义 YieldInstruction（等待 2 秒）
public class MyWaitForSeconds : CustomYieldInstruction
{
    private float waitTime;
    private float startTime;

    public MyWaitForSeconds(float seconds)
    {
        waitTime = seconds;
        startTime = Time.time;
    }

    public override bool keepWaiting => Time.time - startTime < waitTime;
}

// 使用
IEnumerator CoroutineWithCustomWait()
{
    Debug.Log("开始等待...");
    yield return new MyWaitForSeconds(2.0f);  // 自定义等待
    Debug.Log("等待结束!");
}
```

### 14.5 异步资源加载

```csharp
using UnityEngine;
using UnityEngine.UI;

public class ResourcesAsyncDemo : MonoBehaviour
{
    [SerializeField] private string assetPath = "Textures/MyTex";
    [SerializeField] private Image img;
    [SerializeField] private Text progressText;

    void Start()
    {
        StartCoroutine(LoadAssetAsync());
    }

    IEnumerator LoadAssetAsync()
    {
        var req = Resources.LoadAsync<Object>(assetPath);
        while (!req.isDone)
        {
            progressText.text = $"{req.progress:P1}";
            yield return null;
        }

        var tex = req.asset as Texture2D;
        if (tex != null && img != null)
        {
            img.sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                Vector2.one * 0.5f);
        }
    }
}
```

### 14.6 GameEvent 系统

```csharp
// 发布者
public class PlayerHealth : Subject<PlayerHurtData>
{
    public void TakeDamage(float damage, GameObject source)
    {
        Notify(new PlayerHurtData(damage, source, gameObject));
    }
}

// 订阅者 A - 血条 UI
public class HealthUI : IGameEventListener<PlayerHurtData>
{
    public void OnEvent(PlayerHurtData data)
    {
        // 更新 Slider 和 Text
        Debug.Log($"[UI] 伤害: {data.Damage}, 来源: {data.Source.name}");
    }
}

// 订阅者 B - 成就系统
public class AchievementSystem : IGameEventListener<PlayerHurtData>
{
    public void OnEvent(PlayerHurtData data)
    {
        if (data.Damage > 20f)
        {
            Debug.Log($"[成就] 重击: {data.Damage}");
        }
    }
}

// 设计要点:
// 1. 基于 IGameEventListener<T> 接口，解除耦合
// 2. OnDestroy 时自动取消注册，防止内存泄漏
// 3. 事件数据结构使用 struct，零 GC
// 4. 支持单播和多播
// 5. 类型安全，编译时检查
// 6. 灵活的事件类型注册机制
```

---

## 十五、面试高频问题速查表

C# 面试高频问题速查，涵盖值类型/引用类型、装箱拆箱、委托事件、GC、泛型、LINQ、async/await、反射等核心知识点：

| 编号 | 问题 | 关联章节 |
|------|------|----------|
| 1 | C++ 和 C# 的区别？ | 第一章 |
| 2 | 值类型与引用类型的区别？基类分别是什么？ | 第二章 |
| 3 | 什么是装箱和拆箱？底层如何实现？ | 第三、四章 |
| 4 | 项目中如何减少装箱拆箱？ | 第四章 |
| 5 | 委托和事件是什么？区别？ | 第五章 |
| 6 | 接口与抽象类的区别？接口可以有属性吗？ | 第六章 |
| 7 | C# 的 GC 原理？分代回收算法？ | 第七章 |
| 8 | Dictionary 底层原理？哈希冲突如何解决？ | 第十章 |
| 9 | String 和 StringBuilder 的区别？ | 第十一章 |
| 10 | struct 和 class 的区别？ | 第十三章 |
| 11 | CLR 是什么？C# 的编译运行流程？ | 第八章 |
| 12 | ArrayList 与 List\<T\> 的区别？ | 第十三章 |
| 13 | using 语句的作用？ | 第十三章 |
| 14 | C# 中 unsafe 代码的使用场景和风险？ | 第九章 |
| 15 | Unity 中的 GC 优化策略？ | 第七章 |
| 16 | HashTable 和 Dictionary 的区别？ | 第十章 |
| 17 | 单例模式的优缺点？ | 第十二章 |
| 18 | Unity 中使用 Mono 还是 IL2CPP？区别？ | 第八章 |
| 19 | C# 的属性（Property）和字段（Field）？ | 属性封装字段（get/set），字段直接存储数据 | 第十七章 |
| 20 | 泛型（Generics）的优点？ | 类型安全、无装箱、代码复用、高性能 | 第十七章 |
| 21 | LINQ 的实现原理？ | 延迟执行：迭代时才执行，基于 yield 或表达式树 | 第十七章 |
| 22 | async/await 的实现原理？ | 编译器生成状态机，await 时释放线程，完成后恢复 | 第十七章 |
| 23 | Lambda 表达式和闭包的原理？ | 闭包将捕获变量提升为匿名类的字段 | 第十七章 |
| 24 | 反射（Reflection）的原理和性能影响？ | 运行时读取元数据，动态调用；慢，需缓存/预编译 | 第十七章 |
| 25 | C# 中 string 的驻留机制（String Interning）？ | 相同字面量只存一份；动态字符串需手动 Intern | 第十七章 |
| 26 | 浮点数为什么会有精度误差？怎么解决？ | 第十六章 |

---

## 十七、面试题详解（Question 19-25）

### 17.1 属性（Property）与字段（Field）的区别

- **字段（Field）**：直接存储在类中的变量，通常声明为 `private`，是数据容器。
- **属性（Property）**：对字段的封装，通过 `get/set` 访问器控制读写，本质是编译器生成的一对方法（如 `get_Age()` / `set_Age(value)`）。
- 属性可以在访问时添加验证逻辑、计算、通知事件等；字段只是裸数据的存储。
- 属性可以只读（只有 get）、只写（只有 set）、计算属性（无对应字段）、不同访问修饰符。

```csharp
private int _age;                          // 字段：直接存储数据
public int Age                             // 属性：封装字段，添加验证
{
    get => _age;
    set { if (value >= 0) _age = value; }
}
public int BirthYear => DateTime.Now.Year - Age;  // 计算属性，无对应字段
```

### 17.2 泛型（Generics）的优点

- **类型安全**：编译时确定类型，避免 `object` 带来的运行时类型转换错误。
- **避免装箱拆箱**：值类型直接存储（`List<int>` 内部是 `int[]`），无堆分配与 GC 压力。
- **代码复用**：一份代码适用于多种类型（`List<T>`, `Dictionary<TKey, TValue>`），减少重复代码。
- **性能更好**：泛型集合比非泛型（`ArrayList`/`Hashtable`）快 2-3 倍，且无类型检查开销。
- **约束（where）**：可对类型参数施加约束（`where T : class, new()`），保留泛型灵活性的同时获得编译时检查。

```csharp
public T Max<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) > 0 ? a : b;
int maxInt = Max(3, 5);        // T=int, 无装箱
string maxStr = Max("ab", "cd"); // T=string, 代码复用
```

### 17.3 LINQ 的实现原理（延迟执行）

- LINQ to Objects 基于 `IEnumerable<T>` 的扩展方法，核心使用 **yield return 迭代器** 实现延迟执行。
- **延迟执行（Deferred Execution）**：查询只在真正迭代（`foreach`、`.ToList()`、`.Count()`）时才执行，而非定义时。每次迭代都重新执行查询。
- 立即执行的方法（`.ToList()`, `.ToArray()`, `.First()`, `.Count()`）会强制立即求值并固化结果。
- LINQ to SQL / Entity Framework 还使用 **表达式树（Expression Tree）**，将 C# 查询转换为 SQL 在数据库端执行。
- 优点：可以组合构建复杂查询，只遍历一次数据源，节约内存（流式处理）。

```csharp
var query = list.Where(x => x > 5).Select(x => x * 2); // 未执行，仅构建查询链
// ... 可以继续追加条件
var result = query.ToList();  // ← 此时才真正执行所有操作
```

### 17.4 async/await 的实现原理（状态机）

- 编译器将 `async` 方法转换为一个实现了 `IAsyncStateMachine` 接口的 **struct 状态机**。
- 方法体被拆分为多个状态块（每个 `await` 是一个状态分界点），由 `MoveNext()` 方法驱动执行。
- 遇到 `await` 时：若操作未完成，注册一个回调（`OnCompleted`）并立即 return，释放当前线程回线程池。
- 操作完成后，通过回调恢复 `MoveNext()`，继续执行 `await` 之后的代码（可能在同一线程或不同线程）。
- 异步的本质：**不以同步方式阻塞线程，而是暂时让出线程，完成后继续**。

```csharp
public async Task<string> FetchAsync()
{
    var client = new HttpClient();
    string data = await client.GetStringAsync(url); // 挂起点：不阻塞线程
    return data.ToUpper();  // 恢复执行（可能在其他线程上）
}
// 编译器近似生成：
// struct <FetchAsync>d__0 : IAsyncStateMachine {
//     int state;  AsyncTaskMethodBuilder<string> builder;
//     HttpClient client;  string data;  TaskAwaiter<string> awaiter;
//     void MoveNext() { /* 根据 state 跳转到对应代码块 */ }
// }
```

### 17.5 Lambda 表达式和闭包的原理

- **Lambda 没有捕获外部变量时**：编译器生成一个**静态方法**，lambda 等同于该方法的委托引用，零分配。
- **Lambda 捕获外部变量（闭包）时**：编译器生成一个**匿名闭包类**，将捕获的变量提升为该类的**public 字段**，lambda 本身成为该类的一个实例方法。每次进入该方法时 new 一个闭包类实例（产生堆分配！）。
- **闭包的关键**：捕获的是**变量的引用**（提升后的字段），而非**值的快照**。因此变量在 lambda 执行前的修改会影响结果。
- **foreach 陷阱**：C# 5 前 foreach 变量在循环外定义，闭包会捕获到循环结束的值；C# 5+ 修正在循环内定义，每次迭代独立。

```csharp
int factor = 3;  // 被闭包捕获——编译器生成匿名类，factor 变为其字段
Func<int, int> multiply = x => x * factor;  // 实际调用闭包实例方法
factor = 5;                                  // 修改闭包字段的值
Debug.Log(multiply(2)); // 输出 10（引用的是字段当前值，非捕获时的快照）

// 编译器近似生成：
// class <>c__DisplayClass0_0 {
//     public int factor;  // 被捕获的变量提升为字段
//     internal int <M>b__0(int x) => x * factor;
// }
```

### 17.6 反射（Reflection）的原理和性能影响

- **原理**：CLR 将类型元数据（类型名、方法、字段、特性等）存储在程序集的 **元数据表** 中。反射 API（`Type`, `MethodInfo`, `FieldInfo`）在运行时读取这些表，实现动态类型检查和调用。
- **性能开销**：(1) 方法调用（`MethodInfo.Invoke`）比直接调用慢 10-100 倍；(2) 涉及大量的运行时类型检查与装箱拆箱；(3) 触发额外的安全检查（CAS）。
- **优化手段**：缓存 `MethodInfo`/`PropertyInfo`（避免反复查询）；使用 `Delegate.CreateDelegate` 或 `Expression.Compile` 预编译为强类型委托；使用 **Source Generator**（编译时生成代码替代运行时反射）；避免 `GetMethods()` 等全量查询，尽量用 `GetMethod(name)` 精确查找。

```csharp
// ❌ 直接反射调用——每次 Invoke 都慢
MethodInfo method = typeof(MyClass).GetMethod("DoWork");
method.Invoke(instance, new object[] { 42 });

// ✅ 预编译为委托——仅第一次慢，后续接近原生调用速度
var fastInvoke = (Action<int>)Delegate.CreateDelegate(
    typeof(Action<int>), instance, method);
fastInvoke(42);  // 快！
```

### 17.7 String Interning（字符串驻留）

- **原理**：CLR 维护一个全局的**字符串驻留池（Intern Pool）**，编译时遇到相同的字符串字面量，只存储一份，所有引用指向同一内存地址。这通过 `object.ReferenceEquals` 验证为 `true`。
- **好处**：减少重复字符串的内存占用；字符串比较可先比较引用（O(1)），相同引用则无需逐字符对比。
- **自动化**：编译时常量（字面量）自动驻留；运行时动态生成的字符串不会自动驻留。
- **手动操作**：`string.Intern(str)` 将动态字符串加入驻留池（如果已存在则返回已有引用）；`string.IsInterned(str)` 检查是否已驻留。
- **注意事项**：`string.Intern()` 会将字符串永久锁定在驻留池中，即使原字符串无其他引用也不会被 GC 回收。滥用可能导致"驻留池膨胀"。

```csharp
string a = "hello";   // 字面量，编译时自动驻留
string b = "hello";   // 引用同一驻留对象
Debug.Log(object.ReferenceEquals(a, b)); // True

string c = new StringBuilder("hel").Append("lo").ToString(); // 运行时生成，未驻留
Debug.Log(object.ReferenceEquals(a, c)); // False

string d = string.Intern(c);  // 手动驻留——获取池中已有引用
Debug.Log(object.ReferenceEquals(a, d)); // True
```

---

## 十六、浮点数精度问题

### 16.1 为什么会有误差

**根本原因**：float/double 使用**二进制**浮点数格式（IEEE 754），而某些十进制小数在二进制中是**无限循环**的，无法精确表示。

```csharp
float a = 0.1f + 0.2f;
Debug.Log(a);                        // 0.300000004 ❌ 不是完美的 0.3
Debug.Log(a == 0.3f);                // False

double d = 0.1 + 0.2;
Debug.Log(d == 0.3);                 // False
```

**具体例子**：十进制 `0.1` 转二进制 = `0.00011001100110011...`（无限循环 0011），计算机只能截断存储，产生舍入误差。

### 16.2 C# 解决方案：`decimal`

```csharp
decimal m = 0.1m + 0.2m;
Debug.Log(m == 0.3m);                // True ✅
```

**decimal 原理**：128 位**十进制**浮点数。内部用 96 位整数存储有效数字 + 指数存储小数点位置。所以能用十进制精确表示的数，decimal 就能精确存储。

### 16.3 float / double / decimal 对比

| | float | double | decimal |
|---|-------|--------|---------|
| 精度（有效位） | ~7 位 | ~15 位 | **28-29 位** |
| 基數 | 二进制 | 二进制 | **十进制** |
| 0.1 精确？ | ❌ | ❌ | **✅** |
| 运算速度 | CPU 硬件加速 | CPU 硬件加速 | **软件模拟（慢得多）** |
| 内存占用 | 4 字节 | 8 字节 | 16 字节 |
| 适用场景 | 图形坐标、物理 | 科学计算 | **金融、货币** |

### 16.4 游戏中的浮点比较

游戏中几乎不用 `decimal`（没有硬件加速，太慢），而是用**容差比较**：

```csharp
// ❌ 直接比较
if (health == 0) { }

// ✅ epsilon 容差
float epsilon = 0.0001f;
if (Mathf.Abs(a - b) < epsilon) { }

// ✅ Unity 内置（本质就是 epsilon 比较）
if (Mathf.Approximately(a, b)) { }
```

### 16.5 其他避免误差的技巧

| 技巧 | 说明 |
|------|------|
| 用 `int` 存货币 | 存"分"而不是"元"，`int money = 199`（1.99 元） |
| 累加时注意顺序 | 从小往大累加比从大往小累加精度损失更小 |
| `Mathf.Round` | 对显示值做四舍五入，掩藏误差 |
| 定点数 | 自己实现 `struct FixedPoint` 用整数模拟小数 |

---

## 最佳实践

1. **优先使用泛型集合**：`List<T>`、`Dictionary<TKey, TValue>` 代替 `ArrayList`、`Hashtable`，避免装箱
2. **小型数据使用 struct**：热点路径中（如 Update），使用 struct 减少 GC 分配
3. **字符串拼接用 StringBuilder**：循环中避免 `+` 拼接
4. **使用 `in` 参数传递大型 struct**：按只读引用传递，避免复制
5. **重写 struct 的 ToString/Equals/GetHashCode**：避免调用 object 基类方法时的装箱
6. **协程中 `yield return null` 优于 `yield return 0`**：避免装箱
7. **对象池替代频繁 new**：复用对象，减少 GC 压力
8. **使用 `Lazy<T>` 实现线程安全单例**：优于双重检查锁定
9. **实现 IDisposable 时使用 using 语句**：确保资源释放
10. **使用 Profiler 定位 GC 热点**：`Unity.Profiling.Profiler.BeginSample/EndSample`

---

## 相关模块

- [[memory-gc]] - Unity 内存管理与 GC 优化详解
- [[cpp-comparison]] - C++ 与 C# 详细对比
- [[design-patterns]] - 设计模式（含单例模式详解）
- [[performance-optimization]] - Unity 性能优化
- [[data-structures-algorithms]] - 数据结构与算法
- [[unity-core]] - Unity 核心概念
