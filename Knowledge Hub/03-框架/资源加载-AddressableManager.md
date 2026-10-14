---
tags: [project, framework, addressables, resource]
created: 2026-10-14
updated: 2026-10-14
---

# 资源加载（AddressableManager）

代码位置：`Assets/Scripts/CSharp/Core/Gear/ResourceSystem/`——`AddressableManager.cs` / `ResourceScope.cs` / `IconLibrary.cs`。Addressables 1.22.3。

## 核心语义

- Lease/Scope 引用计数；resourceKey = `FullTypeName::key`（同 key 同类型共享缓存条目）
- **scope 一律调用方自备，内部不创建**：不传则 Lease 需手动 Dispose；面板场景传面板自己的 scope 随关闭整批释放；常驻走 `ResourceScope.Persistent`

## 三类加载 API

| 类别 | API | 语义 |
|------|-----|------|
| 资源地址 | `AcquireAssetAsync<T>(key, scope)` / `AcquirePersistentAssetAsync<T>(key)` | 单 key 加载 |
| 按标签批量 | `AcquireAssetsByLabelsAsync<T>(labels, scope)`（并集）/ `AcquireAssetsByLabelsIntersectAsync<T>(labels, scope)`（交集） | 多标签解析命中地址后逐个走缓存加载，返回 `List<ResourceLease<T>>`，整批注册 scope、也可单个提前释放 |
| 地址+标签 | `AcquireAssetWithLabelsAsync<T>(key, requiredLabels, scope)` | 地址命中的资源必须带全部指定标签（解析用交集），否则抛异常；标签为空退化为按地址加载 |

批量实现要点：先 `LoadResourceLocationsAsync(keys, mergeMode, typeof(T))` 解析命中地址（取 PrimaryKey，临时 Handle 即刻释放不进缓存），再两遍式——先全部加载成功、再统一建 Lease 注册 scope，**任一失败抛异常且不产生半批 Lease**；与单 key 加载共享同一缓存条目（重复加载不重复占内存）。

## Addressables 1.22.3 API 坑

- `MergeMode` 交集成员叫 **`Intersection`**，没有 `Intersect`（编译期 CS0117）
- `LoadResourceLocationsAsync(IList<object>, MergeMode, Type)` 已 `[Obsolete]`；传 `List<object>` 会精确匹配到它，需 `(IEnumerable)keys` 显式转换绑定现行 `IEnumerable` 重载

## IconLibrary（图标统一通道）

代码位置：`Core/Gear/ResourceSystem/IconLibrary.cs`。

- 单图 Addressables 约定（2026-10-08 拍板，弃图集）：png 按变体入组，地址 = 文件名去扩展
- 头像：`GetCharacterAvatarAsync(IconLibrary.VariantSmall, unitId)`（png 子资源走「名[名]」子地址语法）
- 通用：`GetAsync(名)` —— 内部拼子地址、缓存 + 并发去重 + Persistent Lease，失败返回 null 保留默认图
- **池化单元格异步回包必须校验 `cell.unitId` 未改绑（防串图）**

| 图标族 | 命名 | Addressables 组 |
|---|---|---|
| 头像 | `CHAvatar_{Small,Collection,SpecialPopup,PlayerAvatar}_<unitId>` | `Sprite-Character-Avatar-{变体}` |
| 武器 | `CHWeapon_<unitId>` | `Sprite-Character-Weapon-Icon` |
| 饰品 | `Trinket_Icon_<款式>` | `Sprite-ItemIcon-Trinket` |

## UnitConfigLibrary（单位配置库）

代码位置：`Core/Gameplay/GAS/Core/UnitConfigLibrary.cs`。

- 静态库 `GetAsync(id)` / `TryGetCached` / `PreloadAsync(list)`；`AcquirePersistentAssetAsync` 常驻不释放（与 AddressableTableSource 同口径），pending 表并发去重，失败不入缓存可重试；按 `CharacterUnitConfig` 子类型加载
- SO 资产地址 = unitId，组名 **Configuration-UnitConfig**（⚠️ 不是 "UnitConfigs"——2026-10-08 纠正）；模型预制体与 SO **同址不同型**，用标签交集过滤：`AcquireAssetWithLabelsAsync<GameObject>(unitId, ["UI","Non-Combat"])`

## 关联

- [[08-踩坑与排查/Addressables与资源]] —— 编辑态验证死路、组配置坑
- [[02-流程SOP/SOP-数据表流水线]] —— 配置表入组流程
- [[08-踩坑与排查/SpriteAtlas三坑]] —— 图集仍在用的场景（HomePanel 等）
