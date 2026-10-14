---
tags: [project, framework, ui, uisystem]
created: 2026-10-14
updated: 2026-10-14
---

# UI 框架（UISystem）

代码位置：`Assets/Scripts/CSharp/Core/Gear/UISystem/`——`PanelManager.cs` / `PanelBase.cs` / `UILayer.cs`。业务面板的 MVC 写法见 [[06-模板/模板-UI-MVC三件套]] 与 [[02-流程SOP/SOP-新加UI面板]]；现有面板清单见 [[04-模块/UI-面板总览]]。

## UILayer 层级

`UILayer` 枚举（`UILayer.cs`）：

| 值 | 用途 |
|---|---|
| `Bot` | 底层，背景与主场景 UI |
| `Mid` | 中层，常规功能面板 |
| `Top` | 顶层，弹出层与浮层 |
| System | 系统层，系统级提示与全局覆盖 |

## PanelBase（面板基类）

面板 Control 层继承 `PanelBase`。子类惯用初始化三件：`ComponentInit`（缓存组件）/ `EventInit`（注册事件与按钮）/ `Bind`（接数据）。

关键行为与坑：

- **PanelBase 无 onClose 钩子**——反注册事件/释放资源用 `OnDestroy` + base 释放 ResourceScope（HomePanel/CombatReadinessPanel 先例）
- **`OpenPanel` 的 callback 在 `DisplayPanel()` 后同帧触发**——不能当"动画播完"用；等动画用 `AddDisplayFinishedListener` 模式（PreloadPanel 先例）
- **`OpenPanel` 重名去重会吞回调**——面板可能已开，回调不执行。已开时先 `PanelManager.GetPanel<T>()` 探测、直接 `Bind(...)` 补挂，未开才 `OpenPanel`（CharacterListPanel→CultivatePanel 先例）

## PanelManager 常用 API

| API | 说明 |
|---|---|
| `OpenPanel<T>(UIPath.XXX, UILayer.XXX, callback)` | 加载+打开（UIPath 常量：`Bissness/UI/UIPath/UIPath.cs`） |
| `GetPanel<T>()` | 探测已开面板 |
| `PanelDisplay<T>()` | 显示面板 |
| `ClosePanel` | 关闭（退出动画 + CloseDelay 延迟销毁） |

## 生命周期要点

- 面板经 Addressables 加载预制体实例化，**资源作用域随面板关闭整批释放**（scope 一律调用方自备，面板传自己的 scope）
- 池化单元格的面板：关闭时格子回池而不是随面板销毁（见 [[03-框架/对象池-ObjectsPool]]）
- 事件注册/反注册必须成对：`EventInit` 注册 ↔ `OnDestroy` 反注册

## 关联

- [[04-模块/UI-面板总览]] —— 现有面板与共性运作流程
- [[02-流程SOP/SOP-新加UI面板]] —— 新面板七步流程
- [[08-踩坑与排查/池化与UI]] —— Canvas renderMode 解码异常等 UI 坑
