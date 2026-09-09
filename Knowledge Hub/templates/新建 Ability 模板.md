---
tags: [template, GAS, ability]
created: 2026-09-09
updated: 2026-09-09
---

# 新建 Ability 模板

## 先判断是否真的需要 Ability

适合创建 Ability：

- 功能属于一个实体，且需要 GAS 生命周期；
- 需要读取或发布 `AbilityOwnerContext` 数据；
- 需要在 SO 中保存可复用静态配置；
- 需要以列表顺序与其他能力协作。

不适合创建 Ability：

- 纯工具函数、算法或值对象；应是普通纯 C# 类。
- 全局资源、全局事件、全局对象池；应使用 Gear。
- 特定状态的局部逻辑；应属于对应 `StateBase`。
- 某个 BehaviorEditor 轨道的时间线行为；应属于轨道 Executor。

## 自动生成工具

在 Unity 菜单选择：

```text
Tools/GAS/Create New Ability Scripts
```

先选中目标文件夹；工具会要求输入 Ability 名称并生成三份脚本：

```text
<AbilityName>AbilitySO.cs
<AbilityName>AbilityRuntime.cs
<AbilityName>AbilityRuntimeData.cs
```

它同时假设 `AbilityRuntimeDataType` 已存在对应枚举项。若没有，先添加语义键，再使用生成器。

## 三件套职责

| 脚本 | 保存内容 | 不应该保存 |
| --- | --- | --- |
| `XxxAbilitySO` | 可序列化、跨实体可复用的静态配置。 | 当前角色组件、当前帧数据、订阅句柄。 |
| `XxxAbilityRuntime` | 当前实体独占的逻辑、缓存组件、事件订阅和模块实例。 | 跨实体共享的可变状态。 |
| `XxxAbilityRuntimeData` | 需要给其他 Ability 或 BHSM 读取的实体运行时数据。 | Unity 组件查找、复杂控制逻辑。 |

## 最小骨架

### 1. RuntimeData 与枚举键

```csharp
namespace Framework.Gameplay.Abilities
{
    public sealed class DashAbilityRuntimeData : IAbilityRuntimeData
    {
        public bool IsDashing { get; private set; }

        public void SetDashing(bool isDashing)
        {
            IsDashing = isDashing;
        }

        public void Reset()
        {
            IsDashing = false;
        }
    }
}
```

在 `AbilityRuntimeDataType` 中增加：

```csharp
Dash,
```

同一语义键只能由一个 Ability 注册。若功能不需要对外共享数据，不必创建 RuntimeData，也不应强行添加枚举值。

### 2. 配置 SO

```csharp
using UnityEngine;

namespace Framework.Gameplay.Abilities.Configuration
{
    [CreateAssetMenu(fileName = "DashAbility", menuName = "Framework/Gameplay/Abilities/Dash")]
    public sealed class DashAbilitySO : AbilityDefinitionSO
    {
        [Min(0f)]
        [SerializeField] private float _speed = 8f;

        public float Speed => _speed;

        public override AbilityRuntime CreateRuntime()
        {
            return new DashAbilityRuntime(this);
        }
    }
}
```

### 3. Runtime

```csharp
using Framework.Gameplay.Abilities.Configuration;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    public sealed class DashAbilityRuntime : AbilityRuntime
    {
        private readonly DashAbilitySO _configuration;
        private DashAbilityRuntimeData _runtimeData;
        private CharacterController _characterController;

        public DashAbilityRuntime(DashAbilitySO configuration)
        {
            _configuration = configuration;
        }

        public override void AbilityInit(AbilityOwnerContext ownerContext)
        {
            base.AbilityInit(ownerContext);
            if (ownerContext == null || ownerContext.Owner == null) return;

            _characterController = ownerContext.Owner.GetComponent<CharacterController>();
            _runtimeData = new DashAbilityRuntimeData();
            OwnerContext.Register(AbilityRuntimeDataType.Dash, _runtimeData);
        }

        public override void AbilityUpdate(float deltaTime)
        {
            // 读取输入、状态或业务请求，在这里决定冲刺。
        }

        public override void AbilityDispose()
        {
            OwnerContext?.Unregister(AbilityRuntimeDataType.Dash, _runtimeData);
            _runtimeData = null;
            _characterController = null;
            base.AbilityDispose();
        }
    }
}
```

## 组件依赖规则

谁使用组件，谁在自己的 Runtime 中获取并缓存。例如 Dash 要用 CharacterController，则 Dash Runtime 在 `AbilityInit` 获取。不要为了方便，把 CharacterController、Rigidbody、Collider、Animator、PlayerInput 一律塞进 `GameplayAbilitySystem`。

依赖可以分三类处理：

| 情况 | 做法 |
| --- | --- |
| 必需且可自动补齐 | 在 `AbilityInit` 获取，缺失时添加并明确初始化。 |
| 必需但不应自动创建 | 缺失时输出清晰错误并停用本 Ability 的行为。 |
| 可选能力 | 获取失败后退化执行，不能让整个 GAS 崩溃。 |

## 生命周期选择

| 需要做的事 | 放置位置 |
| --- | --- |
| 获取组件、注册数据、创建纯 C# 子模块 | `AbilityInit` |
| 开关 InputAction、事件订阅恢复 | `AbilityOnEnable` |
| 需要等其他 Ability 初始化完成 | `AbilityStart` |
| 输入、状态判断、普通表现 | `AbilityUpdate` |
| Rigidbody 或固定物理计算 | `AbilityFixedUpdate` |
| 相机/动画等最终跟随表现 | `AbilityLateUpdate` |
| 取消短期状态、停用监听 | `AbilityOnDisable` |
| 解绑事件、注销数据、释放 Scope | `AbilityDispose` |

## 编辑器绘制

若 Ability 需要 Scene Gizmo，可在 SO 重写：

```csharp
public override void GizmoDraw(GameObject owner)
{
    if (owner == null || !_showGizmo) return;
    Gizmos.DrawWireSphere(owner.transform.position, _radius);
}
```

Gizmo 只读取 SO 序列化参数和 `owner`。不要调用 `CreateRuntime`，不要添加组件，不要在编辑模式模拟游戏逻辑。

## 接入检查

1. 创建 SO 资产并填好静态配置。
2. 将 SO 放到目标实体 `GameplayAbilitySystem` 的列表中。
3. 将数据生产者排在消费者前面。
4. 若依赖输入，把 Input Ability 排在本 Ability 前面。
5. 若需要读取状态，把 BetterHSM Ability 排在本 Ability 前面。
6. 确认 `AbilityDispose` 注销数据、取消订阅、释放资源。
7. 修改后检查 Unity Console 零编译错误。
