---
tags: [project, template, gas, ability]
created: 2026-10-14
updated: 2026-10-14
---

# 模板-GAS能力

> 新 GAS 能力（AbilitySO + Runtime + RuntimeData）三件骨架。桥接总纲见 [[04-模块/GAS-能力与状态机]]；先例：`Core/Gameplay/GAS/Behavior/BehaviorAbilitySO.cs`（+ Runtime/RuntimeData）。

## 命名空间

GAS 族在 `Framework.Gameplay.Abilities`；**SO 资产类在 `.Configuration` 子命名空间**（`Framework.Gameplay.Abilities.Configuration`）——漏 using 高发（SetupCH0221HierHsm 曾踩）。

## 三件骨架

```csharp
// 1) AbilitySO —— Configuration 子命名空间，资产载体
namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>XXX 能力配置。</summary>
    [CreateAssetMenu(menuName = "Azure Vanguard/Abilities/XXX")]
    public class XXXAbilitySO : AbilitySO   // 基类按 AbilitySO 实际类型
    {
        [SerializeField] private bool logXxxFlow;   // 调试开关（先例 _logBehaviorFlow）
    }
}

// 2) AbilityRuntime —— 建执行器、Tick、注册 RuntimeData
public class XXXAbilityRuntime : AbilityRuntime
{
    protected override void OnAbilityInit()
    {
        // 建执行器/注册更新
        // 注册 RuntimeData：AbilityOwnerContext 上下文
    }

    protected override void OnAbilityTick()
    {
        // 常开 Tick（若需要）
    }
}

// 3) AbilityRuntimeData —— 状态机按键驱动的操作面
public class XXXAbilityRuntimeData : AbilityRuntimeData
{
    public void Play(string key)
    {
        // 表查 clip → executor.Play；缺失分级 Log 并忽略，不崩逻辑层
    }

    public void Stop() { /* ... */ }

    public override void Reset()
    {
        // 通用重置不打断表现时留空（先例：BehaviorAbilityRuntimeData.Reset 空实现）
    }
}
```

## 框架增量登记

新增能力时同步：

| 项 | 位置 |
|---|---|
| `AbilityRuntimeDataType` 键 | 框架枚举（新能力类型加键） |
| `AbilityOwnerContext` 参数 | 需要UnitConfig 时用可选参数 |
| 纯数据一律进 Excel 表不进 SO | 判定标准：服务端要不要读（[[04-模块/角色养成系统]] 分层边界） |

## SO 资产落地

`Assets/Settings/Ability Configurations/<名>.asset`——用 AgentScripts 幂等创建（SerializedObject 写 `_entityCategory`/`_registrationId` enumValueIndex + 盘上回读确认），先例 `SetupCH0221HierHsm.cs`；**预制体能力列表接线**先例 `SetupCH0221Gas.cs`。

## 检查单

- [ ] using 带 `.Configuration`（SO 类）
- [ ] SO 只装表现绑定（图标/clip/Prefab 引用），数值走表
- [ ] RuntimeData 缺失键分级 Log 不抛异常（不崩逻辑层）
- [ ] Reset 不打断表现（按先例口径）
- [ ] 新类型键登记 AbilityRuntimeDataType
