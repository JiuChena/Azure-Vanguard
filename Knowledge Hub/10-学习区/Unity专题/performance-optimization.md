---
tags:
  - performance
  - optimization
  - unity
created: 2026-06-20
---

# 性能优化

## Unity Profiler

Unity Profiler 是性能分析的核心工具，用于定位性能瓶颈。

### 打开方式

`Window > Analysis > Profiler`

### 关键指标

| 指标 | 含义 | 关注点 |
|------|------|--------|
| **CPU Usage** | CPU 各模块耗时 | Scripts、Physics、Rendering 占比 |
| **GPU Usage** | GPU 渲染耗时 | Draw Call、Fill Rate |
| **Rendering** | 渲染统计 | Batches、SetPass Calls、Triangles |
| **Memory** | 内存占用 | GC Alloc（托管内存分配）、Reserved（GC 预留内存） |
| **Audio** | 音频开销 | Audio Clip 数量、音频播放频率 |
| **Physics** | 物理耗时 | 碰撞体数量、物理步进 |

### 代码中使用 Profiler

```csharp
void Update()
{
    UnityEngine.Profiling.Profiler.BeginSample("CombatUpdate");
    // 要分析的代码
    UnityEngine.Profiling.Profiler.EndSample();
}
```

---

## Draw Call 优化

### 什么是 Draw Call

CPU 向 GPU 发送渲染指令的操作。Draw Call 过多会导致 CPU 瓶颈。

### 优化策略

#### 1. 批处理 (Batching)

- **Static Batching**: 将标记为 Static 的不动物体合并为一个大 Mesh，大幅减少 Draw Call
- **Dynamic Batching**: 对小 Mesh（顶点数 < 900）的动态物体自动合批
- **GPU Instancing**: 对使用相同材质和 Mesh 的多个物体，一次 Draw Call 绘制多个实例
- **SRP Batcher**: 在可编程渲染管线 (URP/HDRP) 中使用，通过缓存材质属性减少 SetPass Call

#### 2. LOD (Level of Detail)

根据物体与摄像机的距离，切换不同精度的模型：

- LOD 0: 最高精度（近距离）
- LOD 1: 中等精度
- LOD 2: 最低精度（远距离）
- Culled: 不渲染

> 通过 `LOD Group` 组件配置各级别的模型和切换距离

#### 3. 遮挡剔除 (Occlusion Culling)

不渲染被其他物体遮挡的不可见物体。

- 使用 Occlusion Area 定义遮挡区域
- 烘焙 Occlusion Data
- 适用于有大量遮挡物的室内场景

---

## AssetBundle

### 常用压缩方式

| 压缩模式 | API | 特点 |
|----------|-----|------|
| **LZMA** (默认) | `BuildAssetBundleOptions.None` | 压缩率最高，包体最小；但加载时需要整体解压，加载速度慢 |
| **LZ4** (推荐) | `BuildAssetBundleOptions.ChunkBasedCompression` | 按块压缩，加载时不需要整体解压，既能压缩又有加载速度 |
| **未压缩** | `BuildAssetBundleOptions.UncompressedAssetBundle` | 包体大但加载最快，适合编辑器开发阶段 |

### 压缩策略推荐

1. **开发期**: 使用未压缩，加快迭代速度
2. **移动端线上包**: 使用 LZ4，兼顾包体大小和加载速度
3. **首包热更新**: 使用 LZMA，减小下载大小

### AssetBundle.Unload 参数

`AssetBundle.Unload(bool unloadAllLoadedObjects)`

| 参数值 | 行为 | 适用场景 |
|--------|------|---------|
| **false** | 仅卸载 Bundle 本身的序列化数据，已加载的资源保留在内存中 | 资源还要使用，只需释放 Bundle 引用 |
| **true** | 卸载 Bundle 及所有从该 Bundle 加载的资源，包括正在使用的实例 | 切换场景等需要彻底清理的场景（**谨慎使用**，可能导致资源丢失） |

### Addressables

Addressables 是 Unity 推荐的资源管理方案，底层基于 AssetBundle：

- **ReleaseInstance**: 释放 GameObject 实例
  - `autoReleaseHandle = true`: 自动释放 AsyncOperationHandle
  - `autoReleaseHandle = false`: 需手动调用 `Addressables.Release(handle)` 释放句柄
- 优势：自动依赖管理、远程加载、引用计数

---

## IL2CPP vs Mono

### Mono

- 老方案，C# 代码编译为 IL
- 运行时使用 JIT (Just-In-Time) 将 IL 编译为原生代码
- 运行时可以进行动态代码生成
- 缺点：部分平台不允许 JIT（如 iOS），性能有上限

### IL2CPP

- 将 C# 代码先编译为 IL，再通过 IL2CPP 工具转为 C++ 源码
- 使用平台原生编译器（如 LLVM / Clang）编译为原生代码
- 优点：跨平台（包括不允许 JIT 的平台如 iOS、WebGL），更好的性能优化空间
- 缺点：编译时间长，包体可能增大

### 关键对比

| 维度 | Mono | IL2CPP |
|------|------|--------|
| 运行方式 | JIT 编译 | AOT (Ahead-of-Time) 编译 |
| iOS 支持 | 不支持 | 支持 |
| 性能 | 一般 | 经过 LLVM 优化，性能更好 |
| 编译时间 | 快 | 较慢 |
| Strip Engine Code | 有限 | 支持更激进的代码裁剪 |

---

## 对象池 (Object Pooling)

避免频繁的 `Instantiate` / `Destroy` 调用，减少 GC 分配压力。

```csharp
public class BulletPool : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int poolSize = 20;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject bullet = Instantiate(bulletPrefab);
            bullet.SetActive(false);
            pool.Enqueue(bullet);
        }
    }

    public GameObject GetBullet()
    {
        if (pool.Count > 0)
        {
            GameObject bullet = pool.Dequeue();
            bullet.SetActive(true);
            return bullet;
        }
        // 池中无可用对象时动态扩展
        return Instantiate(bulletPrefab);
    }

    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }
}
```

### 对象池适用场景

- 子弹、特效、音效等频繁创建/销毁的对象
- UI 列表项的复用
- 敌人 AI 的激活/休眠

---

## GC (垃圾回收) 优化

### GC 触发时机

1. **堆内存不足**: 在托管堆上分配新对象，第 0 代内存不足以满足分配时触发
2. **手动调用**: `System.GC.Collect()` 强制执行回收（**应尽量避免**）
3. **系统内存压力**: 操作系统通知内存不足时触发 Full GC
4. **平台特定**: 不同平台的 GC 策略有差异

### Unity 中的 GC

- Unity 使用 **增量式 GC (Incremental GC)**（Unity 2019+）：将 GC 工作分散到多帧执行，避免单帧长时间卡顿
- 开启方式：`Edit > Project Settings > Player > Use incremental GC`

### GC 优化实战技巧

#### 1. 使用对象池

避免频繁分配新对象（见上文）

#### 2. 结构体 (Struct) 优化

```csharp
// 避免：使用 class 导致堆分配
public class BulletData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Speed;
}

// 推荐：使用 struct，栈上分配，无 GC 压力
public struct BulletData  // 注意：struct 不宜过大（通常 < 16 字节）
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Speed;
}
```

#### 3. 预分配固定大小数组替代 List

```csharp
// 避免：List 频繁扩容产生 GC
private List<Enemy> enemies = new List<Enemy>();

// 推荐：使用固定数组 + 计数管理
private Enemy[] enemyArray = new Enemy[120];
private int enemyCount = 0;
```

#### 4. 使用 Profiler 定位 GC 热点

- 关注 **GC Alloc** 列（托管内存分配量）
- 关注 **GC Collected** 列（GC 回收量）
- 逐步消除 Update 等常用函数中的内存分配

#### 5. 其他技巧

- 避免在 Update 中使用 `new` 创建对象
- 使用 `StringBuilder` 替代频繁的字符串拼接
- 缓存 `GetComponent<T>()` 和 `Find` 的结果
- 使用 `yield return null` 而非 `yield return 0`（后者会产生装箱）
- 小心闭包和 LINQ（如 `.ToList()`），会产生额外分配
- 对 struct 重写 `ToString()` / `GetType()` 等方法时要特别注意装箱
- 及时调用 `Resources.UnloadUnusedAssets()` 释放无引用资源

---

## Burst Compiler

Burst 是 Unity 的高性能编译器，将 C# 代码的子集（HPC#）编译为高度优化的原生代码。

```csharp
[BurstCompile]
struct MyJob : IJob
{
    public NativeArray<float> data;

    public void Execute()
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = math.sqrt(data[i]); // Burst 优化后的数学运算
        }
    }
}
```

### 优势

- 利用 SIMD 指令优化循环
- 针对目标平台进行底层优化
- 与 C++ 级别性能接近

---

## Jobs System

Jobs System 提供安全的多线程编程模型，用于 CPU 密集型计算。

```csharp
using Unity.Collections;

// 使用 NativeContainer 而非托管容器
NativeList<int> nativeList = new NativeList<int>(Allocator.Persistent);

// 使用完后必须手动释放
nativeList.Dispose();
```

### 重要组件

- **NativeArray / NativeList**: 非托管容器，可在线程间共享
- **Allocator**: 指定内存分配器类型
  - `Allocator.Temp`: 临时分配，最快（一帧内有效）
  - `Allocator.TempJob`: 短生命周期（4 帧内）
  - `Allocator.Persistent`: 持久分配，必须手动释放

### Unity 性能面试回答框架

- Unity 中主要 GC 来源：`new` 分配引用类型对象
- GC 优化核心思路：减少堆分配
- 优化层次：**原生容器 (NativeContainer)** > **托管类 (Managed)** > **装箱 (Boxing)**
- 关键工具：Unity Profiler 查看 GC Alloc；`GC.Collect` 定位泄漏
- 常见误区：Update 中 `new` 临时对象、`GetComponent` 频繁调用、字符串拼接、闭包

---

## IL2CPP vs Mono 性能对比

| 维度 | Mono | IL2CPP |
|------|------|--------|
| 编译方式 | JIT（运行时编译 IL → 原生） | AOT（IL → C++ → 平台原生） |
| 编译时间 | 快（秒级） | 慢（分钟级，C++ 编译阶段耗时） |
| 运行时性能 | 一般（无平台级优化） | 更高（LLVM/Clang 深度优化、内联、SIMD） |
| 包体大小 | 较小 | 较大（C++ 运行时库 + 泛型代码膨胀） |
| 平台限制 | 不支持 iOS / WebGL | 全平台支持 |
| 代码裁剪 | 有限 | 支持激进 Strip Engine Code |

> 面试要点：IL2CPP 核心价值是跨平台 AOT（尤其 iOS）+ LLVM 优化带来的性能提升，代价是编译时间更长、包体增大。

---

## Burst + Jobs 完整示例

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
struct MultiplyJob : IJob
{
    public NativeArray<float> values;
    public float multiplier;

    public void Execute()
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] *= multiplier;
        }
    }
}

public class BurstJobsDemo : MonoBehaviour
{
    void Start()
    {
        int count = 100000;
        var array = new NativeArray<float>(count, Allocator.Persistent);
        for (int i = 0; i < count; i++) array[i] = i;

        var job = new MultiplyJob { values = array, multiplier = 2f };
        JobHandle handle = job.Schedule();   // 异步调度到工作线程
        handle.Complete();                   // 主线程等待完成

        array.Dispose(); // 必须手动释放 NativeContainer
    }
}
```

> `[BurstCompile]` + `IJob` 将 C# 子集编译为高度优化的原生代码（SIMD），配合 Jobs System 实现多线程并行。适合粒子系统、物理模拟、网格处理等大量数据运算。

---

## AssetBundle 依赖管理

### Manifest 机制

构建时每个 Bundle 生成 `.manifest`，记录其所有依赖。加载资源前必须先加载依赖链。

```csharp
// 1. 加载 Manifest Bundle
AssetBundle manifestAB = AssetBundle.LoadFromFile(manifestPath);
AssetBundleManifest manifest = manifestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

// 2. 获取依赖
string[] deps = manifest.GetAllDependencies("prefabs/bundle_name");

// 3. 先加载所有依赖 Bundle
foreach (string dep in deps)
    AssetBundle.LoadFromFile(depPath + dep);

// 4. 再加载目标 Bundle
AssetBundle target = AssetBundle.LoadFromFile(targetPath);
```

### 引用计数管理

防止重复加载和过早卸载，为每个 Bundle 维护引用计数：

```csharp
Dictionary<string, int> refCounts = new();

void LoadBundle(string name) {
    if (refCounts.ContainsKey(name)) { refCounts[name]++; return; }
    foreach (string dep in manifest.GetAllDependencies(name))
        LoadBundle(dep);          // 递归加载依赖
    AssetBundle.LoadFromFile(path + name);
    refCounts[name] = 1;
}

void UnloadBundle(string name) {
    if (--refCounts[name] <= 0) {
        bundle.Unload(true);
        refCounts.Remove(name);
    }
}
```

> Addressables 内部已实现自动依赖管理和引用计数，新项目推荐直接使用。

---

## Overdraw 分析

### 什么是 Overdraw

同一像素被多次绘制（多层透明 UI / 特效叠加），导致 GPU 填充率瓶颈。移动端 GPU 填充率通常较弱，Overdraw 是移动端性能杀手。

### 分析工具

- **Frame Debugger**：`Window > Analysis > Frame Debugger`，逐 Draw Call 查看渲染顺序和覆盖
- **Scene Overdraw 视图**：场景视图右上角下拉选择 Overdraw 模式，颜色越白 Overdraw 越严重

### 优化手段

- **减少透明 UI 重叠**：避免多层半透明 Panel，合并层级，用不透明背景
- **关闭多余 Raycast Target**：Image/Text 默认开启 Raycast，列表项中叠加大量不必要的射线检测
- **粒子特效**：减少大面积半透明粒子，控制数量和分辨率
- **Shader 简化**：透明物体使用简单 Shader，避免复杂逐像素光照

---

## 移动端专项优化

### 发热控制

- **降低帧率上限**：`Application.targetFrameRate = 30`（一般手游 30fps 足够，竞技类可 60）
- **降低渲染分辨率**：URP 中调整 Render Scale，或 `Screen.SetResolution`
- **动态画质**：CPU/GPU 温度升高时自动降档

### 功耗优化

- **CPU**：降低 Physics 频率（`Time.fixedDeltaTime = 0.033f`，30Hz），减少不必要的 Update
- **GPU**：减少 Draw Call、降分辨率、使用简单 Shader
- **空闲降频**：UI / 暂停状态大幅降低帧率（如 10fps）

### 包体优化

- **纹理压缩**：Android 用 ASTC / ETC2，iOS 用 ASTC，避免 RGBA32
- **音频压缩**：长音频用 Vorbis/MP3，短音效可用 PCM
- **Sprite Atlas**：合并零散小图，减少 Draw Call 和包体碎片
- **代码裁剪**：IL2CPP + Strip Engine Code 设为 Medium/High
- **资源审计**：`Editor.log` 中 BuildReport 查看资源占用排序，移除未引用资源
- **Shader 精简**：`Graphics Settings > Always Included Shaders` 用最小集合
