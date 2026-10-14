---
tags: [project, framework, persistence, messagepack]
created: 2026-10-14
updated: 2026-10-14
---

# 数据持久化（BinaryDataManager）

代码位置：`Assets/Scripts/CSharp/Core/Gear/BinaryDataManager/`——`BinaryDataManager.cs` + `MessagePackRuntime.cs`。MessagePack 二进制存档，写入 `Application.persistentDataPath/Data/<子目录>/<文件名>.bin`。

## 数据层约定（MessagePack）

- 存档根对象标注 `[Serializable]` + `[MessagePackObject]`，字段用 `[Key(n)]` 显式编号
- **[Key] 契约只追加不重排**；首个字段固定为 `version`，是未来数据迁移的唯一抓手（schema version，迁移链 switch-case 逐版升级，与热更包版本无关）
- 反序列化失败或文件不存在时返回 `default`，各管理器用 `?? new T()` 兜底为新档
- 动态格式化器只接受 public 类型；[MessagePackObject] 类型**所有 public 成员必须标 [Key] 或 [IgnoreMember]**

## API

| API | 说明 |
|---|---|
| `Save<T>(data)` / `Load<T>()` | 按类型落盘/读档 |
| `FolderExists(path)` / `WipeFolder(path)` | 新账号判定 / 调试清档（不可恢复） |

## 各域统一纪律（玩家数据中心钉死）

- **条目形状**：[Key] 契约只追加不重排
- **版本策略**：首字段 version + 迁移链
- **入口**：Manager 单例唯一存档者，**变更即保存**；`LoadData` 内 Normalize 防脏数据（同 id 留首个、字段下限防御、去重）
- 写操作一律走管理器，直改数据对象不落盘

序列化工具链踩坑（信封/动态格式化器/排查手段）见 [[08-踩坑与排查/序列化与存档]]；新存档域落地步骤见 [[04-模块/玩家数据中心]] 的"新域落地模板"节。
