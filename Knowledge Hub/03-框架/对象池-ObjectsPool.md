---
tags: [project, framework, objectpool]
created: 2026-10-14
updated: 2026-10-14
---

# 对象池（ObjectsPool）

代码位置：`Assets/Scripts/CSharp/Core/Gear/ObjectPool/ObjectsPool.cs`。API：`ObjectsPool.Instance.GetObject()` / `ReturnObject()`（部分业务以 `Put` 别名回池——CharacterListPanel 用法）。

## 用途与禁令

- UI 列表/格子等高频创建销毁对象必须池化；**热路径禁 `Instantiate()`+`Destroy()`**
- 列表刷新=旧 Item 全部回池 → 按新数据逐个 `Get` + FillCell

## 池化刷新四铁律（两面板实踩总结）

1. **回池立即移出内容区**——杜绝 Destroy 帧末延迟造成的"幽灵态"（CharacterListPanel 排序切换旧 Item 未清事故）
2. **Get 后强制 `localScale = Vector3.one`**——池空时 `Instantiate` 无父克隆把模板局部尺度当世界尺度，挂回非 1 尺度画布链放大 1/P 倍（实测 108 倍）；GridLayout 不管 scale，必须显式归一
3. **事件监听 `RemoveAllListeners` 后重绑**——池化单元格复用，旧闭包数据必须清
4. **池化单元格必须自带绑定数据字段**（如 `CultivateCharacterItem.unitId`）——异步回包校验 `cell.unitId` 未改绑，防串图/串数据

> 详见 [[08-踩坑与排查/池化与UI]]。

## 关联

- 实战先例：[[04-模块/UI-面板总览]]（CharacterListPanel 池化刷新节）
- [[03-框架/UI框架-UISystem]] —— 面板关闭与格子回池
