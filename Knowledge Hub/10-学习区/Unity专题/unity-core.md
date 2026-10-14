---
tags: [unity, core, engine]
created: 2026-06-20
updated: 2026-06-20
aliases: [Unity核心, Unity Engine Core]
---

# Unity 核心系统

## 概述

Unity 核心系统涵盖 MonoBehaviour 生命周期、协程 (Coroutine)、GameObject/Component 模型、ScriptableObject、Prefab、Inspector 序列化、Transform、场景管理、资源加载（Resources / AssetBundle / Addressables）、物理系统（Collider / Trigger）以及 GC 内存管理等底层机制。这是 Unity 客户端开发最基础也最常被面试考察的知识体系。

---

## 核心知识点

### 1. MonoBehaviour 生命周期

Unity 脚本继承自 `MonoBehaviour`，引擎会按固定顺序调用其生命周期函数。

**完整执行顺序：**

```
Reset (仅编辑器) → Awake → OnEnable → Start → FixedUpdate → Update → LateUpdate → OnDisable → OnDestroy
```

**只执行一次的函数（One-time Lifecycle）：**

| 函数 | 调用时机 |
|------|----------|
| `Reset()` | 仅在编辑器中将脚本附加到 GameObject 或点击 Reset 时调用 |
| `Awake()` | 脚本实例被加载时调用，无论脚本是否 enabled。用于初始化引用 |
| `OnEnable()` | 脚本被启用时调用（包括初始加载和每次 SetActive(true) 重新激活） |
| `Start()` | 第一次 Update 之前调用，仅在脚本 enabled 时调用 |
| `OnDisable()` | 脚本被禁用时调用（与 OnEnable 配对） |
| `OnDestroy()` | 脚本所在 GameObject 被销毁时调用 |

**每帧执行的函数：**

| 函数 | 调用时机 | 用途 |
|------|----------|------|
| `FixedUpdate()` | 固定时间间隔调用（默认 0.02s，即 50 FPS） | 物理相关逻辑（Rigidbody 操作） |
| `Update()` | 每帧调用一次，间隔不固定 | 游戏逻辑主循环 |
| `LateUpdate()` | 所有 Update 执行完毕后调用 | 摄像机跟随、动画后处理 |

**注意事项：**

- ⚠️ `Awake` 中不要依赖其他对象的 `Start` 是否执行——`Start` 还未调用
- `OnEnable/OnDisable` 对应的是"启用/禁用"状态，会在对象激活/隐藏时反复调用
- 在 Hierarchy 中删除对象时会触发 `OnDisable → OnDestroy`
- `Reset` 只在编辑器模式下触发，打包后不执行

```csharp
using UnityEngine;

public class LifecycleDemo : MonoBehaviour
{
    private void Reset()
    {
        Debug.Log($"[Editor] Reset on {gameObject.name}");
    }

    private void Awake()
    {
        Debug.Log($"[Once] Awake - 初始化引用");
    }

    private void OnEnable()
    {
        Debug.Log($"[PerEnable] OnEnable - 对象激活");
    }

    private void Start()
    {
        Debug.Log($"[Once] Start - 初始化逻辑");
    }

    private void FixedUpdate()
    {
        // 物理计算，固定间隔
    }

    private void Update()
    {
        // 每帧逻辑
    }

    private void LateUpdate()
    {
        // Update 之后执行，适合摄像机跟随
    }

    private void OnDisable()
    {
        Debug.Log($"[PerDisable] OnDisable - 对象隐藏");
    }

    private void OnDestroy()
    {
        Debug.Log($"[Once] OnDestroy - 对象销毁");
    }
}
```

---

### 2. 协程 (Coroutine)

协程是 Unity 中实现异步延迟执行的机制，基于 C# 的 `IEnumerator` 迭代器。

**核心原理：**

1. 协程方法返回 `IEnumerator`，通过 `yield return` 暂停执行
2. Unity 在每帧 Update 之后检查协程的 `MoveNext()`，决定是否继续执行
3. `yield return` 的值决定等待条件：`null`（下一帧）、`WaitForSeconds`（等待秒数）、`WaitForEndOfFrame` 等

**yield return 常见用法：**

```csharp
yield return null;              // 等待下一帧
yield return new WaitForSeconds(1f);  // 等待 1 秒
yield return new WaitForEndOfFrame(); // 等待帧结束
yield return new WaitUntil(() => condition);  // 等待条件成立
yield return new WaitWhile(() => condition);  // 等待条件不成立
yield return StartCoroutine(AnotherCoroutine()); // 等待另一个协程完成
```

**自己实现简易协程调度器：**

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCoroutineRunner : MonoBehaviour
{
    private List<IEnumerator> m_coroutines = new List<IEnumerator>();

    public void StartMyCoroutine(IEnumerator routine)
    {
        m_coroutines.Add(routine);
    }

    void Update()
    {
        // 倒序遍历，安全删除已完成的协程
        for (int i = m_coroutines.Count - 1; i >= 0; i--)
        {
            IEnumerator coroutine = m_coroutines[i];
            // MoveNext() 返回 false 表示协程执行完毕
            if (!coroutine.MoveNext())
            {
                m_coroutines.RemoveAt(i);
            }
        }
    }
}

// 使用示例
public class TestCoroutine : MonoBehaviour
{
    void Start()
    {
        var runner = gameObject.AddComponent<SimpleCoroutineRunner>();
        runner.StartMyCoroutine(MyFirstCoroutine());
    }

    IEnumerator MyFirstCoroutine()
    {
        Debug.Log("协程开始 - 第 1 帧");
        yield return new WaitForSeconds(1f);  // 等待 1 秒
        Debug.Log("1 秒后执行");
        yield return null;                     // 等待下一帧
        Debug.Log("第 2 帧 - 协程结束");
    }
}
```

**自定义 YieldInstruction：**

```csharp
using UnityEngine;

public class MyWaitForSeconds : CustomYieldInstruction
{
    private float m_TargetTime;

    public MyWaitForSeconds(float seconds)
    {
        m_TargetTime = Time.time + seconds;
    }

    // keepWaiting 返回 true 时继续等待，false 时协程继续执行
    public override bool keepWaiting
    {
        get { return Time.time < m_TargetTime; }
    }
}
```

**常见问题：**

- ⚠️ `yield return 0` 和 `yield return null` 效果相同（等待一帧），但 `0` 会产生装箱（int → object），推荐使用 `null`
- 协程在 GameObject 被禁用（SetActive(false)）时不会停止，但所在对象被销毁（Destroy）时会强制终止
- 使用 `StopCoroutine` 或 `StopAllCoroutines` 来手动停止协程

---

### 3. GameObject 与 Component 模型

Unity 采用**组件化（Component-Based）架构**：

- **GameObject** 是场景中的基本实体，本质上是一个容器
- **Component** 是附加在 GameObject 上的功能模块
- 每个 GameObject 至少包含一个 **Transform** 组件
- `MonoBehaviour` 不能通过 `new` 创建，必须通过 `AddComponent<T>()` 附加到 GameObject 上

**常用 API：**

```csharp
// 获取组件
Rigidbody rb = GetComponent<Rigidbody>();
Rigidbody rbChild = GetComponentInChildren<Rigidbody>();
Rigidbody rbParent = GetComponentInParent<Rigidbody>();
Rigidbody[] allRb = GetComponents<Rigidbody>();

// 添加/移除组件
gameObject.AddComponent<BoxCollider>();
Destroy(GetComponent<BoxCollider>());

// 查找 GameObject
GameObject player = GameObject.Find("Player");           // 按名称查找
GameObject tagged = GameObject.FindWithTag("Enemy");     // 按 Tag 查找
GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");

// 实例化与销毁
GameObject clone = Instantiate(prefab, position, rotation);
Destroy(gameObject, 2.0f);   // 延迟 2 秒销毁
DestroyImmediate(gameObject); // 立即销毁（仅编辑器）
```

**⚠️ 性能注意事项：**

- **严禁在 Update 中调用 `GameObject.Find`** —— 它会遍历整个 Hierarchy，开销极大。应在 Awake/Start 中缓存引用
- **严禁在 Update 中频繁调用 `GetComponent`** —— 同样应在初始化时缓存
- 大量频繁创建/销毁对象时，应使用对象池（Object Pooling）

---

### 4. Update / FixedUpdate / LateUpdate 的区别

| 特性 | Update | FixedUpdate | LateUpdate |
|------|--------|-------------|------------|
| 调用频率 | 每帧一次，不固定（依赖帧率） | 固定间隔（默认 0.02s/50fps） | 每帧一次，在 Update 之后 |
| 适用场景 | 游戏逻辑、输入检测 | 物理模拟（Rigidbody） | 摄像机跟随、动画后处理 |
| Time.deltaTime | 上一帧到当前帧的时间 | 固定的 `Time.fixedDeltaTime` | 同 Update |

```csharp
void Update()
{
    // 输入检测：Input.GetKeyDown 必须在 Update 中检测
    if (Input.GetKeyDown(KeyCode.Space))
    {
        Jump();
    }

    // 非物理移动（直接修改 Transform）
    float move = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
    transform.Translate(move, 0, 0);
}

void FixedUpdate()
{
    // 物理相关操作：对 Rigidbody 施加力
    rb.AddForce(Vector3.forward * force);
}

void LateUpdate()
{
    // 摄像机跟随——等玩家移动完再跟随
    Camera.main.transform.position = target.position + offset;
}
```

---

### 5. 物理系统：Collider 与 Trigger

| 对比 | Collider（碰撞器） | Trigger（触发器） |
|------|-------------------|-------------------|
| 勾选 IsTrigger | 否 | 是 |
| 物理交互 | 产生碰撞反馈（弹开、停止） | 穿透但产生事件 |
| 回调函数 | `OnCollisionEnter/Stay/Exit` | `OnTriggerEnter/Stay/Exit` |
| 使用场景 | 墙壁、地面、障碍物 | 检测区域、拾取物品、触发剧情 |

**触发条件：** 双方至少一个有 Rigidbody（可无重力 kinematic），物体在移动或有 Rigidbody 时才产生事件（物理引擎驱动）。

```csharp
// Trigger 事件 - 捡金币
void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Coin"))
    {
        coins++;
        Destroy(other.gameObject);
    }
}

// Collision 事件 - 击中敌人
void OnCollisionEnter(Collision collision)
{
    if (collision.gameObject.CompareTag("Enemy"))
    {
        DealDamage(collision.gameObject, 10f);
    }
}
```

---

### 6. 资源加载系统

Unity 提供多种资源加载方式，从简单到复杂：

| 方式 | 特点 | 适用场景 |
|------|------|----------|
| **Resources.Load** | 简单，资源必须放在 Resources 目录 | 小型项目、原型开发 |
| **AssetBundle** | 打包灵活，支持热更新 | 大型项目、需要热更 |
| **Addressables** | 对 AssetBundle 的高层封装，异步加载，引用计数管理 | 推荐的中大型项目方案 |
| **StreamingAssets** | 直接文件读取，不经 Unity 打包 | 配置文件、视频等原始文件 |

**Resources 加载示例：**

```csharp
using UnityEngine;

public class ResourcesLoadDemo : MonoBehaviour
{
    [SerializeField] private string prefabPath = "Prefabs/Cube";

    void Start()
    {
        // 同步加载
        GameObject go = Resources.Load<GameObject>(prefabPath);
        if (go != null)
            Instantiate(go, transform.position, Quaternion.identity);
    }
}
```

**AssetBundle 加载示例：**

```csharp
using UnityEngine;

public class AssetBundleLoadDemo : MonoBehaviour
{
    [SerializeField] private string bundlePath;
    [SerializeField] private string assetName = "Role01";

    void Start()
    {
        StartCoroutine(LoadFromBundle());
    }

    IEnumerator LoadFromBundle()
    {
        var bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            Debug.LogError("Bundle 加载失败: " + bundlePath);
            yield break;
        }

        var go = bundle.LoadAsset<GameObject>(assetName);
        Instantiate(go, transform.position, Quaternion.identity);
        bundle.Unload(false); // 不卸载已加载的资源
    }
}
```

**StreamingAssets 读取示例：**

```csharp
using System.IO;
using UnityEngine;

public class StreamingAssetsLoadDemo : MonoBehaviour
{
    [SerializeField] private string fileName = "config.json";

    void Start()
    {
        var path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            Debug.Log(text);
        }
        else
        {
            Debug.LogWarning("文件不存在: " + path);
        }
    }
}
```

**⚠️ 注意事项：**

- `Resources` 目录下所有资源都会打包进安装包，不管是否使用，体积会持续增长
- `Resources` 永远不要放太多资源，正式项目推荐使用 `Addressables`
- `AssetBundle.Unload(false)` 释放 Bundle 本身但不释放已加载的资源；`Unload(true)` 会同时释放所有从此包加载的资源
- `StreamingAssets` 在 Android 平台需要通过 `UnityWebRequest` 访问（APK 内部是压缩包），不能直接 `File.ReadAllText`

---

### 7. GameObject 对象池 (Object Pooling)

频繁 Instantiate/Destroy 会产生大量 GC 分配和回收压力。对象池通过预创建和复用对象来避免这个问题。

```csharp
using System.Collections.Generic;
using UnityEngine;

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
        // 池为空时创建新对象（扩容）
        GameObject newBullet = Instantiate(bulletPrefab);
        return newBullet;
    }

    public void ReturnBullet(GameObject bullet)
    {
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }
}
```

---

### 8. Unity GC 优化实战

**Unity 2019+ 增量式 GC (Incremental GC)：**
- 可在 `Edit > Project Settings > Player > Other Settings` 中开启 `Use incremental GC (Boehm)`
- 将一次大的 GC 操作分散到多帧执行，减少单帧卡顿

**GC 优化核心策略：**

1. **对象池 (Object Pooling)**：避免频繁创建销毁
2. **使用 Struct 替代 Class**（适用于小数据结构）：
```csharp
// 使用 struct 减少堆分配
public struct BulletData
{
    public Vector3 position;
    public Quaternion rotation;
    public float speed;
}
```

3. **使用数组替代 List**（预知上限的场景）：
```csharp
// 预分配数组，避免 List 内部扩容
private Enemy[] enemyArray = new Enemy[120];
private int enemyCount = 0;
```

4. **使用 Profiler 定位 GC 热点**：
```csharp
void Update()
{
    UnityEngine.Profiling.Profiler.BeginSample("CombatUpdate");
    // 战斗逻辑
    UnityEngine.Profiling.Profiler.EndSample();
}
// Window > Analysis > Profiler 查看 GC Alloc
```

5. **Unity 2021+ 新特性**：
   - **Burst Compiler**：将 C# 代码编译为高效原生代码
   - **Collections Package**（`Unity.Collections`）：`NativeArray<T>`、`NativeList<T>` 等非托管容器

**在 foreach 遍历 List 时的 GC 分配问题：**

⚠️ Unity 旧版本（基于 Mono）中 `foreach` 遍历 `List<T>` 会产生装箱/GC 分配，因为 `List<T>.Enumerator` 是 struct 但接口转换时可能装箱。推荐方案：
- 优先使用 `for` 循环遍历 `List<T>`
- 或使用 Unity 2021+ IL2CPP 后端，它会对此做优化

---

### 9. NavMesh 寻路系统

Unity 内置的 AI 寻路解决方案。

**基本使用流程：**

1. 将场景中静态物体标记为 `Navigation Static`
2. `Window > AI > Navigation` → Bake 烘焙 NavMesh
3. 给移动对象添加 `NavMeshAgent` 组件
4. 通过代码设置目标点

```csharp
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimplePathfinding : MonoBehaviour
{
    public Transform target;
    public LineRenderer lineRenderer;
    NavMeshAgent agent;
    NavMeshPath path = new NavMeshPath();

    void Awake() => agent = GetComponent<NavMeshAgent>();

    void Update()
    {
        // 跟随目标
        if (target != null)
            SetDest(target.position);

        // 点击移动
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit))
                SetDest(hit.point);
        }
    }

    void SetDest(Vector3 destination)
    {
        agent.SetDestination(destination);
    }
}
```

**重要概念：**

- **NavMeshObstacle**：动态障碍物（如移动的车），可添加 Carve 功能实时切割 NavMesh
- **OffMeshLink**：跨越非连接区域（如跳跃点、传送点）
- **Areas**：不同区域可设置不同代价（如水域行走更慢）
- 判断到达目标：`agent.remainingDistance <= agent.stoppingDistance && !agent.pathPending`

---

### 10. ScriptableObject

ScriptableObject 是 Unity 的数据容器，用于存储独立于场景的可配置数据。

**特点：**
- 存储在项目中作为 `.asset` 文件，不依赖场景
- 运行时只读（修改仅在编辑器中持久化，运行时修改不保存）
- 适合存储配置数据、物品属性表、关卡数据等

---

### 11. Transform 核心概念

每个 GameObject 都有一个 Transform 组件，定义其空间属性：

- **Position**：世界/局部坐标位置
- **Rotation**：旋转（Quaternion）
- **Scale**：缩放
- 父子关系：`transform.SetParent(parent)` / `transform.parent`

---

### 12. 序列化系统

Unity 序列化规则：
- `public` 字段自动序列化并在 Inspector 显示
- `private` 字段需要 `[SerializeField]` 特性才会序列化
- 可序列化的类型：`int, float, string, bool, Vector3, GameObject, MonoBehaviour` 引用等
- 不可序列化：`Dictionary`、`属性`、`static` 字段

**JsonUtility 数据持久化示例：**

```csharp
using UnityEngine;
using System.IO;
using System;

[System.Serializable]
public class GameData
{
    public int currentLevel;
    public float playerHealth;
    public string playerName;
    public int[] inventoryItems;
    public DateTime lastSaveTime;
}

public class JsonSaveExample : MonoBehaviour
{
    private string savePath;

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "savegame.json");
        LoadGameData();
    }

    void SaveGameData()
    {
        GameData data = new GameData();
        data.currentLevel = 5;
        data.playerHealth = 85.5f;
        data.playerName = "Player1";
        data.inventoryItems = new int[] { 101, 205, 208 };
        data.lastSaveTime = DateTime.Now;

        string json = JsonUtility.ToJson(data, true); // true 表示格式化输出
        File.WriteAllText(savePath, json);
        Debug.Log($"存档已保存: {savePath}");
    }

    void LoadGameData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            GameData data = JsonUtility.FromJson<GameData>(json);
            Debug.Log($"加载完成 - 关卡: {data.currentLevel}, 血量: {data.playerHealth}");
        }
    }
}
```

---

### 13. 观察者模式 (Observer / Event System)

Unity 中实现解耦的常用模式，通过事件订阅机制让模块间松耦合通信。

```csharp
using System;
using System.Collections.Generic;

// 事件数据（使用 readonly struct 避免拷贝开销）
public readonly struct PlayerHurtData
{
    public float Damage { get; }
    public GameObject Source { get; }
    public GameObject Target { get; }

    public PlayerHurtData(float damage, GameObject source, GameObject target)
    {
        Damage = damage; Source = source; Target = target;
    }
}

// 事件监听接口
public interface IGameEventListener<T>
{
    void OnEvent(T data);
}

// 事件主题基类（发布者）
public abstract class Subject<T>
{
    private readonly List<IGameEventListener<T>> listeners = new List<IGameEventListener<T>>();

    public void Register(IGameEventListener<T> listener) => listeners.Add(listener);
    public void Unregister(IGameEventListener<T> listener) => listeners.Remove(listener);

    protected void Notify(T data)
    {
        // 倒序遍历，防止在回调中移除监听者导致索引错乱
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEvent(data);
    }
}

// Player 作为事件发布者
public class PlayerHealth : Subject<PlayerHurtData>
{
    public void TakeDamage(float damage, GameObject source)
    {
        var data = new PlayerHurtData(damage, source, gameObject);
        Notify(data);
    }
}

// 游戏管理器：连接发布者和订阅者
public class GameTest : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [SerializeField] private HealthUI ui;
    [SerializeField] private AchievementSystem achievement;

    private void Start()
    {
        player.Register(ui);
        player.Register(achievement);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            player.TakeDamage(25f, gameObject);
    }

    private void OnDestroy()
    {
        // 务必注销监听，防止内存泄漏
        if (player != null)
        {
            player.Unregister(ui);
            player.Unregister(achievement);
        }
    }
}
```

---

### 14. Animator / Animation 系统

Unity 提供两套动画系统：

| 特性 | Animation（Legacy，旧版） | Animator（Mecanim，推荐） |
|------|--------------------------|---------------------------|
| 控制方式 | 直接播放 AnimationClip | 通过 AnimatorController 状态机 |
| 设置方式 | Rig → Animation Type → Legacy | Rig → Animation Type → Generic/Humanoid |
| 混合/过渡 | 手动代码控制 | 可视化状态机 + BlendTree |
| 适用场景 | 简单动画（2D 帧动画） | 复杂角色动画、人形动画重定向 |

```csharp
// Legacy Animation
public class LegacyAnimationDemo : MonoBehaviour
{
    [SerializeField] private Animation anim;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            anim.Play("Idle");
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            anim.Play("Run");
    }
}
```

> 新项目推荐使用 **Animator (Mecanim)**，Legacy Animation 仅在维护老项目时使用。

---

## 面试高频问题及答案

### Q1: FixedUpdate、Update、LateUpdate 的区别？

- **Update**：每帧调用一次，频率不固定（依赖帧率）。用于游戏逻辑、输入检测。使用 `Time.deltaTime`。
- **FixedUpdate**：固定时间间隔调用（默认 0.02s，可在 Time 设置中修改）。用于物理模拟（Rigidbody 操作）。使用 `Time.fixedDeltaTime`。
- **LateUpdate**：所有 Update 执行完后调用。用于摄像机跟随、动画后处理等需要在其他对象更新完成后再执行的逻辑。

---

### Q2: MonoBehaviour 生命周期函数的完整执行顺序？

```
Reset → Awake → OnEnable → Start → FixedUpdate → Update → LateUpdate → OnDisable → OnDestroy
```

- `Awake`：初始化自身引用
- `Start`：依赖其他对象的初始化（此时所有 Awake 已执行）
- `OnEnable/OnDisable`：启用/禁用时调用，可反复触发
- `OnDestroy`：对象销毁时调用，用于清理资源

---

### Q3: yield return null 和 yield return new WaitForSeconds 的区别？

- `yield return null`：暂停一帧，下一帧的 Update 之后继续执行
- `yield return new WaitForSeconds(n)`：暂停 n 秒（按游戏时间 Time.time），n 秒后继续
- ⚠️ **面试加分回答**：推荐使用 `yield return null` 而非 `yield return 0`，因为 `0` 是 int 类型会被装箱为 object，产生 GC 分配。`null` 是引用类型，无需装箱。

---

### Q4: GameObject 和 Component 的关系？

Unity 采用**组件化架构**：
- `GameObject` 是场景中的基本实体单元，本质上是一个容器（空壳）
- `Component` 是附加在 GameObject 上的功能模块（Transform、Renderer、Collider 等）
- 每个 GameObject 至少有一个 `Transform` 组件
- `MonoBehaviour` 继承自 `Component`，脚本必须通过 `AddComponent<T>()` 附加到 GameObject 上

---

### Q5: 为什么 MonoBehaviour 不能 new，必须放在 GameObject 上？

`MonoBehaviour` 依赖 Unity 引擎的底层 C++ 对象生命周期管理。直接 `new` 创建的 MonoBehaviour 没有关联的原生对象（Native Object），无法接收引擎回调（Awake、Update 等），会导致空引用或行为异常。

---

### Q6: 在 Update 中使用 GameObject.Find 或 GetComponent 有什么问题？

`GameObject.Find` 遍历整个场景 Hierarchy，时间复杂度 O(n)；`GetComponent` 也有查找开销。在 Update 中频繁调用会导致严重性能问题。

**正确做法：** 在 `Awake` 或 `Start` 中缓存引用到成员变量。

---

### Q7: AssetBundle 是什么？和 Resources 有什么区别？

- **Resources**：资源放在项目中，随安装包一起发布，所有 Resources 目录下的资源都会被打包（无论是否使用），不支持热更新。适用于小型项目和快速原型。
- **AssetBundle**：资源打包成独立文件，可从服务器下载，支持热更新和按需加载，资产之间有依赖管理。适用于正式项目。
- **Addressables**：Unity 对 AssetBundle 的进一步封装，提供更友好的 API，自动处理依赖和引用计数。推荐新项目使用。

---

### Q8: OnTriggerEnter 和 OnCollisionEnter 的区别？

| | OnTriggerEnter | OnCollisionEnter |
|---|---|---|
| **Is Trigger** | 勾选 | 不勾选 |
| **物理效果** | 穿透，不产生碰撞反馈 | 产生碰撞（弹开、停止） |
| **典型用途** | 检测区域、拾取物品 | 墙壁碰撞、击中敌人 |
| **前提条件** | 至少一方有 Rigidbody | 双方都有 Collider，至少一方有 Rigidbody |

---

### Q9: ref 和 out 关键字的区别？

- `ref`：传入前**必须初始化**；方法内可读可写
- `out`：传入前**可不初始化**；方法内**必须赋值**后才能返回
- `in`（C# 7.2+）：只读引用传递，传入前必须初始化，方法内不可修改。适用于大 struct 的高效传递

---

### Q10: Unity 中如何减少 GC？

1. **使用对象池**避免频繁 Instantiate/Destroy
2. **使用 struct 代替 class**（小数据场景）
3. **使用泛型容器** `List<T>` / `Dictionary<TKey,TValue>` 代替 `ArrayList` / `Hashtable`（避免装箱）
4. **避免在 Update 中产生临时字符串**拼接（使用 StringBuilder 或 string.Format）
5. **避免 foreach 在旧版本 Unity 中对 List 的 GC 分配**（用 for 替代）
6. **缓存组件引用**，避免重复 GetComponent
7. **使用 Unity 2019+ 增量式 GC**分散回收压力
8. **使用 Profiler**定位 GC Alloc 热点

---

### Q11: Animator 和 Animation 的区别？

- **Animation**（旧版/Legacy）：直接播放 AnimationClip，没有状态机。适合简单动画。
- **Animator**（Mecanim，推荐）：通过 AnimatorController 状态机管理动画过渡，支持 BlendTree、Layer、IK、人形动画重定向（Humanoid Avatar）。适合复杂的角色动画系统。

---

### Q12: foreach 遍历 List 在 Unity 中会产生 GC 吗？

在 Unity 旧版本（基于 Mono 运行时）中，`foreach` 遍历 `List<T>` 可能因 `List<T>.Enumerator` 是 struct 但在接口上下文中装箱而产生 GC 分配。IL2CPP 后端对此有优化。**安全做法：** 性能敏感代码中使用 `for` 循环遍历 List。

---

## 最佳实践

1. **引用缓存**：在 Awake/Start 中缓存 `GetComponent<T>()` 和 `GameObject.Find()` 结果，永远不要放在 Update 中
2. **物理代码放 FixedUpdate**：任何对 Rigidbody 的力/速度操作放在 FixedUpdate 中，保证与物理引擎同步
3. **协程管理**：确保协程在对象销毁时被停止（OnDestroy 中调用 StopAllCoroutines）
4. **空值检查**：使用 `if (go != null)` 的模式做安全检查
5. **使用 CompareTag 代替 tag 字符串比较**：`other.CompareTag("Enemy")` 比 `other.tag == "Enemy"` 更高效（避免字符串分配）
6. **事件注销**：在 OnDestroy/OnDisable 中注销事件监听，防止内存泄漏和空引用
7. **对象池**：子弹、特效、敌人等高频创建销毁的对象必须使用对象池
8. **资源管理**：新项目优先使用 Addressables；Assets 目录下非必要资源不要放在 Resources 文件夹中
9. **序列化**：需要保存的数据类标记 `[System.Serializable]`；Inspector 中需要显示的私有字段标记 `[SerializeField]`
10. **Profiler 驱动优化**：不要凭感觉优化，先用 Profiler 找到瓶颈

---

## 相关模块

- [[unity-rendering]] - Unity 渲染管线、Draw Call、Shader
- [[unity-physics]] - 物理引擎详解（Collider、Rigidbody、Joint）
- [[unity-ui]] - UGUI 系统（Canvas、RectTransform、Mask、事件系统）
- [[csharp-fundamentals]] - C# 基础（值类型/引用类型、装箱拆箱、委托事件）
- [[design-patterns]] - 设计模式在 Unity 中的应用
- [[performance-optimization]] - Unity 性能优化综合指南
