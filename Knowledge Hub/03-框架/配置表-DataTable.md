---
tags: [project, framework, configtable, datatable]
created: 2026-10-14
updated: 2026-10-14
---

# 配置表系统（DataTable）

代码位置：`Assets/Scripts/CSharp/Core/Gear/ConfigTable/`（DataTableManager 等）。操作流程见 [[02-流程SOP/SOP-数据表流水线]] 与 [[02-流程SOP/SOP-新加物品与配表]]。

## 数据两分法

- **网络传输 / 实时变动**（商店表、物品表等）→ Excel 编辑 → 工具转 MessagePack 二进制 → 运行时反序列化
- **固定单位配置**（UnitConfig：id/图标/BEclip 等）→ SO，不变；SO 资产照常走 Addressables 出 bundle，不与热更冲突
- 玩家运行时数据（背包 ID-数量、配队）**不经 Excel**，运行时直接 MessagePack，与配置表共用一套序列化栈

## Excel → 二进制管线

```
<模块>/Excel/*.xlsx（策划源头，git 管理，不进 Assets）
  │ Tools/ExcelTableExporter（dotnet 9 独立控制台工具）
  ├─→ <模块>/ConfigTables/<表名>.bytes   运行时格式（MessagePack map 格式）
  └─→ <模块>/ConfigTables/<表名>.json    调试副本（工具直接生成带字段名）
        ↓ Addressables 组 ConfigTables，地址 ConfigTables/<表名>，标签 DataTable
运行时 DataTableManager → ITableSource → 反序列化 → DataTable<TRow,TKey>
```

- **工具**：`Tools/ExcelTableExporter/`（ClosedXML 读 xlsx；直接引用 `Assets/Plugins/MessagePack/Runtime/MessagePack.dll`，两端零版本漂移）。无参全量导出（DefaultDirPairs 多目录对）；`--init 表名 字段:类型...` 生成模板；校验（主键唯一/必填/类型）错误定位到单元格坐标，有错不写文件
- **Excel 约定**：第 1 行字段名（`#` 开头=注释列不导出）、第 2 行类型 `int/long/float/bool/string`（可空格追加中文注释）、第 3 行起数据；每个页签一张表，**第 1 列即主键**（允许 skillId/effectId 等语义化命名，不要求字面 id）
- **序列化格式**：map 格式按列名匹配（工具端不需要知道 C# 行类，免共享程序集）；信封 `{version, tableName, rows}`，`version` 是迁移抓手；**加列不破坏契约**——Excel 加列后运行时行类没加字段时反序列化只忽略该 key
- **表地址新规**：模块表放 `Assets/Module/<模块>/Excel/`（源）→ 同级 `ConfigTables/`（产物）；Addressables 逻辑地址统一 `ConfigTables/<表名>`

## 运行时（Core.Gear/ConfigTable/）

| 文件 | 职责 |
|------|------|
| `ConfigTableFile<TRow>` | 信封 [MessagePackObject]，map 格式 [Key("version"/"tableName"/"rows")] |
| `IConfigTableRow<TKey>` | 行约定：`TKey PrimaryKey { get; }` |
| `DataTable<TRow,TKey>` | 只读行集 + 主键索引；重复主键 LogError 保留先出现的行 |
| `ITableSource` | 来源抽象（后续热更/服务端推表实现此接口即可替换） |
| `AddressableTableSource` | 默认来源：`AcquirePersistentAssetAsync<TextAsset>("ConfigTables/<表名>")`，常驻不释放 |
| `DataTableManager` | 懒加载单例；`LoadTableAsync<TRow,TKey>(表名)` 按名缓存，类型不符抛异常；`SetSource()` 注入来源 |

用法：`await DataTableManager.Instance.LoadTableAsync<ItemRow, int>("ItemTable")`。

**编辑态死路**：Addressables 在 EditMode/CLI 直载不可用——验证脚本用 `LocalFileTableSource`（同步读 .bytes）替代（VerifyTrinket/VerifyCharacterProgression 先例），见 [[08-踩坑与排查/Addressables与资源]]。

## 现有表（2026-10-14 盘点）

| 模块目录 | 表 |
|---|---|
| `Assets/Module/Items/` | ItemTable |
| `Assets/Module/Unit/Character/` | CharacterTable、CharacterLevelTable（含升级经验/消耗）、CharacterSkillTable、SkillEffectTable、CharacterSkillLevelTable、UnlockNodeTable、BreakthroughTable |
| `Assets/Module/Unit/Trinket/` | TrinketTable、EnsembleTable |
| `Assets/Module/Unit/Weapon/` | WeaponTable、WeaponLevelTable |

行类落在 `Core/Gameplay/Unit/` 与 `Core/Gameplay/Item/`，写法见 [[06-模板/模板-配置表行类]]。

## 关联

- [[02-流程SOP/SOP-数据表流水线]] —— 全链操作步骤
- [[04-模块/角色养成系统]] —— 角色域表设计（乘区/技能两层/id 编码）
