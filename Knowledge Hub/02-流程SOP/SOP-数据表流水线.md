---
tags: [project, sop, data-pipeline, excel]
created: 2026-10-14
updated: 2026-10-14
---

# SOP-数据表流水线

> 配置表"从改数到运行时可用"的标准管线。**新加一种物品/一张表**走 [[02-流程SOP/SOP-新加物品与配表]]；本篇是纯管线操作手册（改数值、改平衡、加表骨架）。
> 机制详解：[[03-框架/配置表-DataTable]]。

## 管线全景

```
Assets/Module/<模块>/Excel/*.xlsx   ← 唯一真源（git 管理，手改这里）
  │ dotnet run --project Tools/ExcelTableExporter        （无参=全量导出）
  ├─→ Assets/Module/<模块>/ConfigTables/<表名>.bytes      运行时格式
  └─→ Assets/Module/<模块>/ConfigTables/<表名>.json       调试副本
        ↓ Addressables 组 ConfigTables（地址 ConfigTables/<表名>，标签 DataTable）
运行时 DataTableManager.LoadTableAsync<TRow,TKey>(表名)
```

## 常用操作

### A. 改现有表的数值（最常见）

1. 手改 xlsx 对应单元格（**不直接改 json/bytes**——它们是产物，下次导出被覆盖）
2. `dotnet run --project Tools/ExcelTableExporter`（无参全量覆盖所有目录对）
3. 重打包/重构建后运行时生效；编辑态验证脚本走 LocalFileTableSource 直读 .bytes

### B. 批量插行

```bash
dotnet run --project Tools/TableRowInserter -- <xlsx路径> <rows.json>
```
rows.json 按表头字段名/类型行写单元格；**主键幂等跳过**（已存在的主键不动）。

### C. 新建一张表

1. Excel 建页签：第 1 行字段名（`#` 开头=注释列）、第 2 行类型（`int/long/float/bool/string`，可空格追加中文注释）、第 3 行起数据；**第 1 列即主键**（允许 skillId 等语义化命名）
2. 写行类：[[06-模板/模板-配置表行类]]（`IConfigTableRow<TKey>`，主键属性标 `[IgnoreMember]`）
3. 导出（同 A.2）
4. **入组**：`AgentScripts/SetupConfigTablesAddressables.cs` 追加表名（幂等；复制已有条目 schema 建 AddressableAssetEntry，地址 `ConfigTables/<表名>`）
5. 验证：新建 Verify 脚本（先例 `VerifyCharacterTables.cs`：反序列化 → 引用完整性 → 数值断言）

### D. 放表的新位置约定

新模块的表放 `Assets/Module/<模块>/Excel/`（源）→ 同级 `ConfigTables/`（产物）；Addressables 逻辑地址统一 `ConfigTables/<表名>` 不变。**所有数据文本/表放 Module 下**，工具链 DefaultDirPairs 同步追加新目录对。

## 导出工具细则（Tools/ExcelTableExporter）

- ClosedXML 读 xlsx；直接引用 `Assets/Plugins/MessagePack/Runtime/MessagePack.dll`（两端零版本漂移）
- 校验：主键唯一/必填/类型，**错误定位到单元格坐标**；有错不写文件（产物不会半新半旧）
- 序列化：MessagePack map 格式按列名匹配（工具端不需要知道 C# 行类）；信封 `{version, tableName, rows}`
- **加列不破坏契约**：Excel 加列而行类没加字段 → 运行时反序列化忽略该 key

## 运行时消费

| 场景 | 用法 |
|---|---|
| 业务加载 | `await DataTableManager.Instance.LoadTableAsync<TRow,TKey>(表名)`（按名缓存，类型不符抛异常） |
| UnitConfig 聚合 | SO 引用表 .bytes，入队一个入口全部到位（[[04-模块/角色养成系统]]） |
| 编辑态/CLI 验证 | LocalFileTableSource 同步读 .bytes（Addressables 编辑态死路，[[08-踩坑与排查/Addressables与资源]]） |
| 换来源 | 实现 `ITableSource` 注入 `SetSource()`（热更/服务端推表预留） |

## 常见坑

| 坑 | 出处 |
|---|---|
| 直接改 bytes/json → 下次导出被覆盖 | 本篇 §A |
| 信封用 Dictionary<string,object> → 反射字段序列化两端对不上 | [[08-踩坑与排查/序列化与存档]] |
| 行类 public 成员漏 [Key]/[IgnoreMember] → 抛异常 | 同上 |
| 主键重复 → LogError 保留先出现行（静默错数据） | [[03-框架/配置表-DataTable]] |
| 编辑态用 Addressables 加载表 → 死路 | [[08-踩坑与排查/Addressables与资源]] |

## 版本记录

- **v1（2026-10-14）**：首版。
