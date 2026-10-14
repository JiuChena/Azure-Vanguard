---
tags: [project, debugging, addressables]
created: 2026-10-14
updated: 2026-10-14
---

# Addressables 与资源踩坑

> 框架 API 与三类加载方式见 [[03-框架/资源加载-AddressableManager]]。本篇只记事故与坑。

## 坑一：编辑态验证死路 → LocalFileTableSource

**现象**：AgentScripts 的 Verify 脚本在 UnityCLI 内跑表驱动逻辑（养成结算等），等 Addressables 异步加载链 → 死锁/超时。

**解法**：验证脚本开头注入直读源，绕开 Addressables 整条链：

```csharp
DataTableManager.Instance.SetSource(new LocalFileTableSource());
// 直读 Assets/Module/<模块>/ .bytes、已完成态 Task，整条 await 链同步完成
```

VerifyCharacterProgression 当年 24/24 全绿正是因为注入了这个源；后来某轮验证脚本漏注入才复现死锁。**规则：凡 Verify 脚本跑表逻辑，第一行先 SetSource**（[[06-模板/模板-AgentScripts]] Verify 纪律）。

## 坑二：组名与标签，拿不准先看盘上文件名

- SO 配置表组的真名是 **`Configuration-UnitConfig`**，不是 `UnitConfigs`——按记忆里的名字找组会扑空
- 模型组 `Prefab-UI-CultivatePanel-Characters` 带 **UI + Non-Combat 双标签**（决定打包与加载作用域）
- 组会随迭代合并/改名（如 CultivatePanel-Characters 组并入 Panels-Normal），**盘上 `Assets/AddressableAssetsData/AssetGroups/` 的文件名才是唯一事实**

## 坑三：`RemoveAssetEntry(guid, postEvent: false)` 的幽灵条目

清条目用 `postEvent: false` 不发变更事件 → 开着的 Addressables Groups 窗口不重建树 → 窗口里剩**幽灵条目**，按 Delete 报 NRE（真身已删 `parentGroup=null`，树缓存的旧节点遍历到 null 组）。

- 盘上复核干净后**只需重开窗口**，不是真丢数据
- 编辑器驱动条目增删要走 `postEvent: true`（或删完手动让窗口刷新）；`false` 只适合无人看窗口的 CLI 场景

## 坑四：租约竞态——"资源加载失败"但操作本身成功

两条链同地址进 `ShowModelAsync` → AddressableManager `GetOrLoadEntryAsync` 去重并发、共享同一 entry。输家链从 await 醒来时发现令牌已失效 → `lease.Dispose()`；若这步落在「赢家 await 完成后、CreateLease 前」的窗口，引用计数被砸到 0 → entry 释放句柄并从 resources 摘除 → 赢家拿到已死 entry → `IsLoaded=false` → 包装层**静默抛"资源加载失败"**（操作本身成功，无 Addressables 异常日志，故难查）。

业务层现行防护：`ShowModelAsync` 同角短路（同 unitId 直接 return）+ 令牌失效校验（[[04-模块/面板-CultivatePanel]]）。框架层兜底（释放后重载或广播合并）挂账 [[05-玩法设计/设计挂账]]——动框架另议。

## 坑五：1.22.3 API 差异（编译期就撞）

- 集合求交是 `Intersection()`，不是 LINQ 的 `Intersect()`
- 部分返回值要显式 `(IEnumerable<…>)` 转换，隐式 var 推导会编译错

全表见 [[03-框架/资源加载-AddressableManager]]。

## 关联

- [[08-踩坑与排查/UnityCLI工作流]] — CLI 内跑 Addressables 相关验证的综合限制
- [[06-模板/模板-AgentScripts]] — Verify 脚本注入 LocalFileTableSource 惯例
