---
tags: [framework, index, onboarding]
created: 2026-09-09
updated: 2026-09-09
---

# Basic Unity Framework 知识库

## 项目定位

这是一个以 Unity 2022.3 LTS 为运行环境的基础框架仓库。框架目标不是提供一套强制的完整游戏玩法，而是提供可组合的基础能力：资源、对象池、音频、事件、定时、UI、输入、能力生命周期、状态机与 Timeline 行为编排。

设计上的核心原则如下：

1. **核心基础设施与业务玩法分离。** `Core/Gear` 解决跨项目可复用的基础问题；角色、技能、战斗数值与具体玩法不应写进 Gear。
2. **GAS 是实体能力的唯一 MonoBehaviour 入口。** 具体能力运行时是纯 C# 对象，由 `GameplayAbilitySystem` 按 SO 配置列表创建和驱动。
3. **Ability 之间通过拥有者上下文共享数据。** 不将旧式 Provider、移动器或输入驱动器作为所有玩法的中心依赖。
4. **BetterHSM 只负责状态与转换仲裁。** 每个实体各自创建状态实例和状态图；状态判断不直接依赖 Unity 输入组件。
5. **BehaviorEditor 核心只调度轨道。** 每种 Timeline 轨道独立完成作者资源、导出与运行时执行，不在核心中添加轨道类型分支。
6. **资源生命周期必须明确。** Addressables 使用 Lease/Scope；对象池与其依赖资源绑定；不在热路径随意加载、实例化或销毁。

## 从这里开始

| 目标 | 先读 |
| --- | --- |
| 理解整体层级、依赖方向和目录 | [[架构总览]] |
| 理解场景启动与每帧运行顺序 | [[运行流程]] |
| 在角色上使用 GAS、输入或 BHSM | [[GAS 与 BetterHSM]] |
| 创建新的 Ability 三件套 | [[新建 Ability 模板]] |
| 编写状态、注册状态图与读取输入 | [[BetterHSM 状态图模板]] |
| 新增 BehaviorEditor Timeline 轨道 | [[BehaviorEditor 轨道模板]] |
| 使用资源、对象池、音频、网络消息等基础模块 | [[Gear 基础模块]] |
| 使用 Timeline 行为导出与运行时播放 | [[BehaviorEditor]] |
| 使用交互组件 | [[交互模块]] |
| 交接给新 Agent 前的检查清单 | [[接手与开发约定]] |

## 代码根目录

| 目录 | 内容 |
| --- | --- |
| `Assets/Scripts/CSharp/Core/Gear/` | 资源、对象池、音频、事件、计时、UI、存档、网络消息等基础设施。 |
| `Assets/Scripts/CSharp/Core/Gameplay/GAS/` | Gameplay Ability System：实体能力容器、能力定义、运行时上下文、输入与 BHSM 接入。 |
| `Assets/Scripts/CSharp/Core/Gameplay/BetterHSM/` | 纯 C# 状态机本体和状态图构建 API。 |
| `Assets/Scripts/CSharp/Core/Gameplay/BehaviorEditor/` | 行为 Clip 数据、运行时执行器和轨道运行时实现。 |
| `Assets/Scripts/CSharp/Core/Gameplay/Interaction/` | 通用范围交互与 UI 选项组件。 |
| `Assets/Editor/Gameplay/` | GAS、BehaviorEditor 的编辑器工具与 Timeline 导出器。 |
| `Assets/Editor/Shader/` | 卡通 Shader 的专属编辑器工具。 |
| `Assets/Settings/` | 输入、能力和 URP 的项目配置资产。 |

## 当前启动资产

- 构建场景：`Assets/Scenes/Preload.unity`。
- 预加载组件：`Assets/Scripts/CSharp/Core/Preloader.cs`。
- 输入动作资产：`Assets/Settings/Input Settings/Player.inputactions`。
- 输入能力配置示例：`Assets/Settings/Ability Configurations/InputListenerAbility.asset`。

## 当前不属于框架的内容

以下内容已在本轮重构中删除或明确不再作为框架入口，后续不得为了兼容旧实现重新接入：

- `DataProvider` 体系。
- `UnitMover` 聚合器体系。
- 将移动、跳跃、浮动胶囊体、边缘保护等玩法揉进同一个移动组件的做法。
- CH0221 的 BetterHSM 测试状态图与测试枚举项。

未来若需要移动、跳跃、浮动胶囊体或边缘保护，应将它们实现为独立的 Gameplay Ability 或可由 Ability Runtime 持有的纯 C# 功能模块，而不是恢复旧框架。

## 文档维护规则

1. 改动公共 API、生命周期、目录或运行流程时，同步更新对应模块笔记。
2. 新增 Ability、状态图注册方案、轨道类型或 Gear 模块时，至少补充一份使用说明或模板链接。
3. 文档描述当前代码行为；计划、猜测和已删除旧方案必须显式标注，不能伪装成现状。
4. 新 Agent 开始实现前先读 `HOME.md`、相关模块文档和实际源码；知识库用于建立方向，源码是最终事实来源。
