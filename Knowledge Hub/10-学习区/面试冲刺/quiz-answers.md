---
tags: [quiz, answers, quick-reference]
created: 2026-06-20
updated: 2026-06-20
---

# 面试题答案速查

> 与 `quiz-review-tracker.md` 配套，每题附带标准答案，复习时直接看。

---

## 🔴 明天到期（6/21）

### Q1: 装箱与拆箱 (60分)
**装箱**: 值类型 → object 隐式转换，CLR 在堆上分配内存拷贝值
**拆箱**: object → 值类型显式转换，必须用**完全正确的原始类型**，否则 `InvalidCastException`
**性能代价**: 装箱每次堆分配 → 触发GC；拆箱有类型检查开销
**优化**: 泛型集合替代 ArrayList、缓存、避免 `string.Format` 旧版装箱

### Q2: GC 触发时机和分代原理 (55分)
**触发时机**: ① Gen 0 内存预算耗尽（最常见）② GC.Collect() 手动 ③ 系统内存不足
**三代**: Gen 0（新对象，最频繁回收）→ Gen 1（Gen 0 幸存者，缓冲区）→ Gen 2（长命对象，最少回收）
**分代优势**: 基于"代际假说"——新对象越容易死，只扫 Gen 0 避免全堆扫描
**面试金句**: "GC 建立在代际假说之上：对象越新越容易死。所以优先回收 Gen 0，既快又高效。"

### Q3: TCP 三次握手 (70分)
```
客户端 → SYN → 服务端         ① 客户端：我要连你
客户端 ← SYN+ACK ← 服务端     ② 服务端：收到，同意
客户端 → ACK → 服务端          ③ 客户端：确认
```
**为什么不是两次**: 防止历史失效连接请求到达服务端后直接建立连接，造成资源浪费

### Q4: 帧同步 vs 状态同步 (65分)
| | 帧同步 (Lockstep) | 状态同步 |
|---|-----------------|---------|
| 原理 | 客户端只发输入指令，各自计算 | 服务端权威计算，下发结果 |
| 带宽 | 极低（只传输入） | 较高（需同步对象状态） |
| 延迟敏感 | 极高（一人卡全卡） | 中等（预测+插值可缓解） |
| 典型 | RTS（星际）、格斗 | MMO（魔兽）、FPS（守望） |
**面试金句**: "现代大项目很少纯用一种，MOBA 是帧同步思想+可靠传输，FPS 是状态同步+客户端预测+服务器校对。"

### Q5: TCP vs UDP 游戏场景 (60分)
| | TCP | UDP |
|---|-----|-----|
| 连接 | 面向连接 | 无连接 |
| 可靠 | 确认+重传 | 不保证送达 |
| 顺序 | 严格有序 | 可能乱序 |
| 头部 | 20字节 | 8字节 |
| 游戏 | MOBA(王者)、MMO | FPS(Valorant)、RTS帧同步输入、语音 |
**纠正**: 不是"都用UDP"！MOBA常用可靠UDP或TCP，MMO用TCP，FPS用不可靠UDP+自定义可靠层。

### Q6: SOLID 原则 (10分)
| 字母 | 原则 | 一句话 |
|------|------|--------|
| S | 单一职责 SRP | 一个类只负责一件事 |
| O | 开闭原则 OCP | 对扩展开放，对修改关闭 |
| L | 里氏替换 LSP | 子类必须能完全替代父类 |
| I | 接口隔离 ISP | 接口要小而精 |
| D | 依赖倒置 DIP | 依赖抽象不依赖具体 |
**记忆**: Single → Open → Liskov → Interface → Depend

---

## 🟡 后天到期（6/23）

### Q7: MonoBehaviour 生命周期 (70分)
```
完整顺序: Reset(仅编辑器) → Awake → OnEnable → Start → FixedUpdate → Update → LateUpdate → OnDisable → OnDestroy
```
**关键**: Awake 在 Start 之前；OnEnable 每次激活都调；Start 只在 enabled=true 时调一次；FixedUpdate 默认 0.02s
**漏分点**: 漏说了 OnEnable/OnDisable 的配对关系，以及 OnDisable 的调用时机

### Q8: 单例模式 (75分)
```csharp
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }
    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```
**坑**: ① 必须 DontDestroyOnLoad 跨场景 ② Awake 中检查重复实例并 Destroy ③ 测试困难（全局状态）
**漏分点**: 没写具体代码，没说 DDOL 和重复实例处理

### Q9: 协程 GC 陷阱 (75分)
```csharp
// ❌ 每帧 GC Alloc
yield return new WaitForSeconds(1f);

// ✅ 缓存复用
WaitForSeconds wait = new WaitForSeconds(1f);
yield return wait;
```
**协程终止条件**: ① GameObject Disable/Destroy ② 脚本 enabled=false ③ StopCoroutine/StopAllCoroutines

### Q10: Animator Transition (70分)
| 参数 | 含义 | 调不好的Bug |
|------|------|-----------|
| Has Exit Time | 当前动画必须播到一定比例才允许切走 | 勾了→技能延迟；不勾→动作变形 |
| Transition Duration | 两个动画间的融合时长 | 太长→拖泥带水；太短→动作突变 |

### Q11: C++ shared_ptr (30分)
```cpp
// shared_ptr 内部：两个指针
// 1. T* ptr → 指向对象
// 2. control_block* → 引用计数 + 弱引用计数 + 删除器
// 每次拷贝 → shared_count++，每次析构 → shared_count-- → 归零 delete
```
**unique_ptr 更快原因**: 只需要一个指针（无控制块），无原子操作
**面试**: 简历已标注 C++ 基础，被深问概率低，知道引用计数即可

### Q12: UGUI Canvas Render Mode (75分)
| 模式 | UI 位置 | 3D遮挡 | 适用 |
|------|--------|--------|------|
| Overlay | 屏幕最上层 | 永不遮挡 | HUD、菜单 |
| Camera | 屏幕空间绑相机 | 可被遮挡 | 角色名字、标记 |
| World Space | 世界坐标 | 像3D物体 | VR UI、头顶血条 |

### Q13: Lua 八大基本类型（🆕）

| 类型 | 说明 | 示例 |
|------|------|------|
| `nil` | 空值，表示"无" | `local a = nil` |
| `boolean` | 布尔，true/false | `local b = true` |
| `number` | 数字（整数+浮点统一） | `local n = 3.14` |
| `string` | 字符串 | `local s = "hello"` |
| `table` | 表（数组+字典+对象） | `local t = {1, 2, key = "val"}` |
| `function` | 函数是一等公民 | `local f = function() end` |
| `userdata` | C/C++ 传入的自定义数据 | 如 Unity GameObject 引用 |
| `thread` | 协程 | `local co = coroutine.create(f)` |

**记忆口诀**: nil不尔数串表，函用线（nil/boolean/number/string/table/function/userdata/thread）

### Q14: XLua Hotfix (70分)
**原理**: [Hotfix] 标签 → HotfixInject 扫描 → 修改方法 IL 代码 → 入口注入跳转指令 → 运行时检测 Lua 覆盖 → 跳转执行
**vs DoString**: Hotfix 是**替换 C# 方法实现**（被动触发），DoString 是**执行 Lua 脚本**（主动调用）
**限制**: 协程/构造函数不支持，泛型有限支持

---

## 🟢 已掌握（速查复核用）

### 值类型 vs 引用类型
值类型基类 System.ValueType→System.Object，分配在声明处（局部变量→栈，类字段→堆）
引用类型基类 System.Object，分配在托管堆 + 栈上存引用指针

### 减少 GC Alloc（7条）
① 对象池 ② StringBuilder ③ 缓存 WaitForSeconds ④ 避免装箱 ⑤ List 预分配 Capacity ⑥ 小 class→struct ⑦ 避免闭包

### Draw Call 优化（4方案）
① Static Batching（静态标记）② Dynamic Batching（<900顶点）③ GPU Instancing（同Mesh同Material）④ SRP Batcher（URP/HDRP 减少 SetPass）

### FixedUpdate
固定时间步长 0.02s（50次/秒），与 PhysX 引擎同步，物理力操作必须放 FixedUpdate

### 协程底层（状态机）
编译器将 yield return 之间的代码转成 switch-case 状态机，MoveNext() 每帧推进一个 case

### 对象池
Queue<T>（不是 List），预热 Prewarm，获取 Dequeue 归还 Enqueue，Active 管理

### 委托 vs 事件
event 是对 delegate 的封装，外部只能+=/-=不能 Invoke 或=null，用于发布-订阅模式

### Shader 描边（两种）
① 顶点外拓（沿法线方向扩展顶点）→ 角色/敌人，精细 ② 屏幕后处理 Sobel → 场景，2 SPC
**两者配合**: Stencil Test 让角色区域免于后处理描边，避免叠加

### GC 实战（Profiler）
Profiler → CPU Usage → GC Alloc 列；BeginSample 精确测量；Memory Profiler 快照对比找泄漏

---

## 📋 速查索引

| 题目 | 模块链接 |
|------|---------|
| 装箱拆箱 | [[10-学习区/语言与基础/csharp-fundamentals\|C# 基础]] |
| GC 分代 | [[10-学习区/计算机基础/memory-gc\|内存管理与GC]] |
| TCP/UDP/HTTP | [[10-学习区/计算机基础/networking\|网络编程]] |
| SOLID | [[10-学习区/架构与算法/design-patterns\|设计模式]] |
| 生命周期 | [[10-学习区/Unity专题/unity-core\|Unity 核心]] |
| 协程 | [[10-学习区/Unity专题/unity-core\|Unity 核心]] |
| 单例 | [[10-学习区/架构与算法/design-patterns\|设计模式]] |
| Animator | [[10-学习区/Unity专题/unity-animation\|Unity 动画]] |
| C++ 智能指针 | [[10-学习区/语言与基础/cpp-comparison\|C++ 对比]] |
| Canvas | [[10-学习区/Unity专题/unity-rendering\|Unity 渲染]] |
| XLua | [[10-学习区/语言与基础/lua-hotfix\|Lua 热更新]] |
