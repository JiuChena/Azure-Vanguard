---
tags: [project, sop, item, config-table]
created: 2026-10-14
updated: 2026-10-14
---

# SOP-新加物品与配表

> 向 ItemTable 加一种（或一批）新物品并让背包/货币/UI 体系认识它的全流程。
> 物品模型背景：[[04-模块/物品背包货币]]；表系统机制：[[03-框架/配置表-DataTable]]；纯管线操作（不改物品种类）用 [[02-流程SOP/SOP-数据表流水线]]。
> ⚠️ **id 纪律：设计 id 前先问类型**——id 是否进代码常量、是否被档案引用。物品 id 是档案外键（旧档背包条目引用它），**string id 一经发布不可随手改**，改 = 迁移旧档。icon 不变式：icon 地址 = `Item_Icon_<id>`。

## 流程总览

| # | 步骤 | 分工 | 产出 |
|---|---|---|---|
| 1 | 设计拍板 | 📐 | 物品定义单（id/名称/type/tags/数值） |
| 2 | Excel 加行 | 🤖 | ItemTable.xlsx 新行 |
| 3 | 行类核对 | 🤖 | （通常零改动） |
| 4 | 导出 + 入组 | 🤖 | .bytes/.json + ConfigTables 组 |
| 5 | 消费端接线 | 🤖 | 按物品用途（见步骤 5 分支） |
| 6 | 验证 | 🤖 | VerifyItemTable / Play 截图 |

## 步骤 1：设计拍板（📐）

向用户确认物品定义单，重点问清（**不臆造数值**）：

| 项 | 说明 | 备注 |
|---|---|---|
| **id** | string。**问清：是否进代码常量？是否会被旧档引用？** | 惯例：id 值 = 图标地址去 `Item_Icon_` 前缀 |
| name | 显示名（可中文，**显示名走表**，代码不写中文） | |
| type | int，物品是什么类（货币/材料/消耗品…） | 货币 id 对齐 `CurrencyIds`（Gold=1/StarlightStone=2） |
| tags | 逗号分隔：usable/sell/batch…空=纯展示 | 决定行为分发（ItemFeatureCatalog） |
| 数值列 | maxStack/price/sellPrice… | 📐 用户给 |
| icon | 指向 `Item_Icon_<id>`（🖐 用户出图登记） | 🤖 出需求表 |

## 步骤 2：Excel 加行（🤖）

- 源表：`Assets/Module/Items/Excel/ItemTable.xlsx`（**唯一真源**，不直接改 json/bytes）
- 单行手改 xlsx；批量插行：`dotnet run --project Tools/TableRowInserter -- <xlsx路径> <rows.json>`（按表头字段名/类型行写单元格，主键幂等跳过）
- Excel 约定：第 1 行字段名（`#` 开头=注释列）、第 2 行类型、第 3 行起数据；**第 1 列即主键**

## 步骤 3：行类核对（🤖）

- 行类：`Core/Gameplay/Item/ItemRow.cs`（`Framework.Gameplay.Item`）
- **map 格式加列不破坏契约**：Excel 加列而行类没加字段 → 反序列化只忽略该 key，无需改代码
- 只有**新列需要运行时读**时才给 ItemRow 加属性：新属性必须标 `[Key("列名")]`（map 格式）或 `[IgnoreMember]`（计算属性）；**[MessagePackObject] 类型所有 public 成员必须标 [Key] 或 [IgnoreMember]**，漏标反序列化抛异常
- 行类写法模板：[[06-模板/模板-配置表行类]]

## 步骤 4：导出 + 入组（🤖）

1. `dotnet run --project Tools/ExcelTableExporter`（无参全量覆盖，出 `Assets/Module/Items/ItemTable.bytes + .json`）
2. 入组确认：ConfigTables 组地址 `ConfigTables/ItemTable` 已登记过则无需再动（bytes 内容更新随构建走）；**新表**才需要 `AgentScripts/SetupConfigTablesAddressables.cs` 追加（幂等）
3. 校验：工具自带（主键唯一/必填/类型），错误定位到单元格坐标，有错不写文件

## 步骤 5：消费端接线（🤖，按用途分支）

| 物品用途 | 接线 |
|---|---|
| 货币 | `CurrencyIds` 常量加项 → GearBar/HomePanel 余额刷新自动生效（走 GetCount） |
| 经验书/养成材料 | 对齐 [[04-模块/角色养成系统]] 消耗列（`itemId:count` 解析），替换占位行 1002/1003 |
| 可使用（usable） | `IItemFeature` 实现类 + ItemFeatureCatalog 注册（等 UseItemRequest 通道，当前 TODO log） |
| 可出售（sell） | 同上，SellItemRequest 通道 |
| 纯展示 | 无接线，查表即得 |

## 步骤 6：验证（🤖）

1. **回环验证**：`AgentScripts/VerifyItemTable.cs`（读 .bytes → 反序列化 → 建表 → TryGet 命中/未命中），新行补断言
2. **发放测试**：TestItemGranter（调试发放）→ GearBar/HomePanel 余额或背包显示 → Play 截图存 `production/qa/evidence/`
3. **日志**：[[_规范/日志规范]]

## 常见坑

| 坑 | 出处 |
|---|---|
| id 拍脑袋用 int → 全链返工 | 物品 id 是 string，**设计前先问类型** |
| 改 id 不迁移旧档 → 背包条目孤儿 | [[08-踩坑与排查/序列化与存档]] |
| ItemRow 新属性漏 [Key]/[IgnoreMember] → 反序列化异常 | [[08-踩坑与排查/序列化与存档]] |
| 信封用 Dictionary<string,object> → 两端对不上 | 同上 |
| 验证脚本期望值抄实现 → 同义反复抓不住错 | 期望硬编码独立推（VerifyTrinket 教训） |

## 版本记录

- **v1（2026-10-14）**：首版，自 ItemTable 首表与货币接线过程提炼。
