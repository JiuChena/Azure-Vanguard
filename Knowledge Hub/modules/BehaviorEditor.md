---
tags: [module, BehaviorEditor, timeline, tracks]
created: 2026-09-09
updated: 2026-09-09
---

# BehaviorEditor

## 定位

BehaviorEditor 将 Unity Timeline 的作者期轨道导出为可运行的 `BehaviorClip` ScriptableObject，并由纯 C# `BehaviorExecutor` 推进。它不规定“攻击”“特效”“相机震动”等具体业务；具体业务应作为独立轨道或 ExecuteSO 实现。

## 核心链路

```text
TimelineAsset
    -> 轨道 Compiler Export
    -> BehaviorExportContext
    -> BehaviorClip
        -> BehaviorPlaybackSettings
        -> List<BehaviorTrackData>
    -> BehaviorExecutor.Play
    -> IBehaviorTrackExecutor.Begin / Tick / Stop
```

## BehaviorClip

`BehaviorClip` 是运行时行为资产，包含：

- `BehaviorPlaybackSettings`：时长、速度倍率、`WrapMode`；
- 多态 `BehaviorTrackData` 列表。

每种 TrackData 只有一个权威实例。导出时，`SetTrackData` 会替换相同数据类型的旧值；`ReplaceTrackData` 会清除整个旧集合并按类型去重。

运行时不再读取 Timeline。Timeline 仅是作者期资产，导出后的 BehaviorClip 才是游戏运行时数据来源。

## BehaviorExecutor 的使用

业务宿主自行持有并驱动：

```csharp
private BehaviorExecutor _behaviorExecutor;

private void Awake()
{
    _behaviorExecutor = new BehaviorExecutor(gameObject);
}

private void Update()
{
    _behaviorExecutor.Tick(Time.deltaTime);
}

public void PlayBehavior(BehaviorClip clip)
{
    _behaviorExecutor.Play(clip);
}

private void OnDrawGizmos()
{
    _behaviorExecutor?.DrawGizmos();
}

private void OnDestroy()
{
    _behaviorExecutor?.Stop();
}
```

`Play` 会停止当前行为、创建该次播放独占的轨道执行器、按 `ExecutionOrder` 排序并调用 `Begin`。`Stop` 必定调用所有轨道的 `Stop`。

### WrapMode

| WrapMode | 行为 |
| --- | --- |
| `Once` | 到时停止，并触发 `OnCompleted`。 |
| `Loop` | 到时回卷播放头、停止旧执行器并重建下一轮执行器。 |
| `ClampForever` | 到达末尾后停在最后时间，不自动停止。 |

## 已有轨道

| 轨道 | 作者资源 | 运行时职责 |
| --- | --- | --- |
| MetaTrack | `BehaviorTimelineMetaTrack` | 导出播放设置，不创建运行时执行器。 |
| AnimationTrack | Timeline Animation Clip | 解析 Animator 或 `IBehaviorAnimationPlayer`，播放片段。 |
| HitboxTrack | `BehaviorTimelineHitboxTrack` | 在时间窗内进行 NonAlloc 物理命中查询，并调用 `HitExecuteSO`。 |
| EventTrack | `BehaviorTimelineEventTrack` | 在时间点构造 `BehaviorEventContext`，调用业务 `BehaviorEventExecuteSO`。 |

MetaTrack 是 Timeline 作者期对 `BehaviorPlaybackSettings` 的填写入口。它不是普通运行时轨道，因此不会生成 `BehaviorTrackData` 或 Executor。

## 编辑器入口

| 菜单 | 用途 |
| --- | --- |
| `Tools/Behavior Editor/Timeline Exporter` | 打开 Timeline 导出与作者期会话窗口。 |
| `Tools/Behavior Editor/Create New Track Scripts` | 生成最小新轨道的五份脚本。 |
| `Framework/Behavior Editor/Animator Controller Setup` | 创建或设置 BehaviorEditor 需要的 Animator Controller 约定。 |

## 导出机制

每个 Timeline 轨道 Compiler 实现 `IBehaviorTimelineTrackCompiler`，并标记：

```csharp
[BehaviorTrackCompiler(typeof(MyTimelineTrack))]
```

`BehaviorTrackCompilerCatalog` 使用 `TypeCache` 自动发现这类 Compiler；新增轨道不需要往窗口、总执行器或集中注册表加 `if (track is ...)`。

### `Export`

`Export(TrackAsset track, BehaviorExportContext context)` 只读取自己认识的 Timeline 轨道与 Clip，把数据复制到导出上下文中，并调用：

```csharp
context.ConsiderEndTime(clip.end);
```

以保证总行为时长覆盖轨道末尾。

### `Commit`

只有轨道在导出期需要跨多条同类轨道收集、合并、统一排序或最终一次写入时才需要 `Commit`。

例如 EventTrack 与 HitboxTrack 会收集多段数据后排序并写入一个权威 TrackData，因此使用 `IBehaviorTrackExportState` 和 `Commit`。

如果轨道的每次导出都能直接确定唯一 TrackData，且不存在多条同类 Timeline 轨道的合并需求，就不必为了形式而创建 Commit。

## 轨道之间的边界

- `BehaviorExecutor` 只负责播放头与 `IBehaviorTrackExecutor` 调度，不认识 Animation、Hitbox、Event 等类型。
- TrackData 只创建自己的 Executor，不查其他轨道的数据或 Executor。
- 轨道业务交互通过业务 `ExecuteSO`、共享业务 Ability 或上层状态机组织。
- 不把 Camera、伤害、特效、角色控制器等具体依赖塞入 `BehaviorExecutionContext`。它只提供宿主、TransformResolver 与播放速度。
- 轨道有 Scene Gizmo 需求时，实现 `IBehaviorTrackGizmoDrawer`；总执行器只做接口分发。

新增轨道的完整步骤见 [[BehaviorEditor 轨道模板]]。
