---
tags: [project, template, save, messagepack]
created: 2026-10-14
updated: 2026-10-14
---

# 模板-存档POCO

> 新存档域的数据对象（POCO）+ 管理器骨架。持久化机制见 [[03-框架/数据持久化-BinaryDataManager]]；域全景与"新域落地模板"见 [[04-模块/玩家数据中心]]。先例：`Core/Gameplay/Player/CharacterWarehouseData.cs`。

## 数据对象骨架

```csharp
using System;
using MessagePack;

namespace Framework.Gameplay.MyDomain
{
    /// <summary>XXX 域存档根。[Key] 契约只追加不重排。</summary>
    [Serializable]
    [MessagePackObject]
    public class MyDomainData
    {
        /// <summary>首字段固定 version——schema 版本，迁移抓手（迁移链 switch-case 逐版升级）。</summary>
        [Key(0)]
        public int version;

        [Key(1)]
        public List<MyEntry> entries = new List<MyEntry>();
    }

    [Serializable]
    [MessagePackObject]
    public class MyEntry
    {
        // ⚠️ id 设计前先问类型：id 会被旧档引用（档案外键），string id 一经发布不可随手改
        [Key(0)]
        public string id;

        [Key(1)]
        public int count;
    }
}
```

## 硬规则

1. **[Key(n)] 显式编号，只追加不重排**——重排 = 旧档全部错位；废弃字段保留编号不删（可加 `[IgnoreMember]` 停用语义，或迁移链清理）
2. **首字段 `version` 固定 Key(0)**；迁移 = `LoadData` 内按 version 逐版 switch-case 升级
3. **id 字段是档案外键**：改 id 必须写迁移（旧档条目会成孤儿——背包 icon 不变式同理）
4. 类型变更要考虑旧档：string unitId 迁移靠"[Key] 序号不变 + 旧档反序列化失败自动降级空档"的破坏性重置（有意识决策，不是默认安全）
5. 序列化由 BinaryDataManager 统一负责（`Application.persistentDataPath/Data/<子目录>/<文件名>.bin`），**禁 PlayerPrefs**

## 管理器骨架

```csharp
/// <summary>XXX 域管理器：唯一存档者，变更即保存。</summary>
public class MyDomainManager
{
    private MyDomainData data;

    public static MyDomainManager Instance { get; } = new MyDomainManager();
    private MyDomainManager() { LoadData(); }

    // 读：快照/查询 API（写操作一律走管理器，直改不落盘）
    public IReadOnlyList<MyEntry> Snapshot() => data.entries;

    // 写：校验 → 改 → 存 → 广播
    public bool AddEntry(string id, int count)
    {
        if (count <= 0) return false;                       // 下限防御
        var entry = data.entries.Find(e => e.id == id);
        if (entry != null) { entry.count += count; }        // 幂等/合并语义按域定
        else data.entries.Add(new MyEntry { id = id, count = count });
        SaveData();
        EventCenter.Instance.SetEventTrigger(EventNames.MyDomainUpdated);
        return true;
    }

    public void SaveData() => BinaryDataManager.Instance.Save(data);

    public void LoadData()
    {
        data = BinaryDataManager.Instance.Load<MyDomainData>() ?? new MyDomainData();
        Normalize();   // 防脏数据：同 id 留首个 / 字段下限防御 / 去重
    }

    private void Normalize()
    {
        data.entries = data.entries
            .GroupBy(e => e.id).Select(g => g.First())
            .Where(e => e.count > 0).ToList();
        if (data.version < CurrentVersion) { /* 迁移链 switch-case */ data.version = CurrentVersion; }
    }
}
```

## 收尾三步（新域落地模板）

1. EventNames 加事件（对应区段末尾）
2. `PlayerDataCenter` 的 SaveAll/WipeAll 批量方法挂上本域
3. Verify 脚本：落盘-重载-防御路径-幂等（先例 `VerifyPlayerDataCenter.cs`；备份还原不污染真实档）

## 检查单

- [ ] [Key] 只追加不重排；version=Key(0)
- [ ] id 类型设计前拍板（string/int），档案外键意识
- [ ] Manager 单例唯一存档者、变更即保存、LoadData 内 Normalize
- [ ] 所有 public 成员有 [Key] 或 [IgnoreMember]
- [ ] 事件广播 + PlayerDataCenter 批量方法挂接
- [ ] Verify：备份还原不污染真实档；期望值硬编码独立推
