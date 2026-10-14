---
tags: [project, debugging, unitycli, compilation]
created: 2026-10-14
updated: 2026-10-14
---

# UnityCLI 工作流踩坑

> 调用命令与编译三重验证的"正确做法"见 [[01-项目总览/工具与环境]]；接线/验证脚本模板见 [[06-模板/模板-AgentScripts]]。本篇是全量坑集（多轮事故结晶，按主题分节）。

## 编译不会自动触发

改动 Assets 下脚本后，编辑器**不会自动重编译**（CLI 下 auto-refresh 不生效）。`AssetDatabase.Refresh()` 在 run_script 执行期间只排队不编译——编译要等脚本返回后的编辑器循环，而 CLI 下循环迟迟不触发。

**做法**：改完脚本先跑 `AgentScripts/Recompile.cs`（Refresh + RequestScriptCompilation）。构建管线（BuildPlayerContent）会顺带强制同步重编译，可省。

## 编译成败判定（CompileCheck 反射已失效）

`EditorUtility.scriptCompilationFailed` 可能是**上一轮残留**；CompileCheck 的 LogEntries 反射在 2022.3 模块化程序集下已失效（logEntries=-1 读不到）。可靠判定：

1. **console groundTruth**：`unity.exe command console` 的 `compilationFailed` / `consoleErrors`（console 错误条目是历史残留，按 timestampUtc 对照写文件时间辨新旧）
2. **dll mtime**：`Library/ScriptAssemblies/Assembly-CSharp(-Editor).dll` 是否刷新——**刷新即成功**（失败不会重写 dll）。⚠️ **基线必须在写文件之前取**，否则"无变化"会被误读成"没编译"（曾空转数轮）
3. **dll 二进制 grep**：新类型名 ASCII 可搜（#Strings 堆 UTF-8）；**字符串字面量在 #US 堆是 UTF-16LE，ASCII grep 必然假 0 命中**——必须按 `utf-16-le` 编码搜，否则成功编译被读成没编译（2026-10-09 误判一轮）
4. 兜底：`%LOCALAPPDATA%/Unity/Editor/Editor.log` 里新增 `error CS` 行

## run_script 语法与误调本体

- 实体：**`C:/Users/jiiuc/AppData/Local/Unity/bin/unity.exe`**（不在 PATH）
- ⚠️ `D:\Unity3D\2022.3.55f1c1\Editor\Unity.exe` 是**编辑器本体**——误调它跑 `command run_script` 会**新开一个编辑器实例**并弹 "Unity is running as administrator" 模态永久挂起。补救：`Get-Process Unity` 看窗口标题（模态实例标题=该文案）Stop-Process 杀掉，**绝不能杀用户开着的编辑器**（标题=项目名 - 场景名）
- `editor_status` 是 `command` 下的子命令；看连接状态用 `unity.exe status`（Port/State/Project/PID，**端口会漂移**——曾 7801 后变 7800，别记死端口）
- `void` 入口返回 `result:null`——回读输出走 `command console`（输出大时持久化成文件，grep `[脚本名]` 提取）
- run_script 有 **30s 超时**：幂等脚本重跑即可；长任务见下节

## 主线程死锁（代价=重启编辑器）

run_script 在**主线程**执行。脚本里对真异步（冷缓存的 Addressables 表源 LoadTableAsync）做 `.GetAwaiter().GetResult()` 会把主线程**永久**锁死——30s 看门狗只打印 `Main thread operation timed out` 弃掉请求，**不掐线程**；后续一切 exec 排队超时、最终 502，**只有重启编辑器能救**。

识别特征：脚本自己的 console 输出停在半路、后续 run_script 零输出超时。

**修复**：验证脚本开头 `DataTableManager.Instance.SetSource(new LocalFileTableSource())`（直读 .bytes、已完成态 Task，整条 await 链同步完成），GetResult 即安全——这就是 VerifyCharacterProgression 能全绿而新脚本死锁的差别。

## 编辑态 await 链路死路

run_script 执行期间编辑器泵不驱动 await 续延：`Task.Wait(15s)` 永远 `WaitingForActivation`（**操作其实已完成**、续延挂起）。验证要用 `Addressables.LoadAssetAsync(...).WaitForCompletion()` 直载直证；await 链路留给 Play 模式。测试后 `ForceReleaseAllResourcesForShutdown()` 复位清孤儿租约。

配套：编辑器重启后 Addressables 冷缓存——任何表加载验证必须先注入同步源（[[08-踩坑与排查/Addressables与资源]]）。

## 长任务过看门狗（内容构建 >30s）

看门狗弃请求但**线程照常跑完**（实测 289.4s error=none）。发起后**不重发**，挂后台轮询 console 等 `[脚本名] done` 日志，完成标志以 console 为准。

## 误判教训存档

- **"旧 Item 没入池"真因**：Item.prefab 被重建时丢失 `CHListItem` 组件——"幽灵态"根因不在池，在预制体组件缺失（见 [[08-踩坑与排查/池化与UI]]）
- **漏 using 三连翻车**：UI / U2D / Addressables 父命名空间三次同型事故——AgentScripts 无 IDE 补全，**写前按命名空间全量过清单**（清单见 [[06-模板/模板-AgentScripts]]）

## 关联

- [[01-项目总览/工具与环境]] — 命令与验证流程
- [[08-踩坑与排查/Addressables与资源]] / [[08-踩坑与排查/序列化与存档]] — 配套领域坑
