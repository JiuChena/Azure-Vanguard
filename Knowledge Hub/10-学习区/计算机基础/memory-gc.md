---
tags:
  - memory
  - gc
  - performance
  - unity
created: 2026-06-20
---

# 内存管理与垃圾回收

## 一、栈与堆的内存模型

### 1.1 栈 (Stack)

- 栈内存由编译器自动分配和释放，遵循 LIFO（后进先出）原则。
- 存放**值类型**的局部变量、方法参数、返回地址等。
- 分配速度极快（仅移动栈指针），无需 GC 介入。
- 每个线程有独立的栈空间，大小有限（Windows 默认约 1MB），过度递归或 `stackalloc` 过大时抛出 `StackOverflowException`。

```
STACK (线程独占)          HEAP (托管堆共享)
┌─────────────┐          ┌──────────────────┐
│ int i = 42  │          │ object o → 42     │
│ struct Pt   │          │ string s = "hello"│
│  .x = 1     │          │ List<int> entries │
│  .y = 2     │          │                    │
│ (栈帧)       │          │ (GC 管理)          │
└─────────────┘          └──────────────────┘
```

### 1.2 堆 (Heap)

托管堆由 GC 管理，分为两个区域：

| 区域 | 说明 |
|------|------|
| **SOH** (Small Object Heap) | 存放小于 85000 字节的对象，划分为 Gen 0/1/2 |
| **LOH** (Large Object Heap) | 存放 >= 85000 字节的对象，默认不压缩（.NET 4.5.1+ 可配置） |

堆的创建方式：
- `new` 关键字：在托管堆上分配，由 GC 自动回收
- `Marshal.AllocHGlobal` / `Marshal.AllocCoTaskMem`：在非托管堆上分配，需手动 `FreeHGlobal` / `FreeCoTaskMem`
- `unsafe` + `stackalloc`：在栈上分配非托管内存块（配合 `Span<T>` 使用）

```csharp
// 托管堆分配（GC 管理）
var list = new List<int>();
var arr = new byte[1024];
string s = "hello";

// 非托管堆分配（手动释放）
IntPtr p = Marshal.AllocHGlobal(1024);
try {
    // 使用 p...
} finally {
    Marshal.FreeHGlobal(p);
}

// 栈上分配（unsafe + stackalloc）
unsafe {
    int* p = stackalloc int[128];          // 分配 128 个 int
}
Span<int> buf = stackalloc int[64];        // 使用 Span 包装
buf[0] = 42;
```

### 1.3 堆与栈的关键对比

| 维度 | 栈 (Stack) | 堆 (Heap) |
|------|-----------|-----------|
| 分配速度 | 极快（移动指针） | 较慢（需查找空闲块） |
| 释放方式 | 自动（方法返回时弹出） | GC 自动回收 |
| 存储内容 | 值类型局部变量、引用指针 | 引用类型对象实体 |
| 空间大小 | 有限（~1MB/线程） | 较大（受虚拟内存限制） |
| 碎片问题 | 无 | 存在（GC 压缩可缓解） |
| 线程安全 | 线程独立 | 多线程共享，需同步 |

---

## 二、值类型与引用类型内存布局

### 2.1 值类型 (Value Types)

C# 中的值类型包括：
- 内置简单类型：`int`, `long`, `float`, `double`, `char`, `bool`, `byte`, `short` 等
- 结构体 (`struct`)
- 枚举 (`enum`)

**内存特点：**
- 值类型变量**直接包含**其数据。
- 存储在栈上（局部变量）或作为引用类型对象的内联字段存储在堆上。
- 赋值时复制整个值（深拷贝）。
- 基类为 `System.ValueType`（而 `System.ValueType` 继承自 `System.Object`）。
- 不能为 `null`（除非使用 `Nullable<T>` 即 `T?`）。

```csharp
// 值类型示例
int value = 42;                    // 整数
Vector3 position = new Vector3();  // 结构体
PlayerState state = PlayerState.Idle; // 枚举

// 值类型赋值 = 完整拷贝
int a = 10;
int b = a;   // b = 10, a 和 b 独立
b = 20;      // a 仍然是 10
```

### 2.2 引用类型 (Reference Types)

C# 中的引用类型包括：
- 类 (`class`)
- 接口 (`interface`)
- 委托 (`delegate`)
- 数组 (`array`)
- 字符串 (`string`)

**内存特点：**
- 引用类型变量存储**对象的引用**（指针），对象实体在托管堆上。
- 赋值时复制引用（浅拷贝），多个变量可指向同一对象。
- 基类为 `System.Object`。
- 默认可为 `null`。
- 由 GC 自动回收。

```csharp
// 引用类型示例
string name = "Alice";
int[] numbers = new int[] { 1, 2, 3 };
Action callback = () => Console.WriteLine("done");

// 引用类型赋值 = 复制引用
var list1 = new List<int> { 1, 2, 3 };
var list2 = list1;    // list2 指向同一对象
list2.Add(4);         // list1 也变为 {1,2,3,4}
```

### 2.3 类型系统继承链

```
System.Object
├── System.ValueType
│   ├── int, long, float, double ...
│   ├── char, bool, byte, short ...
│   ├── struct (自定义结构体)
│   └── enum (枚举)
├── string
├── class (自定义类)
├── interface
├── delegate
└── Array
```

---

## 三、装箱与拆箱

### 3.1 什么是装箱 (Boxing)

**装箱**：将值类型隐式转换为 `object` 类型或接口类型的过程。

**底层实现：**
1. 在托管堆上分配内存，大小 = 值类型大小 + 类型指针（`TypeHandle`）+ 同步块索引（`SyncBlockIndex`）。
2. 将值类型的值**拷贝**到堆内存中。
3. 返回对该堆对象的引用（`object`）。

```
装箱前 (栈)               装箱后 (栈 + 堆)
STACK: int i = 42;      STACK: int i = 42;
                        STACK: object o ──→ HEAP: [TypePtr|SyncBlock|int:42]
```

```csharp
int value = 42;                    // 值在栈上
object obj = value;                // 装箱！在堆上创建新对象
Console.WriteLine(obj.GetType());  // System.Int32
```

### 3.2 什么是拆箱 (Unboxing)

**拆箱**：将 `object` 类型的引用显式转换回值类型的过程。

**注意事项：**
- 拆箱必须**精确匹配**原始值类型，否则抛出 `InvalidCastException`。
- 拆箱从堆中**拷贝**值到栈上（不会修改堆上对象）。

```
拆箱过程:
STACK: object o ──→ HEAP: [TypePtr|SyncBlock|int:42]
                                                    ↓ 拷贝
STACK: int i = 42;
```

```csharp
object obj = 42;                    // 先装箱
int unboxed = (int)obj;             // 拆箱：必须匹配 int 类型
Console.WriteLine(unboxed);         // 42

// 错误示例
object wrongObj = "This is a string";
try {
    int invalid = (int)wrongObj;    // InvalidCastException!
} catch (InvalidCastException ex) {
    Console.WriteLine(ex.Message);
}
```

### 3.3 装箱拆箱的性能代价

1. **装箱**：堆分配 + 内存拷贝 → 触发 GC 压力
2. **拆箱**：类型检查 + 内存拷贝 → CPU 开销
3. 频繁装箱拆箱在热路径（Update、循环）中影响严重

### 3.4 项目中的优化措施

**1. 使用泛型集合替代非泛型集合**

```csharp
// 不好 — 每次 Add 都装箱
ArrayList list = new ArrayList();
list.Add(42);                       // int → object，装箱！

// 好 — 无装箱
List<int> intList = new List<int>();
intList.Add(42);                    // 直接存储 int
```

**2. 使用泛型字典代替 Hashtable**

```csharp
// 不好
Hashtable table = new Hashtable();
table["key"] = 100;                 // 装箱

// 好
Dictionary<string, int> dict = new Dictionary<string, int>();
dict["key"] = 100;                  // 无装箱
```

**3. 泛型约束 + 强类型事件系统**

```csharp
public static class MessageSystem
{
    private class MessageHandler<TEvent, TParam> where TEvent : struct
    {
        public static readonly Dictionary<TEvent, Action<TParam>> eventTable = 
            new Dictionary<TEvent, Action<TParam>>();
    }

    public static void Register<TEvent, TParam>(TEvent eventType, Action<TParam> handler)
        where TEvent : struct
    {
        var handlers = MessageHandler<TEvent, TParam>.eventTable;
        if (handlers.TryGetValue(eventType, out var existing)) {
            handlers[eventType] = existing + handler;
        } else {
            handlers.Add(eventType, handler);
        }
    }
}
```

**4. 接口约束为 struct 避免装箱**

```csharp
interface IBehavior {
    void Execute();
}

// 不装箱：泛型参数约束为 struct
void ProcessBehavior<T>(T behavior) where T : struct, IBehavior {
    behavior.Execute();
}

// 会装箱：直接传接口参数
void ProcessBehavior(IBehavior behavior) {
    behavior.Execute();  // struct 实现接口 → 装箱！
}
```

**5. 协程中避免 yield return null 装箱**

```csharp
// 不好 — yield return null 可能引起装箱
yield return null;

// 这些方式也会有类似问题
// ⚠️ 具体优化取决于 Unity 版本和上下文
```

**6. 重写 ToString() / GetType() 等虚方法**

```csharp
struct MyStruct {
    public int Value;
    
    // 重写后调用 ToString() 不会装箱
    public override string ToString() {
        return Value.ToString();
    }
}
```

**7. 使用 in 关键字传递大结构体**

```csharp
// 只读引用传递，避免拷贝，也不装箱
void ProcessLargeStruct(in BigStruct data) {
    // 只读访问 data
}
```

**8. 缓存装箱后的对象（慎用）**

```csharp
// 如果某个值反复作为 object 使用，缓存装箱结果
object cachedBoxedInt = 42;

// 多次使用同一装箱对象
SomeMethod(cachedBoxedInt);
AnotherMethod(cachedBoxedInt);
```

**9. 使用自定义对象池封装**

```csharp
// 对象池：复用对象避免 GC 分配
public class ObjectPool<T> where T : class, new()
{
    private readonly Stack<T> pool = new Stack<T>();
    
    public T Get() {
        return pool.Count > 0 ? pool.Pop() : new T();
    }
    
    public void Return(T obj) {
        pool.Push(obj);
    }
}
```

**10. 总结原则**

> 频繁热路径中：减少值类型转 object、优先泛型集合、接口调用注意 struct 装箱、大 struct 用 `in` 传参。

---

## 四、GC 原理

### 4.1 GC 的基本概念

**垃圾回收 (Garbage Collection, GC)** 是一种自动内存管理机制。GC 会检测托管堆上不再被引用的对象，并自动释放其内存。

与 **C/C++ 手动内存管理** (`malloc/free`, `new/delete`) 的根本区别：
- C/C++：程序员负责，容易出现内存泄漏、野指针、重复释放等问题。
- C#：GC 自动回收，牺牲部分运行时开销换取安全性和开发效率。

### 4.2 GC 如何判断对象是"垃圾"

**标记-清除 (Mark and Sweep) 算法**，分为两个阶段：

**阶段一：标记 (Marking)**
1. 暂停所有托管线程（Stop-The-World）。
2. 从一组"**根 (Roots)**"出发：
   - 静态字段
   - 线程栈上的局部变量/参数
   - CPU 寄存器中的引用
   - GC 句柄表
3. 沿引用链递归遍历，把可达对象标记为"存活"。
4. 未被标记的对象认定为"垃圾"。

**阶段二：清除 (Sweep) 与压缩 (Compact)**
1. 遍历堆，回收未标记对象所占用的内存。
2. 整理（压缩）存活对象到堆的一端，消除内存碎片。
3. 更新引用指针，确保所有引用指向新的对象地址。

```
GC 前: [A|垃圾|B|垃圾|C|垃圾]
         ↓ 标记：A, B, C 可达
         ↓ 清除：回收垃圾
         ↓ 压缩：
GC 后: [A|B|C|_______________空闲空间]
```

### 4.3 分代收集 (Generational Collection)

.NET GC 采用**分代假设**：
- 新创建的对象通常"短命"
- 活得越久的对象，越可能继续存活

因此将托管堆划分为三代：

```
┌─────────────────────────────────────────────────────┐
│  Generation 0  │  Generation 1  │  Generation 2     │
│   (Gen 0)      │   (Gen 1)      │   (Gen 2)         │
├────────────────┼────────────────┼───────────────────┤
│ 最新的临时对象   │ Gen 0 幸存者    │ 长期存活对象       │
│ 分配在此        │ 晋升至此        │ 静态数据/全局缓存   │
│ GC 最频繁       │ GC 偶发         │ Full GC 才回收     │
│ 成本最低        │ 成本中等        │ 成本最高           │
└─────────────────────────────────────────────────────┘
```

**GC 触发层级：**

| GC 级别 | 回收范围 | 触发频率 | 开销 |
|---------|---------|---------|------|
| **Gen 0 GC** | 仅 Gen 0 | 最频繁（分配预算耗尽） | 最低 |
| **Gen 1 GC** | Gen 0 + Gen 1 | 较频繁 | 中等 |
| **Gen 2 GC** (Full GC) | 全部代 + LOH | 最少（内存压力大时） | 最高 |

**⚠️ LOH (Large Object Heap)** 的特殊性：
- 存放 >= 85000 字节的对象。
- 传统上不参与压缩（.NET 4.5.1+ 可通过 `GCSettings.LargeObjectHeapCompactionMode` 配置）。
- 仅在 Full GC 时回收。

### 4.4 Boehm GC

**Boehm GC** 是 Unity 传统运行时（Mono）使用的垃圾回收器，特点：
- **保守式 GC**：不精确区分指针和整数，可能误判。
- **不分代**：每次 GC 扫描整个堆，Stop-The-World 时间长，容易造成卡顿。
- 属于**标记-清除**型 GC，无分代优化。

> ⚠️ Boehm GC 在 Unity 中不分代，因此 GC 调用开销较大，是移动端卡顿的主因之一。

---

## 五、Unity 中的 GC

### 5.1 Unity 运行时环境与 GC 对应关系

| Unity 版本 | 运行时 | GC 实现 | 特点 |
|-----------|--------|--------|------|
| 早期 Unity | Mono | **Boehm GC** | 不分代，一次性扫描全堆 |
| Unity 2019+ / IL2CPP | IL2CPP | **增量 GC (Incremental GC)** | 分步执行，减少卡顿 |
| Unity 2021 LTS+ | .NET Core / Mono | **升级 GC** | 分代 GC 改进 |

### 5.2 Unity 内存管理架构

```
┌────────────────────────────────────────┐
│         Unity 引擎层 (Native C++)      │
│  Physics | Animation | Rendering ...  │
│  (P/Invoke 交互层)                      │
├────────────────────────────────────────┤
│      托管堆 (Managed Heap - C#/Mono)   │
│  类对象 | List | Dictionary | 字符串    │
│  ──────────────────────────────────    │
│  Unity 层分配的内存主要在 Managed Heap  │
└────────────────────────────────────────┘
```

**两层内存：**
1. **Native 层 (C++)**：Unity 引擎内部的物理、动画、渲染等，由 Unity 自己管理。
2. **Managed 层 (C#)**：脚本中的 new 出来的对象，由 GC 管理。

### 5.3 Unity GC 工作流程（传统 Boehm GC）

**阶段 1 — 标记阶段 (Mark Phase)**

GC 从所有**根引用**出发遍历对象图：
- 全局静态引用（如单例 `Instance`）
- 当前活跃的 `MonoBehaviour` 组件
- `FindObjectOfType` 查找到的引用
- `GameObject.GetComponent` 持有的一切引用

```csharp
// Unity 中的根引用示例
public static GameManager Instance { get; private set; }

void Update() {
    var player = FindObjectOfType<Player>(); // 产生 GC 根引用
    if (player != null) {
        // player 及其引用的所有对象标记为存活
    }
}
```

**阶段 2 — 清除阶段 (Sweep Phase)**

Boehm GC 扫描整个堆，释放未被标记的对象：

```
清除前: [存活A] [垃圾] [存活B] [垃圾] [存活C]
         ↓ Sweep
清除后: [存活A] [空闲] [存活B] [空闲] [存活C]
```

> Boehm GC 不做压缩 (Compacting)，会产生内存碎片。

### 5.4 增量 GC (Incremental GC - Unity 2019+)

**传统 GC** 一次性 Stop-The-World 执行整个过程，导致明显卡顿。

**增量 GC** 将 GC 工作**分散到多帧**执行：

```
传统 GC:           [暂停-----GC 全部工作------继续]
                  ↑ 长卡顿

增量 GC:  [暂停-GC分步] [运行] [暂停-GC分步] [运行] [暂停-GC分步]
               ↑ 短暂停   ↑ 短暂停   ↑ 短暂停
```

**启用方式：**
- Unity 2019+: IL2CPP 后端默认支持，需在 Player Settings 中开启 `Use incremental GC`
- 增量 GC 配合 Burst Compiler、Collections Package 可以进一步降低 GC 开销

### 5.5 Unity 中触发 GC 的常见操作

| 触发操作 | 原因 | 优化方向 |
|---------|------|---------|
| `Instantiate()` / `Destroy()` | 分配/释放 GameObject 及组件 | 使用对象池 |
| `FindObjectOfType<T>()` | 遍历场景对象，产生临时枚举器 | 注册表模式替代 |
| `GetComponent<T>()` | 返回组件引用（本身不是分配，但可能触发） | 缓存引用 |
| 字符串拼接（`+` 操作符） | 每次拼接创建新 string 对象 | 使用 `StringBuilder` |
| 协程 `StartCoroutine` | 创建 `Coroutine` 对象及 `IEnumerator` | 池化协程对象 |
| `foreach` 遍历非泛型集合 | 产生枚举器装箱 | 使用 `for` 循环或泛型集合 |
| LINQ 查询（每帧调用） | 产生大量临时对象 | 避免热路径使用 LINQ |
| 装箱操作 | 值类型转 object | 使用泛型 |
| `Delegate` / `event` 加减 | 委托链分配新数组 | 减少动态订阅/取消 ⚠️ |

### 5.6 GC.Collect 的使用

```csharp
// 强制触发 GC（不推荐在游戏运行中频繁调用）
System.GC.Collect();            // 强制 Full GC
System.GC.Collect(0);           // 仅回收 Gen 0（开销最小）
System.GC.Collect(1);           // 回收 Gen 0 + Gen 1

// 典型使用场景：
// 1. 加载完成后清理加载过程中的临时分配
// 2. 切换场景时清理前一个场景的残留对象
// 3. 某些内存敏感的关键节点（如战斗开始前）

// 示例：场景加载后
async Task LoadSceneAsync(string sceneName) {
    await SceneManager.LoadSceneAsync(sceneName);
    System.GC.Collect();        // 清理加载过程中的临时内存
    Resources.UnloadUnusedAssets();
}
```

> ⚠️ 不要在 Update / FixedUpdate 等热路径中调用 GC.Collect，会导致严重卡顿。

### 5.7 Unity Profiler 分析 GC

```csharp
// 代码中打点分析
void Update() {
    UnityEngine.Profiling.Profiler.BeginSample("CombatUpdate");
    // ... 业务逻辑
    UnityEngine.Profiling.Profiler.EndSample();
}

// Profiler 窗口中关注：
// - GC Alloc：每帧 GC 分配量（应尽量为 0）
// - Reserved：GC 预留内存
// - Used：实际使用内存
```

**Profiler 路径：** Window > Analysis > Profiler

---

## 六、内存优化实践

### 6.1 对象池 (Object Pool)

对象池是 Unity 中最核心的内存优化手段，避免频繁 Instantiate / Destroy 导致的 GC 分配。

```csharp
public class BulletPool : MonoBehaviour {
    private Queue<GameObject> pool = new Queue<GameObject>();
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int initialSize = 20;
    
    void Start() {
        for (int i = 0; i < initialSize; i++) {
            GameObject bullet = Instantiate(bulletPrefab);
            bullet.SetActive(false);
            pool.Enqueue(bullet);
        }
    }
    
    public GameObject GetBullet() {
        if (pool.Count > 0) {
            GameObject bullet = pool.Dequeue();
            bullet.SetActive(true);
            return bullet;
        }
        return Instantiate(bulletPrefab); // 池子空了，new 一个
    }
    
    public void ReturnBullet(GameObject bullet) {
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }
}
```

### 6.2 使用 Struct 优化数据

```csharp
// 不好 — class 每个都在堆上分配
public class BulletData {
    public Vector3 position;
    public Quaternion rotation;
    public float speed;
}

// 好 — struct 在栈上或内联分配，无 GC 压力
public struct BulletData {
    public Vector3 position;
    public Quaternion rotation;
    public float speed;
}
```

### 6.3 预分配数组替代 List 动态扩容

```csharp
public class EnemyManager : MonoBehaviour {
    // 不好 — List 自动扩容产生 GC
    // private List<Enemy> enemies = new List<Enemy>();
    
    // 好 — 预分配固定大小数组
    private Enemy[] enemyArray = new Enemy[128];
    private int enemyCount = 0;
    
    public void AddEnemy(Enemy enemy) {
        if (enemyCount >= enemyArray.Length) {
            // 扩容（尽量避免，估算好上限）
            Array.Resize(ref enemyArray, enemyArray.Length * 2);
        }
        enemyArray[enemyCount++] = enemy;
    }
}
```

### 6.4 字符串优化

```csharp
// 不好 — 每帧分配新字符串
void Update() {
    string debugText = "HP: " + currentHp + " / " + maxHp;
    uiText.text = debugText;
}

// 好 — 使用 StringBuilder 复用
private StringBuilder sb = new StringBuilder(64);

void Update() {
    sb.Clear();
    sb.Append("HP: ");
    sb.Append(currentHp);
    sb.Append(" / ");
    sb.Append(maxHp);
    uiText.text = sb.ToString(); // ⚠️ ToString() 仍会分配，注意版本差异
}
```

### 6.5 Span<T> 与 stackalloc

```csharp
// Span<T> 可以在栈上安全地操作连续内存
Span<int> buffer = stackalloc int[64];  // 栈上分配，零 GC

// 切片操作无分配
Span<int> slice = buffer.Slice(0, 32);

// 配合 System.Buffers 使用
// ⚠️ Span<T> 是 ref struct，不能作为类字段、不能装箱、不能用于异步方法
```

### 6.6 Unity 2021+ 优化新特性

**1. Burst Compiler**

```csharp
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
struct MyJob {
    public NativeArray<float> data;
    
    public void Execute() {
        for (int i = 0; i < data.Length; i++) {
            // Burst 编译为高效 SIMD 代码
            data[i] = math.sqrt(data[i]);
        }
    }
}
```

**2. Collections Package（NativeContainer）**

```csharp
using Unity.Collections;

// 非托管容器，不与 GC 交互
NativeList<int> nativeList = new NativeList<int>(Allocator.Persistent);

// 使用完后手动释放
nativeList.Dispose();
```

### 6.7 面试回答框架

> **问题：Unity 的 GC 是什么？怎么优化？**

**回答要点：**
1. Unity 使用 **Boehm GC**（传统 Mono）或**增量 GC**（Unity 2019+/IL2CPP），负责自动回收托管堆内存。
2. Boehm GC 是保守式的、不分代的 GC，STW 时间长且可能产生内存碎片。增量 GC 将工作分摊到多帧，减少卡顿。
3. **分层内存**：Native 层（C++ 引擎）和 Managed 层（C# 脚本）。C# 中 new 的对象在托管堆，由 GC 管理；Unity 引擎内部内存由 Native 层管理。
4. **生命周期管理**：MonoBehaviour 的 `new` → Update 中频繁分配 → GC 触发 → 回收。应避免高频 new。
5. **优化手段**：对象池、struct 替代 class、预分配数组、StringBuilder、Span<T>、泛型集合、减少装箱。
6. 通过 **Unity Profiler** 查看 `GC Alloc` 指标和 `GC.Collect` 频繁程度。

---

## 七、unsafe 代码与指针

### 7.1 unsafe 上下文

```csharp
unsafe {
    int x = 42;
    int* ptr = &x;    // 获取 x 的地址
    *ptr = 100;       // 通过指针修改值
    Console.WriteLine(x); // 100
}
```

### 7.2 多线程中栈地址的可见性

⚠️ 栈变量地址仅在同一线程内有效。将栈变量的指针传递给其他线程是不安全的。

```csharp
// 不安全 — 其他线程访问了本线程的栈地址
unsafe {
    int x = 42;
    // x 在栈上，地址只在当前线程有效
    new Thread(() => {
        Console.WriteLine($"From other thread: {*(int*)(&x)}");
        // ⚠️ 未定义行为：x 的栈帧可能已被回收
    }).Start();
    Thread.Sleep(100);
}
```

### 7.3 闭包捕获的"栈提升"

```csharp
void Demo() {
    int a = 100;
    Task.Run(() => {
        Console.WriteLine(a);  // a 被闭包捕获 → 提升到堆上
    });
}
// 编译器将 a 提升为匿名类的字段，存储在堆上
```

---

## 八、常见问题与陷阱

| 问题 | 原因 | 解决 |
|------|------|------|
| **StackOverflowException** | 递归过深或 stackalloc 过大 | 检查递归终止条件，限制 stackalloc 大小 |
| **OutOfMemoryException** | 托管堆耗尽 | 检查 LOH 分配（>85000 字节）、内存泄漏 |
| **GC 卡顿** | 高频 new 导致 GC 频繁触发 | 对象池、struct 替代 class、预分配 |
| **InvalidCastException** | 拆箱类型不匹配 | 拆箱时断言类型，使用 `is` / `as` 模式匹配 |
| **LOH 碎片** | 频繁分配和释放大对象 | 复用大数组，使用 ArrayPool |

```csharp
// 使用 ArrayPool 复用大数组（减少 LOH 分配）
byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
try {
    // 使用 buffer...
} finally {
    ArrayPool<byte>.Shared.Return(buffer);
}
```

---

## 相关话题

- [[cpp-comparison|C++ 与 C# 对比]] — C++ 手动内存管理 vs C# GC
- 值类型与引用类型（见本文第二章）
- 装箱拆箱优化（见本文第三章）
- Unity Profiler 使用指南
