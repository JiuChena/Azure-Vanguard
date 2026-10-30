---
tags: [project, framework, behavioreditor, presentation]
created: 2026-10-14
updated: 2026-10-15
---

# 行为表现（BehaviorEditor，BE）

代码位置：`Assets/Scripts/CSharp/Core/Gameplay/BehaviorEditor/`——`Data/`（BehaviorClip 数据）/ `Tracks/`（轨道）/ `Runtime/`（BehaviorExecutor 等执行器）。**纯 C# 表现层，不依赖 UnityEngine 游戏逻辑**；GAS 侧驱动方式见 [[04-模块/GAS-能力与状态机]]。

## 定位

- BE 负责"一个行为片段（BehaviorClip）怎么播"：轨道数据 + 到点分发。`.playable`/TimelineClips 由用户在编辑器制作（养成页动画先例：`Non-Combat/<unitId>/TimelineClips/*.playable`，每页配什么动作由用户自定）
- 行为数据（clip）挂在 UnitConfig SO 的行为表上：键 = 叶子状态类 `public const string BehaviorKey`（见 GAS 篇）

## 播放生命周期约定（GAS×BE 桥接口径）

- 状态 `OnEnter` 调 `Play(key)`、`OnExit` 调 `Stop()`
- `BehaviorAbilityRuntimeData.Reset` 空实现——通用重置不打断表现

## 反向事件（BE → 玩法）

BE 自带完整事件执行链：`EventTrackExecutor` 排序/到点分发/骨骼位姿解析 → `BehaviorEventExecuteSO.Execute`；GAS **不另做事件系统**。

- 需要触达玩法时：按需写项目侧 `ExecuteSO` 子类——Context 已含 `OwnerGameObject`/`OwnerTransform`，`GetComponent` 即达 GAS
- 纯表现类事件子类不碰 GAS

## 编辑器：单位行为快捷选择

代码位置：`Assets/Editor/Gameplay/BehaviorEditor/CoreAuthoring/BehaviorEditorWindow.UnitPicker.cs`（可选 UI）、`BehaviorUnitAssetCollection.cs`（只读收集、精确名称配对）；入口仍为 `Tools > Behavior Editor > Timeline Exporter`。原有 Main/Export/Preview 分工保留，未改变运行时 BehaviorClip 格式。

1. 🖐 展开“单位行为快捷选择（可选）”，拖入场景单位/预制体/UnitConfig SO，按需指定单位资产目录。
2. 🖐 点击“收集 / 刷新单位行为引用（替换列表）”：收集单位层级组件（含非激活）与配置 SO 序列化引用，再补充显式目录下的 TimelineAsset / BehaviorClip。UnitConfig 本身没有 Timeline 表，未被引用的 Timeline 通过单位目录或手动引用补齐。
3. 🖐 展开引用列表，补充、替换或移除资产；支持批量拖入。刷新替换列表，目录外的手动项请在刷新后添加。
4. 🤖 按资产 `Object.name` 精确同名配对，区分大小写；重复对象去重，不同资产同名标记冲突，不猜测后缀、字典键或角色 ID。
5. 🖐 点击配对行“选择”填入旧 Source Timeline / Target BehaviorClip；有效场景单位可作为 Reference Root。预制体和 SO 不自动用于预览根，需指定场景对象。
6. 🖐 用原 Begin Behavior Authoring 编辑，再用 End Editing And Export 导出。

**兼容与防误操作**：快捷区可完全不用，旧手动流程与输出参数保持原样。快捷选择不创建资产、不导出、不创建组件。活动会话或 Play Mode 锁定快捷切换；重名冲突/仅 SO 组禁止快捷选择。仅 Timeline 组会清空旧 Target 并设同名输出名，请确认 Output Folder——该输出路径有同名 SO 时仍按旧规则复用，否则导出时创建。更换单位范围清空快捷列表但不修改旧表单，选择新配对后才切换。

**性能范围**：收集只在按钮触发时执行，配对缓存仅在引用变化/窗口启用时重建；不在重绘时扫描整个项目。序列化 SO 图有循环保护，发现行为资产后不再递归其内部图。显式目录只能选 Assets 子目录，不接受 Assets 根目录。

技术 API 与完整操作说明：`Assets/Technical Documentation/编辑器工具/BehaviorEditor-单位行为快捷选择.md`。
隔离验证脚本：`Assets/Editor/Gameplay/BehaviorEditor/Tests/VerifyBehaviorUnitPicker.cs`（入口 `VerifyBehaviorUnitPicker.Run`）；使用独立 Preview Scene 与唯一临时资产目录，不动真实单位行为资产。编译/脚本跑法见 [[08-踩坑与排查/UnityCLI工作流]]。

## 原生 ActivationTrack / AudioTrack

原生激活、音频轨道已加入既有编译器注册目录，导出为 BehaviorClip 内的 `ActivationTrackData` / `AudioTrackData`，由总执行器自动创建对应执行器。旧动画、事件、Hitbox 与单位快捷选择流程保留；新增轨道需重新导出才写入 SO。

- 🖐 激活轨道绑定单位内 GameObject，音频轨道绑定单位内单一 AudioSource；Reference Root 必须与运行时宿主对应。绑定以相对路径保存，不写入场景对象。
- 🤖 激活片段按区间并集执行，区间外关闭；行为 Stop 执行 Active / Inactive / Revert / LeaveAsIs。重复绑定同对象的激活轨道保留第一条并警告。
- 🤖 音频保留资源、时间区间、裁切、倍速、源循环、静态轨道与片段音量、混入混出曲线；每轨独立原生音频图，支持多轨及重叠片段。读取现有全局音效音量和开关，不覆盖作者 AudioSource 上的普通播放。
- 🤖 运行时只在 Begin 查找路径，Tick 不扫描层级或分配集合；Stop 清理图和输出，输出被宿主连带销毁时兜底清图。
- ⚠️ 未绑定、单位外或歧义路径跳过并警告；找不到目标不回退根节点。激活单位根节点会警告其可能停掉自身 Tick。
- ⚠️ 动画化混音参数暂不导出，明确警告；播放按 BE 帧时间控制，非 Timeline 采样级提前调度。静态声像/空间混合由独立 AudioSource 应用，真实角色听音与空间声效仍需 Play 验证。

代码：运行时 `Tracks/ActivationTrack/`、`Tracks/AudioTrack/`；编译器 `TimelineCompilation/ActivationTrack/`、`TimelineCompilation/AudioTrack/`；共享绑定校验 `TimelineCompilation/Core/NativeTrackBindingExportUtility.cs`。
完整 API、使用步骤和限制见 `Assets/Technical Documentation/编辑器工具/BehaviorEditor-原生激活与音频轨道.md`；隔离验证入口 `VerifyBehaviorNativeTracks.Run`。

## 关联

- [[04-模块/GAS-能力与状态机]] —— 谁在何时播哪个 clip
- [[02-流程SOP/SOP-新加角色]] —— 步骤 4 模型预制体（TimelineClips 用户手调）
