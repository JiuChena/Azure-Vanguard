---
tags: [module, Gear, core, resources]
created: 2026-09-09
updated: 2026-09-09
---

# Gear 基础模块

## 使用原则

Gear 是跨项目可复用的基础设施。业务功能先检查这里是否已经存在能力；存在则调用，缺失且确实通用时再提出新增模块设计。不要在角色 Ability 内重新写一套资源缓存、事件中心、定时器、对象池或文件存档。

## AddressableManager：资源 Lease 与 Scope

路径：`Assets/Scripts/CSharp/Core/Gear/ResourceSystem/`

### 核心 API

```csharp
ResourceLease<GameObject> lease =
    await AddressableManager.Instance.AcquireAssetAsync<GameObject>(key, scope);

GameObject prefab = lease.Asset;
lease.Dispose();
```

| API | 用途 |
| --- | --- |
| `AcquireAssetAsync<T>(key, scope)` | 获取普通资源租约。相同类型与 key 共享加载条目。 |
| `AcquirePersistentAssetAsync<T>(key)` | 获取常驻 Lease，仅用于启动级长期资源。 |
| `TryGetLoadedAsset<T>(key, out asset)` | 只读取已加载缓存，不触发加载。 |
| `IsAssetLoaded<T>(key)` | 查询缓存加载完成状态。 |
| `GetResourceStatus<T>(key)` | 查询 Addressables 异步状态。 |
| `TryGetResourceDebugInfo<T>` | 获取引用计数和状态诊断，不是业务数据。 |
| `ReleaseUnusedResources()` | 清理引用计数为零的兜底方法。 |

### Scope 用法

```csharp
private ResourceScope _scope;

public async Task LoadAsync()
{
    _scope = new ResourceScope("Enemy:CH1001");
    ResourceLease<GameObject> lease =
        await AddressableManager.Instance.AcquireAssetAsync<GameObject>("EnemyPrefab", _scope);
}

public void Dispose()
{
    _scope?.Dispose();
    _scope = null;
}
```

一个 Scope 可以登记多个 Lease，Dispose 时反向释放。不要只拿 `lease.Asset` 后丢失 Lease；那会造成资源引用无法准确归还。

资源 key 内部按 `FullTypeName::key` 区分类型，因此同名但不同资源类型不会共享条目。

## ObjectsPool：通用对象池

路径：`Assets/Scripts/CSharp/Core/Gear/ObjectPool/ObjectsPool.cs`

| API | 用途 |
| --- | --- |
| `ObjectsPool.Instance.Get(addressableKey, parent, callback)` | Addressable 预制体取对象；池会持有资源 Lease 并绑定依赖。 |
| `Get(prefab, dependencyKey, parent, callback)` | 用已有预制体取对象，同时声明依赖资源 key。 |
| `Get(prefab, parent, callback)` | 不需要资源依赖的已有预制体取对象。 |
| `Put(obj)` | 正常回池。 |
| `TimerPut(obj, delayTime)` | 延迟回池。 |
| `Clear(key)` / `ClearAll()` | 销毁池内对象并释放池自身持有的资源。 |

池对象应挂 `Poolable<T>` 子类或使用现有的池化流程。由 Addressable 获取的池必须让池持有正确依赖，不能手动提前 Dispose 对应资源 Lease，否则池内实例可能引用已释放资源。

## VFXPool：特效池

路径：`Assets/Scripts/CSharp/Core/Gear/VFXPool/`

使用 `VFXPool.VFXSpawn(group, prefabName, prefab, position, rotation, autoRecycleTime, callback)` 生成特效；`autoRecycleTime` 必须大于零。生成的实例挂 `VFXPoolHost` 以保存池归属信息，使用 `VFXRecycle` 回收。

特效分组用于批量 `ClearGroup`；预制体名用于 `ClearPool`。VFXPool 管的是 GameObject 生命周期，不替代 Addressables 资源租约管理。

## AudioManager：只播放 AudioClip

路径：`Assets/Scripts/CSharp/Core/Gear/AudioModuleManager/`

当前 Audio 模块只接收已经拿到的 `AudioClip` 并播放；资源加载是外部职责。需要一组音频资源时，业务层应使用 SO + Addressables/Scope 管理加载和释放，再把 Clip 交给 AudioManager。

常见职责：

- `AudioData` 描述播放参数；
- `AudioDataManager` 管理不同音频类型的 AudioSource；
- `AudioManager` 负责实际播放；
- 外部在使用前检查所需 AudioSource 是否仍有效，若被其他对象连带销毁则丢弃缓存引用。

不要让 AudioManager 兼管 Addressables、角色业务音频表或复杂资源分组。

## EventCenter：全局事件

路径：`Assets/Scripts/CSharp/Core/Gear/EventSystem/`

使用 EventCenter 在跨模块、低耦合的广播场景传递事件。事件名在 `EventNames` 中集中定义，事件容器在 `EventsContainers` 中定义。

使用规则：

- 注册与移除必须成对；生命周期内订阅的监听在 `OnDisable` 或 `Dispose` 移除。
- 同一个事件名必须保持参数类型一致；模块会对类型不匹配报错。
- 同对象内强依赖的直接调用不必绕成全局事件。
- 不要为某个业务 Ability 再造独立的 Action 事件总线。

## Timer、GOTimer、PublicMono

| 模块 | 适用情况 |
| --- | --- |
| `Timer.Instance` | 全局延迟或重复回调；使用 `AddTimerEvent` 创建，保证 interval 与次数有效。 |
| `GOTimer` | 某个 GameObject 存活期间的到点行为；回池前调用 `Clear` 清理任务。 |
| `PublicMono.Instance` | 需要全局每帧回调但不值得新建一个专用 MonoBehaviour 时，使用 `AddListener` / 对应移除 API。 |

计时回调和每帧监听都会持有委托。对象结束时必须取消，避免静态单例留住已销毁对象。

## UI：PanelManager 与 PanelBase

`PanelManager` 使用 Addressable key 加载 Canvas 与面板。Canvas key 和层级节点名称定义在 `CorePathDependencies`。

```csharp
PanelManager.Instance.OpenPanel<MyPanel>("InventoryPanel", UILayer.Mid);
PanelManager.Instance.ClosePanel("InventoryPanel");
```

`PanelBase` 约定：

| 回调 | 用途 |
| --- | --- |
| `EventInit()` | Awake 阶段注册事件。 |
| `ComponentInit()` | Start 阶段查找组件。 |
| `OnUpdate()` | 按需逐帧刷新。 |
| `DisplayPanel()` | 打开时表现。 |
| `HidePanel()` | 关闭时播放退出表现；真正销毁由 PanelManager + Timer 处理。 |
| `OnEscapePressed()` | 默认关闭自身，子类可改为返回或弹确认。 |

面板的 `ResourceScope` 由 PanelManager 注入。面板加载的附属 Addressable 资源必须挂到这个 Scope，销毁时自动释放。

## BinaryDataManager：本地持久化

路径：`Assets/Scripts/CSharp/Core/Gear/BinaryDataManager/`

```csharp
BinaryDataManager.Instance.Save("Player", "profile", profileData);
ProfileData data = BinaryDataManager.Instance.Load<ProfileData>("Player", "profile");
```

文件写入：

```text
Application.persistentDataPath/Data/<path>/<fileName>.bin
```

序列化使用 MessagePack 运行时封装。数据类应使用稳定 MessagePack `Key` 标记字段；当前项目不为每个业务数据手写 Formatter/Resolver 注册表，也不主动兼容没有 Key 的旧数据格式。

## InternetMessage：网络消息帧头

路径：`Assets/Scripts/CSharp/Core/Gear/InternetMessageDataBase/`

InternetMessage 只负责消息 ID 与长度帧头，不负责 TCP/WebSocket 粘包、分包、接收缓存、MessagePack 业务序列化或网络线程。

帧格式：

```text
0..3   uint 消息 ID，大端
4..7   uint 消息体长度，大端
8..N   MessagePack 消息体
```

初始化阶段在 `MessageTool.Init()` 中显式写入：

```csharp
InternetMessage.Register((uint)InternetMessageID.PlayerInput, typeof(MsgPlayerInput));
```

规则：

- 一个 `uint` ID 只能对应一个 Type；一个 Type 只能对应一个 ID。
- 重复注册、未注册类型打包、未注册 ID 解码、帧头不足都是严重配置/协议错误，应抛异常中断，不能静默回退。
- 注册在单线程初始化阶段完成；之后只读查询可并发使用。
- 打包前由 MessagePack 把对象序列化成 payload，然后调用 `MessageContextPush<T>(ref bytes)`。
- 收到字节后先 `ReadContext(bytes)`，网络层根据 payloadLength 确认完整帧，再按 ID 获取 Type 并交给 MessagePack 反序列化。

## SceneLoader、SmoothNormal

- `LoadSceneManager` 是场景加载入口；不要在业务层随意散落 SceneManager 调用。
- `SmoothNormalBinder` 是平滑法线网格与 Renderer 的序列化绑定组件，由 `Tools/NormalSmooth` 编辑器工具维护；运行时无 Update。它不是角色玩法脚本，不应放进 GAS。

## Gear 使用检查

1. 资源是否保留了 Lease，并在对象结束时 Dispose？
2. 池对象是否正确绑定 Addressable 资源依赖？
3. 全局事件、Timer、PublicMono 回调是否成对取消？
4. 面板附属资源是否加入 `PanelBase.ResourceScope`？
5. 网络层是否只把 InternetMessage 用于帧头，不混入粘包处理？
6. 是否避免在 Update/FixedUpdate 中加载资源、Instantiate/Destroy 或反复 GetComponent？
