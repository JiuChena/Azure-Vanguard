---
tags: [template, BehaviorEditor, timeline, track]
created: 2026-09-09
updated: 2026-09-09
---

# BehaviorEditor 轨道模板

## 自动生成最小五件套

Unity 菜单：

```text
Tools/Behavior Editor/Create New Track Scripts
```

工具要求填写轨道名称和输出路径，并把路径保存到本地 EditorPrefs。它生成：

```text
MyTrack/
    BehaviorTimelineMyTrack.cs
    BehaviorTimelineMyClipAsset.cs
    MyTrackData.cs
    MyTrackExecutor.cs
    MyTimelineTrackCompiler.cs
```

生成后仍需要填写片段参数、导出复制和运行时 Tick 逻辑。

## 文件职责

| 文件 | 阶段 | 职责 |
| --- | --- | --- |
| `BehaviorTimelineMyTrack` | Timeline 作者期 | 声明可容纳哪种 ClipAsset。 |
| `BehaviorTimelineMyClipAsset` | Timeline 作者期 | 保存单段作者参数；Timeline 决定时间。 |
| `MyTimelineTrackCompiler` | 编辑器导出 | 将作者数据转成运行时数据。 |
| `MyTrackData` | 运行时资产 | 保存已导出的纯数据，创建 Executor。 |
| `MyTrackExecutor` | 每次播放 | 缓存组件、处理时间窗、调用业务。 |

可选文件：

```text
MyTrackExportState.cs            多条同类轨道需合并/排序时。
MyClipAssetEditor.cs             自定义 Inspector 时。
MyAuthoringParticipant.cs        需要开始/结束作者期预览时。
MyExecuteSO.cs / MyContext.cs    需要让业务层自定义最终执行时。
```

## 最小作者期轨道

```csharp
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    [TrackClipType(typeof(BehaviorTimelineCameraShakeClipAsset))]
    public sealed class BehaviorTimelineCameraShakeTrack : TrackAsset
    {
    }
}
```

```csharp
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    public sealed class BehaviorTimelineCameraShakeClipAsset : PlayableAsset, ITimelineClipAsset
    {
        [Min(0f)] public float amplitude = 1f;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);
        }
    }
}
```

不要把 Timeline 的 `start`、`duration` 再复制为独立、可编辑的运行时来源。时间范围应由编译器读取 `TimelineClip.start/end/duration` 导出。

## 最小运行时数据与执行器

```csharp
using System;
using UnityEngine;

namespace BehaviorEditor
{
    [Serializable]
    public sealed class CameraShakeDefinition
    {
        public float startTime;
        public float endTime;
        public float amplitude;
    }

    [Serializable]
    public sealed class CameraShakeTrackData : BehaviorTrackData
    {
        public CameraShakeDefinition[] definitions;

        public override string DisplayName => "Camera Shake";

        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new CameraShakeTrackExecutor(this, context);
        }
    }
}
```

```csharp
namespace BehaviorEditor
{
    public sealed class CameraShakeTrackExecutor : IBehaviorTrackExecutor
    {
        private readonly CameraShakeTrackData _data;
        private readonly BehaviorExecutionContext _context;

        public CameraShakeTrackExecutor(CameraShakeTrackData data, BehaviorExecutionContext context)
        {
            _data = data;
            _context = context;
        }

        public int ExecutionOrder => _data.executionOrder;

        public void Begin()
        {
            // 缓存此轨道需要的业务适配器或组件。
        }

        public void Tick(float elapsedTime)
        {
            // 仅处理属于本轨道的时间窗和业务调用。
        }

        public void Stop()
        {
            // 撤销这次播放产生的临时状态。
        }
    }
}
```

如果宿主不存在所需业务组件，轨道自行决定跳过、告警或通过接口适配；不要要求 `BehaviorExecutor` 增加组件字段。

## Compiler：单轨道直接导出

```csharp
using System.Collections.Generic;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

namespace BehaviorEditor.Editor
{
    [BehaviorTrackCompiler(typeof(BehaviorTimelineCameraShakeTrack))]
    public sealed class CameraShakeTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        public System.Type TrackType => typeof(BehaviorTimelineCameraShakeTrack);

        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            var cameraShakeTrack = track as BehaviorTimelineCameraShakeTrack;
            if (cameraShakeTrack == null) return;

            var definitions = new List<CameraShakeDefinition>();
            foreach (TimelineClip timelineClip in cameraShakeTrack.GetClips())
            {
                var asset = timelineClip.asset as BehaviorTimelineCameraShakeClipAsset;
                if (asset == null) continue;

                definitions.Add(new CameraShakeDefinition
                {
                    startTime = (float)timelineClip.start,
                    endTime = (float)timelineClip.end,
                    amplitude = asset.amplitude
                });
                context.ConsiderEndTime(timelineClip.end);
            }

            context.GetOrCreateTrackData<CameraShakeTrackData>().definitions =
                definitions.ToArray();
        }
    }
}
```

实际项目中使用的命名空间和 API 以同目录现有 Compiler 为准。重点是：Attribute 的 TrackType、`TrackType` 属性与实际 Track 类必须是同一个类型。

## 多条同类轨道时使用 Commit

若 Timeline 可以有多条 `BehaviorTimelineCameraShakeTrack`，且全部片段最终要合并为一个 `CameraShakeTrackData`：

1. 定义 `CameraShakeTrackExportState : IBehaviorTrackExportState`；
2. 每次 `Export` 把定义追加到该 State；
3. 在 `CameraShakeTrackExportState.Commit(BehaviorExportContext)` 排序，并通过 `GetOrCreateTrackData<CameraShakeTrackData>()` 一次性写入；
4. `BehaviorExportContext.CommitTo(BehaviorClip)` 会在所有轨道导出结束后调用每个 ExportState 的 `Commit`；
5. 不要让多个 Compiler 调用分别覆盖同一种 TrackData。

EventTrack 和 HitboxTrack 是现有参考实现；AnimationTrack 的实现方式则适合直接导出场景。

## 可选作者期参与者

只有轨道确实需要创建临时 Animator、预览物或绑定 PlayableDirector 时，才实现 `IBehaviorAuthoringParticipant`。参与者实例属于一次 `BehaviorAuthoringSessionContext`，不要把“本轮创建的对象”存进静态全局缓存。

结束作者期时要在 `EndAuthoring` 撤销本轨道自己的临时资源。不要由别的轨道或窗口直接清理。

## 新增轨道禁止项

- 不修改 `BehaviorExecutor` 来识别新轨道类型。
- 不在 BehaviorEditor 窗口加新轨道类型判断。
- 不改一个集中注册表；Compiler Attribute 已负责自动发现。
- 不把业务相机、伤害、特效、角色控制器塞进 `BehaviorExecutionContext`。
- 不让一个轨道直接读取或调用另一个轨道的 Executor。
- 不把具体“发射特效”“造成伤害”等玩法分类做进核心 EventTrack；按 HitboxTrack 的模式让业务继承 ExecuteSO。

## 完成检查

1. Timeline Track 与 Clip 可正常创建。
2. Compiler 被 `BehaviorTrackCompilerAttribute` 发现。
3. Export 复制了全部自定义参数，并调用 `ConsiderEndTime`。
4. 多轨合并时实现 Commit；无需合并时不增加无价值 Commit。
5. TrackData 只保存运行时所需的纯数据。
6. Executor 在 `Stop` 清理本轮状态。
7. 新轨道没有修改调度核心。
