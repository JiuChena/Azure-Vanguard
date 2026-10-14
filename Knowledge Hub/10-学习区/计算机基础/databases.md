---
tags:
  - database
  - persistence
  - unity
created: 2026-06-20
---

# 数据库与持久化

## 项目数据存储方案总览

根据数据类型和需求选择不同的存储方案：

| 存储方式                     | 适用场景           | 优缺点                |
| ------------------------ | -------------- | ------------------ |
| **PlayerPrefs**          | 少量简单配置（音量、最高分） | 最简单，但数据量受限且不安全     |
| **JSON 文件**              | 游戏存档、配置文件      | 可读性好，灵活，适合中等数据量    |
| **XML 文件**               | 结构化配置、与外部系统交互  | 标签冗余、体积大，Unity 不内置 |
| **Binary Serialization** | 大量数据、网络传输      | 体积最小、读写最快          |
| **SQLite**               | 结构化数据、需要复杂查询   | 支持 SQL 查询，适合大型数据   |
| **ScriptableObject**     | 编辑器配置数据        | 编辑器友好，不依赖数据库       |
| **服务器数据库**               | 用户数据、排行榜、敏感数据  | 防作弊、可跨设备同步         |

---

## PlayerPrefs (轻量级键值存储)

Unity 内置的最简单持久化方式，适合少量配置数据。

### 支持的数据类型

| 类型 | 写入 | 读取 |
|------|------|------|
| int | `PlayerPrefs.SetInt("key", 999)` | `PlayerPrefs.GetInt("key", 0)` |
| float | `PlayerPrefs.SetFloat("key", 0.8f)` | `PlayerPrefs.GetFloat("key", 1.0f)` |
| string | `PlayerPrefs.SetString("key", "warrior")` | `PlayerPrefs.GetString("key", "unknown")` |

```csharp
// 保存
PlayerPrefs.SetInt("HighScore", 9999);
PlayerPrefs.SetFloat("MusicVolume", 0.8f);
PlayerPrefs.SetString("PlayerName", "Player1");
PlayerPrefs.Save(); // 立即写入磁盘

// 删除
PlayerPrefs.DeleteKey("HighScore");
PlayerPrefs.DeleteAll();
```

### 局限性

- 只能存储 int / float / string 三种基本类型
- 不适合大量数据或复杂结构
- Windows 存储在注册表，有大小限制
- 数据不安全，玩家可直接修改（**不能存储付费状态等敏感数据**）
- 无查询功能

---

## JSON 文件存储 — 游戏存档

将对象序列化为 JSON 文本存储到文件，是最常用的存档方案。

```csharp
using UnityEngine;
using System.IO;
using System;

[System.Serializable]
public class GameData
{
    public int currentLevel;
    public float playerHealth;
    public string playerName;
    public int[] inventoryItems;
    public DateTime lastSaveTime;
}

public class JSONSaveExample : MonoBehaviour
{
    private string savePath;

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "savegame.json");
        LoadGameData();
    }

    void SaveGameData()
    {
        GameData data = new GameData();
        data.currentLevel = 5;
        data.playerHealth = 85.5f;
        data.playerName = "Hero";
        data.inventoryItems = new int[] { 101, 205, 308 };
        data.lastSaveTime = DateTime.Now;

        string json = JsonUtility.ToJson(data, true); // true = 格式化输出
        File.WriteAllText(savePath, json);
        Debug.Log($"存档已保存至: {savePath}");
    }

    void LoadGameData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            GameData data = JsonUtility.FromJson<GameData>(json);
            Debug.Log($"读取存档 - 关卡: {data.currentLevel}, 生命值: {data.playerHealth}");
        }
    }
}
```

### JSON vs XML

| 特性 | JSON | XML |
|------|------|-----|
| 体积 | 较小 | 较大（标签冗余） |
| 可读性 | 好 | 好（更冗长） |
| 解析速度 | 快 | 较慢 |
| Unity 支持 | `JsonUtility` 内置 | 需要额外库 |
| 推荐场景 | **首选方案** | 与外部 XML 系统对接 |

---

## Binary Serialization / 高效序列化方案

### Protocol Buffers (protobuf-net)

二进制序列化协议，体积显著小于 JSON/XML，解析速度更快。适合网络传输和高频读写场景。

```csharp
[ProtoContract]
public class PlayerSnapshot
{
    [ProtoMember(1)] public uint playerId;
    [ProtoMember(2)] public float[] position;       // (x, y, z)
    [ProtoMember(3)] public byte animationState;    // 压缩为单字节
}

// 序列化
byte[] SerializePlayerData(PlayerSnapshot snapshot)
{
    using var stream = new MemoryStream();
    Serializer.Serialize(stream, snapshot);
    return stream.ToArray();
}
```

### 自定义压缩结构体

```csharp
public struct CompactTransform
{
    public ushort x, y, z;  // 16位压缩位置 (0-65535 映射实际范围)
    public byte rotation;    // 8位压缩旋转 (1/256 精度)

    public byte[] Serialize()
    {
        byte[] data = new byte[7];
        Buffer.BlockCopy(BitConverter.GetBytes(x), 0, data, 0, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(y), 0, data, 2, 2);
        Buffer.BlockCopy(BitConverter.GetBytes(z), 0, data, 4, 2);
        data[6] = rotation;
        return data;
    }
}
```

---

## SQLite — 结构化数据

SQLite 是嵌入式关系数据库，适合需要复杂查询的大量结构化数据。

```csharp
using Mono.Data.Sqlite;
using System.Data;
using UnityEngine;
using System.Collections.Generic;

public class SQLiteExample : MonoBehaviour
{
    private string dbPath;
    private IDbConnection dbConnection;

    [System.Serializable]
    public class PlayerItem
    {
        public int id;
        public string name;
        public int count;
    }

    void Start()
    {
        dbPath = "URI=file:" + Application.persistentDataPath + "/game.db";
        dbConnection = new SqliteConnection(dbPath);

        CreateTable();
        InsertData();
        QueryData();
    }

    void CreateTable()
    {
        dbConnection.Open();
        IDbCommand command = dbConnection.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS PlayerItems (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            count INTEGER DEFAULT 0
        )";
        command.ExecuteNonQuery();
        dbConnection.Close();
    }

    void InsertData()
    {
        dbConnection.Open();
        IDbCommand command = dbConnection.CreateCommand();
        command.CommandText = "INSERT INTO PlayerItems (name, count) VALUES ('Potion', 5)";
        command.ExecuteNonQuery();
        command.CommandText = "INSERT INTO PlayerItems (name, count) VALUES ('Sword', 1)";
        command.ExecuteNonQuery();
        dbConnection.Close();
    }

    void QueryData()
    {
        dbConnection.Open();
        IDbCommand command = dbConnection.CreateCommand();
        command.CommandText = "SELECT * FROM PlayerItems";

        IDataReader reader = command.ExecuteReader();
        List<PlayerItem> items = new List<PlayerItem>();

        while (reader.Read())
        {
            PlayerItem item = new PlayerItem();
            item.id = reader.GetInt32(0);
            item.name = reader.GetString(1);
            item.count = reader.GetInt32(2);
            items.Add(item);
            Debug.Log($"物品: {item.name}, 数量: {item.count}");
        }

        reader.Close();
        dbConnection.Close();
    }
}
```

### 最佳实践

- 使用参数化查询防止 SQL 注入
- 及时关闭数据库连接
- 考虑对敏感数据进行加密

---

## ScriptableObject — 编辑器配置数据

ScriptableObject 是 Unity 原生的数据容器，在编辑器中可视化编辑。

```csharp
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    public string enemyName;
    public int maxHealth;
    public float moveSpeed;
    public float attackDamage;
    public GameObject prefab;
}

// 运行时读取
EnemyConfig config = Resources.Load<EnemyConfig>("EnemyConfig");
```

### ScriptableObject vs 其他方案

| 特性 | ScriptableObject | JSON 文件 | SQLite |
|------|-----------------|-----------|--------|
| 编辑器可视化 | **最好** | 需额外工具 | 需额外工具 |
| 运行时热更新 | 不支持 | 支持 | 支持 |
| 数据量 | 中小型 | 中小型 | 大型 |
| 查询 | 无 | 无 | 强（SQL） |
| 类型安全 | 最好（C# 类型直接引用） | 需手动映射 | 需手动映射 |

---

## 方案选择总结

```
少量简单配置  → PlayerPrefs
游戏存档      → JSON + File.ReadAllText/WriteAllText
大量查询需求  → SQLite
编辑器配置    → ScriptableObject
高性能/网络   → protobuf-net 或自定义二进制
敏感数据      → 服务器存储 + 加密
```

---

## 面试高频题

### Q1: PlayerPrefs 的局限性？
- 只支持 int / float / string 三种类型
- 不适合大量数据
- 数据不安全，可被玩家修改（不可存储付费状态等敏感数据）
- Windows 存储在注册表，有大小限制

### Q2: ScriptableObject 与 JSON 配置如何选择？
- ScriptableObject：适合游戏内只读配置（怪物属性、技能数据），在 Editor 中可视化编辑
- JSON：适合存档数据（玩家进度），需要运行时读写和热更新

### Q3: 如何防止存档被篡改？
- 对存档文件进行校验（MD5 / SHA256 哈希签名）
- 使用 AES 加密存档内容
- 关键数据（如付费、排名）存储在服务器端
- Binary 序列化增加逆向难度

---

## 扩展补充

- AES 加密存档：`Aes.Create()` → `CryptoStream` 加密写入，密钥可存 PlayerPrefs 或用设备ID派生
- Addressables 配置热更新：远程 Catalog + 分组策略（StaticContent/UpdateableContent）+ 版本号比对
- 云端存档：PlayFab 免费层够小项目用，自建用 REST API + JWT 认证
- 异步 IO：`await File.ReadAllTextAsync()` 或 `Task.Run(() => File.ReadAllText(...))`，避免阻塞主线程

> ℹ️ 此模块为扩展参考，不作为面试抽查重点。
