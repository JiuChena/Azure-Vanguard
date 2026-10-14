---
tags:
  - csharp
  - cpp
  - comparison
  - programming
created: 2026-06-20
---

# C++ 与 C# 对比

## 一、语言概述与历史

### 1.1 C++

**C++** 由 Bjarne Stroustrup 于贝尔实验室开发。最初名为 "C with Classes"，融合了 C 语言的底层能力和 Simula67 的面向对象特性。

**核心设计理念（C++ 原理）：**
- 程序应简单、面向对象、易于理解
- 开发应在稳健且安全的环境中进行
- 代码应遵循特定架构且必须可移植
- 代码应易于"解释和动态处理"

**语言类型：** 中级语言（封装高级和低级语言特性），可直接操作硬件。

### 1.2 C#

**C#** 由 Anders Hejlsberg 主导开发，1999 年启动（最初代号 "Cool"），2000 年在 .NET 开发者大会上正式发布。

**核心设计理念（C# 开发原理）：**
- 应该是简单、现代、通用、面向对象的编程语言
- 语言和实现应支持软件工程原则
- C# 是构建托管和嵌入式系统应用程序的理想选择

**语言类型：** 高级语言，运行在 .NET 平台之上。

> "C Sharp" 这个名字的灵感来自于音乐符号，`#` 表示音符的音高必须高出半音。

---

## 二、编译流程对比

### 2.1 C++ 编译流程

C++ 编译为**原生机器码 (Native Code)**，流程分为四个阶段：

```
源代码 (.cpp/.h)
    │
    ▼
【1. 预处理 (Preprocessing)】
    │  宏展开、头文件包含 (#include)、条件编译 (#ifdef)
    ▼
【2. 编译 (Compilation)】
  词法分析 (tokens) → 语法分析 (AST) → 语义分析 → 中间代码生成
    │
    ▼
【3. 汇编 (Assembly)】
  中间代码 → 汇编代码 (.s) → 目标文件 (.o / .obj)
    │  段分布：
    │  - .text (代码段)
    │  - .data (已初始化数据段)
    │  - .bss  (未初始化数据段)
    │  - 字符串池、符号表
    ▼
【4. 链接 (Linking)】
    │  符号解析、地址重定位、段合并
    │  - 静态链接: 将 .a / .lib 合并到可执行文件
    │  - 动态链接: 引用 .so / .dll，运行时解析（PLT/GOT）
    ▼
可执行文件 (ELF / PE / Mach-O)
```

#### 链接详解

**静态库 (.a / .lib)**

```bash
# 编译各源文件为目标文件
gcc -c util1.c -o util1.o
gcc -c util2.c -o util2.o

# 打包为静态库
ar rcs libutil.a util1.o util2.o

# 链接到最终可执行文件
gcc main.o libutil.a -o app
# main.o 中引用的函数直接嵌入 app，无外部依赖
```

**动态库 (.so / .dll)**

```bash
# 编译为动态库（位置无关代码）
gcc -shared -fPIC util1.o util2.o -o libutil.so

# 链接
gcc main.o -L. -lutil -o app

# 运行时指定库搜索路径
export LD_LIBRARY_PATH=.:$LD_LIBRARY_PATH
```

**PLT/GOT 延迟绑定 (Lazy Binding)**

```
call printf@plt → jmp *printf@got → (首次) 动态链接器解析地址
                                  → (之后) 直接跳转到 printf
```

**LTO (Link Time Optimization)**

```bash
# 链接时优化，跨编译单元内联
gcc -flto -O3 file1.c file2.c -o app
```

**符号版本控制**

```bash
# version.map 控制导出的符号版本
gcc -Wl,--version-script=version.map ...
```

#### C++ 模板编译

```cpp
// 模板：编译时展开，生成具体类型的代码
template<typename T>
T max(T a, T b) {
    return a > b ? a : b;
}

template<typename T, size_t N>
class Array {
    T data[N];
public:
    constexpr size_t size() const { return N; }
    T& operator[](size_t i) { return data[i]; }
};

// 特化：为特定类型定制
template<>
class Array<bool, 8> {
    unsigned char bits;
    // 位压缩存储
};

// 偏特化：为指针类型定制
template<typename T>
class Array<T*, 10> {
    // 指针专属逻辑
};

// 可变参数模板 (C++11)
template<typename... Args>
void print(Args... args) {
    ((std::cout << args << " "), ...);  // C++17 折叠表达式
}
```

**模板实例化控制：**

```cpp
// 显式实例化
template class Array<int, 100>;
extern template class Array<double, 5>;  // 声明在其他翻译单元实例化
```

### 2.2 C# 编译流程

C# 编译为 **IL (中间语言)**，由 CLR (公共语言运行时) 的 JIT (即时编译器) 在运行时编译为原生代码。

```
源代码 (.cs)
    │
    ▼
【C# 编译器 (Roslyn)】
    │ 源文件 → CIL (Common Intermediate Language) / MSIL
    ▼
程序集 (.dll / .exe) — 包含 IL 代码与元数据
    │
    ▼
【运行时 — CLR (Common Language Runtime)】
    │
    ├── JIT 编译 (Just-In-Time):
    │   首次调用方法时，将 IL 编译为原生代码
    │   (各方法独立编译，缓存结果)
    │
    ├── GC (Garbage Collection):
    │   自动管理托管堆内存
    │
    └── 类型加载、安全检查、异常处理 ...
```

**CLR 的核心职责：**

1. **代码管理**：加载 IL 代码
2. **JIT 编译**：将 MSIL (Microsoft Intermediate Language) 即时编译为原生代码
3. **内存管理**：GC 自动回收
4. **线程管理**：托管线程池
5. **安全检查**：代码访问安全 (CAS)、类型安全验证
6. **异常处理**：跨语言异常机制
7. **互操作**：P/Invoke 调用原生代码、COM 互操作

**.NET Framework vs Mono vs .NET Core：**

| 运行时 | 平台 | 说明 |
|--------|------|------|
| **.NET Framework** | Windows | 最初的 .NET 实现，CLR 运行在 Windows |
| **Mono** | 跨平台 | .NET Framework 的第三方跨平台实现，Unity 早期依赖 Mono |
| **.NET Core / .NET 5+** | 跨平台 | 官方跨平台运行时，性能更好 |

### 2.3 Unity 中的 C# 编译方式

Unity 内嵌了一个 Mono 运行时，但在构建时可以将 IL 转换为 C++：

```
Unity 编辑器中 (开发阶段)
    C# 源代码 → Mono → IL → Mono VM 解释执行

Unity 构建时 (发布阶段)
    C# 源代码 → IL → IL2CPP → C++ 代码 → 原生编译器 → 原生机器码
    
    (IL2CPP VM: 精简的运行时，替代 Mono VM)
```

**IL2CPP** 的工作原理：
- 将 IL 代码**翻译为 C++ 代码**
- 再用平台原生 C++ 编译器编译为原生代码
- 使用 IL2CPP VM 提供 GC、线程等运行时服务

> **性能对比：** IL2CPP 生成的代码比 Mono JIT 更快，尤其在 iOS 等不允许 JIT 的平台上。
> IL2CPP VM 比 Mono VM 更轻量，无 JIT 开销，但构建时间更长。

**跨平台原理：**
1. C# 编译器在任何平台上生成相同的 IL
2. IL2CPP 将 IL 转为 C++（平台无关）
3. 平台原生编译器将 C++ 转为目标平台原生代码

---

## 三、内存管理对比

### 3.1 总览

| 维度 | C++ | C# |
|------|-----|-----|
| **管理方式** | 手动管理 | 自动 GC |
| **分配** | `new` / `malloc` | `new` 关键字 |
| **释放** | `delete` / `free` | GC 自动回收 |
| **RAII** | 核心惯用法（析构函数确定性地释放） | `IDisposable` + `using` 语句 |
| **智能指针** | `unique_ptr`, `shared_ptr`, `weak_ptr` | GC 自动追踪引用 |
| **内存泄漏** | 容易发生（忘记 delete/free） | 理论上不会（GC 回收），但存在"托管泄漏"（被无意持有引用） |
| **碎片** | 需自行管理池 | GC 压缩整理 |
| **确定性** | 完全确定（析构函数） | 非确定（GC 触发时机不确定） |

### 3.2 C++ — new / delete vs malloc / free

#### 核心区别

| 维度 | `malloc` / `free` | `new` / `delete` |
|------|------------------|-----------------|
| **来源** | C 标准库 (`<stdlib>`) | C++ 关键字/操作符 |
| **返回类型** | `void*`，需手动类型转换 | 类型安全，返回正确类型指针 |
| **构造函数** | **不调用** | **调用**构造函数 / 析构函数 |
| **失败行为** | 返回 `NULL` | 抛出 `std::bad_alloc`；`new (std::nothrow)` 返回 `nullptr` |
| **大小计算** | 手动计算字节数 | 编译器自动计算 |
| **可重载性** | 不可重载 | 可重载（全局/类级别） |

```cpp
// malloc / free — C 风格
MyClass* p1 = (MyClass*)malloc(sizeof(MyClass));  // 不调用构造函数
// ... 手动初始化 ...
free(p1);  // 不调用析构函数

// new / delete — C++ 风格
MyClass* p2 = new MyClass();     // 调用构造函数
delete p2;                       // 调用析构函数，然后释放内存

// new[] / delete[] — 数组
MyClass* arr = new MyClass[10];  // 调用 10 次构造函数
delete[] arr;                    // 调用 10 次析构函数
```

> **黄金规则：** `malloc` 对应 `free`，`new` 对应 `delete`，`new[]` 对应 `delete[]`。混用导致未定义行为。

#### delete 如何知道释放多大内存？

```cpp
// 单个对象：new 在对象内存前额外分配一个头部 (cookie) 记录大小
MyClass* p = new MyClass();
// 实际内存布局: [Header: size | other] [MyClass 对象]
//                                    ↑ p 指向这里
delete p;  // free(p - header_size)，从头部读取大小信息

// 数组：new[] 在数组前额外记录元素个数
MyClass* arr = new MyClass[N];
// 实际内存布局: [Header: count=N] [obj0] [obj1] ... [objN-1]
//                                    ↑ arr 指向这里
delete[] arr;  // 读取 N，调用 N 次析构函数，然后释放全部内存
```

### 3.3 C++ 智能指针 (C++11+)

RAII (Resource Acquisition Is Initialization) 理念：用对象生命周期管理资源。

#### unique_ptr — 独占所有权

```cpp
#include <memory>

// 独占所有权，不可复制，只能移动
std::unique_ptr<MyClass> p1 = std::make_unique<MyClass>();
// std::unique_ptr<MyClass> p2 = p1;            // 编译错误！不能复制
std::unique_ptr<MyClass> p2 = std::move(p1);    // p1 变为 nullptr，p2 接管

// 自定义删除器
auto deleter = [](MyClass* p) { /* 自定义清理 */ delete p; };
std::unique_ptr<MyClass, decltype(deleter)> p3(new MyClass(), deleter);

// 离开作用域 → 自动调用删除器释放对象
```

| 特性 | 说明 |
|------|------|
| 所有权 | 独占（唯一） |
| 引用计数 | **无** — 零开销 |
| 可复制 | 否 |
| 可移动 | 是 (`std::move`) |
| 使用场景 | 明确唯一所有者，无循环引用风险 |

#### shared_ptr — 共享所有权

```cpp
// 共享所有权，引用计数
std::shared_ptr<MyClass> p1 = std::make_shared<MyClass>();
std::shared_ptr<MyClass> p2 = p1;            // use_count = 2
std::shared_ptr<MyClass> p3 = p1;            // use_count = 3

// 每次拷贝 use_count + 1
// 每次析构 use_count - 1
// use_count 归零时释放对象

p1.reset();  // use_count - 1，p1 变为空
// p2, p3 仍有效
```

```
shared_ptr 内存结构:

  shared_ptr p1 ──→ ┌──────────────┐
  shared_ptr p2 ──→ │ 控制块        │
                     │ - use_count  │ ──→ [MyClass 对象]
                     │ - weak_count │
                     │ - deleter    │
                     └──────────────┘
```

| 特性 | 说明 |
|------|------|
| 所有权 | 共享 |
| 引用计数 | **有** — `use_count` |
| 开销 | 控制块分配 + 原子操作 |
| 循环引用 | **会泄漏**（需 weak_ptr 打破） |
| 使用场景 | 多个所有者共享同一对象 |

#### weak_ptr — 弱引用

```cpp
// weak_ptr 不增加 use_count，用于打破循环引用
std::shared_ptr<Node> nodeA = std::make_shared<Node>();
std::shared_ptr<Node> nodeB = std::make_shared<Node>();

// 错误 — 循环引用导致泄漏
// nodeA->parent = nodeB; nodeB->child = nodeA; (如果都是 shared_ptr)

// 正确 — 使用 weak_ptr
class Node {
public:
    std::weak_ptr<Node> parent;   // 弱引用父节点
    std::shared_ptr<Node> child;  // 共享所有权持有子节点
};

nodeA->child = nodeB;        // nodeB use_count++
nodeB->parent = nodeA;       // weak_ptr 不增加 use_count（不增加 use_count!）

// lock() 方法：尝试获取 shared_ptr
if (auto sp = nodeB->parent.lock()) {
    // 对象仍然存活，sp 是一个合法的 shared_ptr (use_count 临时 +1)
    sp->DoSomething();
} else {
    // 对象已被回收
}
```

```
weak_ptr 与 shared_ptr 的引用计数关系:

use_count:   shared_ptr 的数量 → 归零时释放对象
weak_count:  weak_ptr 的数量 → 归零时释放控制块

注意：use_count = 0 时对象即被释放；但控制块保留到 weak_count = 0 才释放。
这使得 lock() 可以安全判断对象是否存活。
```

#### 三种智能指针对比表

| 维度 | `unique_ptr` | `shared_ptr` | `weak_ptr` |
|------|-------------|-------------|-----------|
| 所有权 | 独占（唯一） | 共享 | 无（观察） |
| 引用计数 | 无 | 有 (use_count) | 不增加 use_count |
| 可复制 | 否 | 是（拷贝 +1） | 是 |
| 可移动 | 是 | 是 | 是 |
| 释放方式 | 离开作用域自动 delete | use_count 归零后 delete | 配合 shared_ptr |
| 循环引用 | 不会 | **会泄漏** | 用于打破循环引用 |
| 开销 | 零开销 | 控制块 + 原子操作 | 极小 |

> **面试总结：** 优先使用 `unique_ptr`（零开销），需要共享时用 `shared_ptr`，打破循环引用用 `weak_ptr`。

### 3.4 C# — GC 自动管理

详见 [[memory-gc|内存管理与垃圾回收]]

```csharp
// C# 中的 "GC 版本" 对比 C++ 智能指针：

// unique_ptr ≈ 单一引用（GC 自动判断，无需显式管理）
// shared_ptr ≈ GC 追踪的引用（但 GC 不基于引用计数，而是基于可达性分析）
// weak_ptr ≈ WeakReference<T>
WeakReference<MyClass> weakRef = new WeakReference<MyClass>(obj);
if (weakRef.TryGetTarget(out var target)) {
    target.DoSomething();
}
```

---

## 四、语言特性对比

### 4.1 编译 vs 解释

| 维度 | C++ | C# |
|------|-----|-----|
| **编译目标** | 原生机器码 | IL (中间语言) → JIT → 原生码 |
| **运行方式** | 直接 CPU 执行 | CLR 托管运行 |
| **平台依赖** | 需为每个平台重新编译 | IL 可跨平台（CLR 适配各平台） |
| **性能特性** | 峰值性能更高 | JIT 编译有预热开销，但 JIT 可做运行时特化优化 |
| **部署** | 可单文件静态链接 | 依赖运行时（.NET Runtime） |

### 4.2 内存模型

| 维度 | C++ | C# |
|------|-----|-----|
| **分配** | `new` / `malloc`，需手动 `delete` / `free` 配对 | `new` 分配到托管堆，GC 自动回收 |
| **GC 支持** | 无（可用 Boehm GC 库，非标准） | 内置 GC |
| **栈上对象** | 直接声明即可 (`MyClass obj;`) | 仅值类型可以在栈上（class 始终在堆上） |
| **确定性析构** | 是（离开作用域立即析构） | 否（GC 不确定时机；`IDisposable` + `using` 提供确定性资源释放） |

### 4.3 多重继承 vs 接口

```cpp
// C++ 支持多重继承
class Drawable {
public:
    virtual void Draw() = 0;
};

class Serializable {
public:
    virtual void Serialize() = 0;
};

class UIElement : public Drawable, public Serializable {
public:
    void Draw() override { /* ... */ }
    void Serialize() override { /* ... */ }
};
// ⚠️ 菱形继承问题需虚拟继承解决
```

```csharp
// C# 不支持多重继承，使用接口实现多行为组合
interface IDrawable {
    void Draw();
}

interface ISerializable {
    void Serialize();
}

class UIElement : IDrawable, ISerializable {
    public void Draw() { /* ... */ }
    public void Serialize() { /* ... */ }
}

// 接口"Can-Do"关系：表示"能做某事"
// 抽象类"Is-A"关系：表示"是什么"
// 接口支持属性、事件、索引器（C# 8.0+ 支持默认实现）
```

**接口 vs 抽象类对比：**

| 维度 | 接口 (Interface) | 抽象类 (Abstract Class) |
|------|-----------------|------------------------|
| **关系** | "Can-Do"（能做） | "Is-A"（是什么） |
| **实现** | 纯契约，无实现（C# 8+ 可默认实现） | 可有部分实现 |
| **成员** | 方法/属性/事件/索引器 | 字段/方法/属性/构造函数 |
| **访问修饰符** | 默认 public（不允许其他） | 支持 public/protected 等 |
| **多继承** | 可实现多个接口 | 只能继承一个抽象类 |
| **构造函数** | 无 | 有（供子类调用） |

### 4.4 模板 vs 泛型

| 维度 | C++ 模板 | C# 泛型 |
|------|---------|--------|
| **展开方式** | **编译时**完全展开（代码膨胀，但零运行时开销） | **运行时**，CLR 保留泛型类型信息 |
| **约束** | 隐式（duck typing），C++20 Concepts 增加显式约束 | 显式 `where T : class / struct / new() / IInterface` |
| **特化** | 支持完全特化、偏特化 | 不支持特化 |
| **值类型** | 原生的值语义 | 自动处理，值类型避免装箱 |
| **非类型参数** | 支持（`template<int N>`） | 不支持（需传入参数） |
| **代码重用** | 每个实例化生成独立代码 | 引用类型共享 JIT 代码；值类型各自特化 |

```cpp
// C++ 模板 — 编译时多态
template<typename T>
T Max(T a, T b) {
    return a > b ? a : b;
}

// 非类型模板参数
template<typename T, size_t N>
class Array {
    T data[N];
};

// 特化
template<>
class Array<bool, 8> {
    unsigned char bits;
};
```

```csharp
// C# 泛型 — 运行时多态
T Max<T>(T a, T b) where T : IComparable<T> {
    return a.CompareTo(b) > 0 ? a : b;
}

// 泛型约束
void Process<T>(T item) where T : class, IDisposable, new() {
    using (item) { /* ... */ }
}

// 不支持非类型参数，不支持模板特化
```

### 4.5 委托 vs 函数指针

C# 中的委托（Delegate）是类型安全的函数指针封装，支持**多播**（链式调用）。

```csharp
// C# 委托 — 类型安全的函数指针，支持多播
delegate void AttackDelegate(int damage);

AttackDelegate attackChain = MeleeAttack;
attackChain += PlaySound;         // 添加
attackChain += SpawnVFX;          // 再添加
attackChain?.Invoke(50);          // 顺序调用所有方法

// 执行流程:
// Player → AttackDelegate Chain → MeleeAttack(50)
//                               → PlaySound()
//                               → SpawnVFX()

// 多播委托内部维护调用列表 (invocation list)
// 基类是 System.MulticastDelegate（继承自 System.Delegate）
```

```cpp
// C++ 函数指针 / std::function — 无内置多播
#include <functional>
#include <vector>

std::function<void(int)> callback = [](int dmg) { /* ... */ };

// 实现多播需手动管理
std::vector<std::function<void(int)>> callbacks;
callbacks.push_back(MeleeAttack);
callbacks.push_back(PlaySound);
for (auto& cb : callbacks) cb(50);
```

| 维度 | C++ | C# |
|------|-----|-----|
| **机制** | 函数指针 / `std::function` / Lambda | Delegate / `Action` / `Func` |
| **多播** | 需手动管理 | 内置（`MulticastDelegate`） |
| **类型安全** | 是 | 是 |
| **匿名函数** | Lambda (C++11+) | Lambda / 匿名方法 |
| **闭包捕获** | 显式捕获列表 `[=]`, `[&]` | 编译器自动处理 |

### 4.6 STL vs .NET 集合

| 数据结构 | C++ STL | C# .NET |
|---------|---------|--------|
| **动态数组** | `std::vector<T>` | `List<T>` |
| **双向链表** | `std::list<T>` | `LinkedList<T>` |
| **有序字典** | `std::map<K,V>` (红黑树) | `SortedDictionary<K,V>` |
| **哈希字典** | `std::unordered_map<K,V>` | `Dictionary<K,V>` |
| **栈** | `std::stack<T>` | `Stack<T>` |
| **队列** | `std::queue<T>` | `Queue<T>` |
| **双端队列** | `std::deque<T>` | `LinkedList<T>`（非精确等价） |
| **集合** | `std::set<T>` | `HashSet<T>` |

```cpp
// C++ STL 示例
#include <vector>
#include <map>
#include <unordered_map>

std::vector<int> vec;
vec.push_back(42);
vec.reserve(100);

std::map<std::string, int> orderedMap;           // 红黑树，有序
std::unordered_map<std::string, int> hashMap;    // 哈希表，O(1)
```

```csharp
// C# .NET 集合示例
List<int> list = new List<int>();
list.Add(42);

Dictionary<string, int> dict = new Dictionary<string, int>();  // 哈希表
SortedDictionary<string, int> sorted = new SortedDictionary<string, int>(); // 红黑树
```

### 4.7 其他关键特性对比

| 特性 | C++ | C# |
|------|-----|-----|
| **指针** | 完整支持，任意使用 | `unsafe` 代码块中可用，不推荐 |
| **引用参数** | `&` (引用), `*` (指针) | `ref`, `out`, `in` |
| **const 正确性** | 完善（`const int*`, `int* const`） | 无 const 成员函数，仅 `readonly` |
| **switch 语句** | 仅整数/枚举 | 整数、字符串、模式匹配 (C# 7+) |
| **foreach** | C++11 范围 for | 内置 `foreach` |
| **预处理指令** | 完整宏系统 (`#define`, `#ifdef`) | 仅条件编译 (`#if DEBUG`，无宏) |
| **运算符重载** | 支持 | 支持 |
| **属性** | 无语言级属性 | 内置 `get`/`set` 属性 |
| **事件** | 无语言级事件 | `event` 关键字 |
| **异常处理** | `try`/`catch`/`throw` | `try`/`catch`/`finally`/`throw` |

---

## 五、异常处理对比

### C++ 异常与 RAII

```cpp
// C++ RAII: 离开作用域时自动调用析构函数（确定性）
void riskyFunction() {
    Resource r1("first");   // 构造
    Resource r2("second");  // 构造
    
    if (errorCondition) {
        throw std::runtime_error("error");
        // r2 先析构，r1 后析构（栈展开，自动）
    }
    
    // 正常退出：同样 r2 先析构，r1 后析构
}
// 编译器生成的伪代码：
// try_begin:
//   construct r1
//   register_cleanup(r1.~Resource)
//   construct r2
//   register_cleanup(r2.~Resource)
// exception_handler:
//   call_registered_cleanups()  // 逆序析构
```

### C# 异常处理

```csharp
// C# IDisposable + using = 确定性资源释放
void RiskyMethod() {
    using (var r1 = new Resource("first"))
    using (var r2 = new Resource("second")) {
        if (errorCondition) {
            throw new Exception("error");
            // r2.Dispose() 和 r1.Dispose() 自动调用
        }
    }
}

// 等价于：
var r1 = new Resource("first");
try {
    var r2 = new Resource("second");
    try {
        // ...
    } finally {
        r2.Dispose();
    }
} finally {
    r1.Dispose();
}
```

| 维度 | C++ | C# |
|------|-----|-----|
| **资源管理** | RAII（析构函数） | `IDisposable` + `using` |
| **确定性** | 完全确定 | `using` 块内确定 |
| **finally** | 无（析构函数代替） | 有 `finally` 块 |
| **异常开销** | 零开销模型（正常路径无成本） | 有运行时开销 |

---

## 六、性能特性对比

| 维度 | C++ | C# |
|------|-----|-----|
| **峰值运行速度** | 更高（直接编译为原生代码） | 略低（JIT 编译 + GC 开销） |
| **启动速度** | 快（无运行时初始化） | 较慢（CLR 加载 + JIT 预热） |
| **内存占用** | 更小（可精确控制） | 较大（运行时 + GC 预留） |
| **缓存友好** | 手动控制内存布局 | 托管堆不可控布局 |
| **SIMD** | 编译器自动向量化 / intrinsics | 需 Burst Compiler 或显式 intrinsics |
| **二进制大小** | 较小（无运行时） | 较大（需包含运行时或 IL2CPP 生成） |

**为什么 C++ 通常更快：**
1. 直接编译为原生码，无中间层
2. 无 GC 暂停 — 内存管理完全由程序员控制
3. 更好的缓存局部性（可精确控制内存布局）
4. 零开销抽象（模板展开、内联优化）
5. 可做平台特定的 SIMD/汇编优化

**C# 的优势：**
1. 开发效率高（GC 减少内存 bug）
2. JIT 可在运行时做特化优化（PGO — Profile Guided Optimization）
3. 内存安全（无野指针、缓冲区溢出）
4. 跨平台部署简便（IL2CPP 或 .NET Runtime）

---

## 七、实际应用场景

| 场景 | 推荐语言 | 原因 |
|------|---------|------|
| **游戏引擎底层** | C++ | 极致性能，硬件直控 |
| **游戏逻辑/脚本** | C# (Unity) | 开发效率，快速迭代 |
| **操作系统/驱动** | C++/C | 底层硬件交互 |
| **桌面应用** | C# | WinForms/WPF/MAUI 生态 |
| **服务器后端** | C++ (高频交易) / C# (企业应用) | 性能 vs 开发效率权衡 |
| **Web/云服务** | C# (ASP.NET) | 成熟的 Web 生态 |
| **嵌入式系统** | C++ | 资源受限，需精确控制 |
| **移动端** | C# (Unity/Xamarin/MAUI) | 跨平台开发效率 |

---

## 八、面试常见问题

### C++ 高频题

1. **new/delete 和 malloc/free 的区别？** → 见本文 3.2 节
2. **shared_ptr, unique_ptr, weak_ptr 的原理和适用场景？** → 见本文 3.3 节
3. **std::vector 的 push_back 扩容机制？reserve 和 resize 区别？**
4. **map 和 unordered_map 的区别？（红黑树 vs 哈希表）**
5. **const 的各种用法？（`const int*`, `int* const`, `const` 成员函数）**
6. **C++ 编译链接过程？（预处理 → 编译 → 汇编 → 链接）**
7. **虚函数实现原理？（虚函数表 vtable）**
8. **placement new 的作用？**
9. **extern "C" 的作用？**
10. **static_cast, dynamic_cast, const_cast, reinterpret_cast 区别？**

### C# 高频题

1. **值类型和引用类型的区别？** → 见 [[memory-gc#二、值类型与引用类型内存布局]]
2. **装箱和拆箱是什么？如何优化？** → 见 [[memory-gc#三、装箱与拆箱]]
3. **GC 原理和优化？** → 见 [[memory-gc#四、GC 原理]]
4. **接口和抽象类的区别？**
5. **委托和事件的区别？**
6. **C# 编译流程？（IL → JIT → 原生）**
7. **泛型和 C++ 模板的区别？**
8. **`using` 语句的作用？**
9. **as/is 和强制类型转换的区别？**

---

## 相关话题

- [[memory-gc|内存管理与垃圾回收]] — C# GC 详解
- Unity IL2CPP 编译原理
- C# 值类型与引用类型
- 泛型与模板深度对比

---

> ⚠️ 本文部分内容基于 OCR 识别结果重新整理，可能存在遗漏或不够精确之处，建议结合实际代码验证关键细节。
