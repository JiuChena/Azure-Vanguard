---
tags: [references, external]
created: 2026-06-20
updated: 2026-10-14
---

# 外部参考资料

## Unity 官方文档

- [Unity Manual](https://docs.unity3d.com/Manual/index.html)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/index.html)
- [Unity UGUI Documentation](https://docs.unity3d.com/Packages/com.unity.ugui@latest)
- [Unity URP Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
- [Unity Netcode for GameObjects](https://docs-multiplayer.unity3d.com/netcode/current/about/)

## C# / .NET 参考

- [Microsoft C# Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/)
- [.NET API Reference](https://learn.microsoft.com/en-us/dotnet/api/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

## 图形学

- [Learn OpenGL](https://learnopengl.com/) - 图形学入门经典
- [Real-Time Rendering](https://www.realtimerendering.com/) - 实时渲染权威参考
- [GPU Gems](https://developer.nvidia.com/gpugems) - NVIDIA GPU 编程精华

## 网络编程

- [Beej's Guide to Network Programming](https://beej.us/guide/bgnet/)
- [Gaffer on Games - Networking](https://gafferongames.com/) - 游戏网络编程经典

## 推荐书籍

- 《Unity 实战》- Unity in Action
- 《CLR via C#》- Jeffrey Richter
- 《游戏编程模式》- Game Programming Patterns
- 《深入理解 C#》- C# in Depth
- 《3D 数学基础：图形与游戏开发》
- 《实时渲染》- Real-Time Rendering
- 《TCP/IP 详解》

## 在线学习

- [Unity Learn](https://learn.unity.com/)
- [Catlike Coding](https://catlikecoding.com/) - Unity 渲染教程

## 通用方案对比表（面试用，自原决策记录归档 2026-10-14）

> 面向面试的通用对比，非项目定稿。本项目实际选型见 [[09-决策记录/决策记录]]（资源=Addressables、渲染=URP、热更与同步未开工未选型）。

### 热更新方案对比

| 方案 | 适用场景 | 推荐度 |
|------|----------|--------|
| xLua | 大型项目，需要频繁热更 | ⭐⭐⭐⭐⭐ |
| HybridCLR | 新项目，Unity 2021+ | ⭐⭐⭐⭐ |
| ILRuntime | 不想引入 Lua | ⭐⭐⭐ |
| ToLua | 已有 ToLua 项目 | ⭐⭐⭐ |

### 网络同步方案

| 方案 | 适用场景 | 复杂度 |
|------|----------|--------|
| 帧同步 | RTS、MOBA、格斗 | 高 |
| 状态同步 | MMO、FPS | 中-高 |
| 混合方案 | 大型项目 | 高 |
