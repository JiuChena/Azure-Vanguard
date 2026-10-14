---
tags: [lua, hotfix, unity]
created: 2026-06-20
updated: 2026-06-20
---

# Lua 热更新

## 概述

Lua 热更新是 Unity 手游开发中常用的技术方案，允许在不重新发布 App 的情况下修复 Bug 和更新游戏逻辑。

## Lua 八大基本类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `nil` | 空值 | `local a = nil` |
| `boolean` | 布尔 | `local b = true` |
| `number` | 数字（整数浮点统一） | `local n = 3.14` |
| `string` | 字符串 | `local s = "hello"` |
| `table` | 表（数组+字典+对象一体） | `local t = {1, key = "v"}` |
| `function` | 函数是一等公民 | `local f = function() end` |
| `userdata` | C/C++ 传入的自定义数据 | |
| `thread` | 协程 | `coroutine.create(f)` |

**记忆**: nil/boolean/number/string/table/function/userdata/thread

---

## 核心知识点

### 1. 热更新方案对比

| 方案 | 原理 | 优点 | 缺点 |
|------|------|------|------|
| **xLua** | 将 C# 类注册到 Lua，支持 Hotfix 标签 | 腾讯开源，文档完善 | 需适配各平台 |
| **ToLua** | 基于 LuaInterface，静态绑定 | 性能较好 | 配置复杂 |
| **ILRuntime** | IL 解释器，纯 C# 实现 | 不需要 Lua | 性能略低于 Lua |
| **HybridCLR** | 原生 IL2CPP 热更新方案 | Unity 官方支持 | 较新，生态在发展中 |

### 2. xLua 基本使用

```csharp
// 初始化
LuaEnv luaEnv = new LuaEnv();
luaEnv.DoString("print('Hello from Lua')");

// 调用 Lua 函数
luaEnv.DoString(@"
    function add(a, b)
        return a + b
    end
");
```

### 3. Hotfix 原理

- 通过 `[Hotfix]` 标签标记需要热更新的方法
- xLua 在 IL 层面替换方法实现，跳转到 Lua 逻辑
- 需要在 Editor 下生成适配代码

## 面试高频题

### Q1: 为什么要用热更新？
**难度**: 🟡
**回答**: 手游发版周期长（App Store 审核），热更新可以快速修复线上 Bug、更新活动内容、调整游戏数值，无需用户重新下载安装包。

### Q2: xLua 和 ToLua 的区别？
**难度**: 🟡
**回答**: xLua 支持 Hotfix 特性更完善，可热补丁 C# 方法；ToLua 使用静态绑定性能更好但配置更复杂。xLua 社区活跃度更高。

## 最佳实践

- 核心战斗逻辑用 C# 保证性能，业务逻辑用 Lua 保证灵活性
- 热更新资源使用 AssetBundle 管理
- 做好版本兼容和回滚机制

## 关联模块

- [[memory-gc|内存管理与 GC]]
- [[performance-optimization|性能优化]]
