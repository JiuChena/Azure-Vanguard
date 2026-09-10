# Framework Editor Layer

这一层承载可复用框架对应的编辑器扩展。

## 当前目录职责

- `SkillCore/`
  - 行为编辑器核心作者工具
  - 包括 Timeline 轨道 Inspector、导出器、行为作者窗口等

- `RPGGameplay/`
  - 通用战斗承载层的编辑器扩展
  - 当前主要是 `StatusDataEditor`

- `Combat/`
  - 通用战斗全局配置的编辑器扩展
  - 当前主要是 `BattleGlobalSettingsEditorUtility`

## 边界规则

- 这里应只放可随 `Framework` 一起迁移的编辑器代码
- 不应依赖 `Business/MiniatureGarden`
- 新项目复用框架时，这一层应与 `Assets/Scripts/C#/Framework` 一起迁移
