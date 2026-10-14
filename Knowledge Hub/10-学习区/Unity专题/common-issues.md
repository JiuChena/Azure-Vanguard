---
tags: [debugging, troubleshooting]
created: 2026-06-20
updated: 2026-06-20
---

# 常见问题排查

## Unity 常见 Bug

### GC 导致卡顿
- **现象**: 游戏运行时周期性掉帧
- **原因**: GC 回收触发，尤其是频繁的堆内存分配
- **排查**: Profiler → CPU Usage → GC.Alloc
- **解决**: 使用对象池、减少 foreach 中的装箱、使用 struct 替代 class

### Draw Call 过高
- **现象**: 渲染性能差，帧率低
- **排查**: Profiler → Rendering → Draw Calls
- **解决**: 开启 Static/Dynamic Batching、合并 Mesh、使用 GPU Instancing

### 内存泄漏
- **现象**: 游戏运行越久内存占用越高
- **常见原因**: 事件未取消订阅、协程未停止、资源未释放
- **排查**: Memory Profiler、检查 OnDestroy 中的清理逻辑

### 协程陷阱
- **问题**: 协程在 GameObject 被禁用/dispose 后仍可能运行
- **解决**: 在 OnDisable/OnDestroy 中使用 StopCoroutine
- **注意**: 协程不是线程，仍在主线程执行

## 网络问题

### 粘包/半包
- **原因**: TCP 是流式协议
- **解决**: 自定义消息头（长度+类型）+ 缓冲区处理

### 同步延迟
- **帧同步**: 使用锁帧 + 预测 + 回滚
- **状态同步**: 插值 + 外推 + 客户端预测

## 构建问题

### IL2CPP 构建失败
- 检查是否使用了不被支持的 API
- 查看 link.xml 是否正确配置
- 检查第三方库兼容性

### AssetBundle 加载异常
- 检查依赖关系是否正确
- 确认 AssetBundle 未过期/损坏
- 使用 Addressable 系统简化管理

## 排查工具

| 工具 | 用途 |
|------|------|
| Unity Profiler | 性能分析（CPU/GPU/Memory） |
| Frame Debugger | 渲染调试 |
| Memory Profiler | 内存分析 |
| Console Window | 日志和错误 |
| Deep Profiling | 详细性能分析 |
