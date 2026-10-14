---
tags: [project, debugging, spriteatlas]
created: 2026-09-15
updated: 2026-10-14
---

# SpriteAtlas 三坑

> 项目：Azure Vanguard · Unity 2022.3.55f1c1（Asset Pipeline V2）。三坑：①文件夹 packable 的 fileID、②脚本 `Add()` 不落盘、③图集渗色。图集管线操作见 [[02-流程SOP/SOP-数据表流水线]] 同级工具节；工具入口 `Tools/Sprite Atlas Injector`（Assets/Editor/Art/）。

## 坑一：文件夹 packable 的 fileID

手写 `.spriteatlasv2` YAML（或 API 对 v2 资产 `Add(文件夹)` 后落盘）时，文件夹 packable 若序列化为 `{fileID: 2800000, guid: <文件夹>, type: 3}`，导入器**静默解析失败**：`GetPackables()` 返回 0、`spriteCount=0`、**无任何报错**。`fileID: 2800000` 只对**贴图**（TextureImporter）有效。

**正确写法：文件夹 packable 的 fileID 是 `102900000`**：

```yaml
m_ImporterData:
  packables:
  - {fileID: 102900000, guid: <文件夹guid>, type: 3}
```

### 排查方法（可复用）

用一次性 v1 图集做对照实验，让 Unity 自己写出正确序列化：

1. `new SpriteAtlas()` → `AssetDatabase.CreateAsset(atlas, "tmp.spriteatlas")`
2. `SpriteAtlasExtensions.Add(atlas, folder)` → `SaveAssets()`
3. 读回 YAML 看 Unity 写的 fileID（实测 v1 写出 `102900000`；v2 资产经 API `Add` 落盘却写成 `2800000`，重载后丢失——见坑二）
4. 修好后 `ImportAsset(ForceUpdate)`。注意**打包 1600+ 张图是异步的**：`ImportAsset` 同步返回时 `spriteCount` 仍为 0，需等 import worker 结束后再查

## 坑二：脚本 `Add()` packables 不持久化 → YAML 手术

`UnityEditor.U2D.SpriteAtlasExtensions.Add(atlas, sprites)` 后无论怎么保存，`.spriteatlasv2` 磁盘文件的 packables **纹丝不动**，同步 Reimport 后内存态也被打回磁盘状态。

实测三条通道全部失败（内存 `GetPackables()` 计数是对的，磁盘没写）：

| 通道 | 结果 |
|------|------|
| `Add` + `SetDirty(资产)` + `SaveAssets()` | ✗ 不落盘 |
| `Add` + `SetDirty(importer)` + `SaveAssets()` | ✗ 不落盘 |
| `Add` + `AssetDatabase.ForceReserializeAssets()` | ✗ 不落盘 |

且 `Add()` **不去重**：同一批 sprite 重复 Add，`GetPackables()` 计数线性涨（内存垃圾，不落盘）。

**解法：手写 YAML（可靠）**——直接改 `.spriteatlasv2` 文件的 `m_ImporterData.packables` 列表，然后 `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` 触发重导入重打包。此方案已工具化为编辑器窗口 **`Tools/Sprite Atlas Injector`**（`Assets/Editor/Art/`），CLI 打包一律走它，别再裸调 `Add()`。

### 通道结论（2026-09-22 实证）

| 修改项 | 可靠通道 |
|--------|----------|
| **packables** | `Add()` 全部通道不落盘 → **YAML 手术**（Injector 窗口） |
| **图集设置**（padding / alphaDilation / 压缩） | importer 的 `SerializedObject`（属性在 `.meta` 的 `SpriteAtlasImporter` 块，**不在资产 YAML 里**）改 `m_PackingSettings.padding`、`m_PackingSettings.enableAlphaDilation` 等 → `ApplyModifiedPropertiesWithoutUndo()` → **`si.SaveAndReimport()` 持久化 ✓** |

importer 的 SerializedObject 里看不到 `m_ImporterData`（不可见属性），packables 不走这条路——两类修改通道完全不同，别混。

### packable 的 fileID 写法（2022.3 实证）

| packable 类型 | fileID | 来源 |
|---------------|--------|------|
| 文件夹 | `102900000` | 坑一 |
| 单 sprite 的 PNG（整图） | `2800000` | 贴图主资源 |
| 单 sprite 的 PNG（引用其中 Sprite） | `21300000` | 场景 `m_Sprite: {fileID: 21300000, guid: …}` 实证 |
| 多图集（SpriteSheet）某个子 sprite | 该 sprite 的 `internalID`（可能为负数） | `.png.meta` 的 `spriteSheet.sprites` 列表 |

> 多图集的 fileID **不要**从场景引用反推——prefab 实例里的 Image 引用序列化在 prefab 而不在场景，会漏。直接读 `.meta` 的 `spriteSheet.sprites`（`name: Common_N` 与 `internalID:` 同块对应）。

### 校验

Reimport 后：`GetPackables().Length`（从磁盘读回=真落盘）+ `atlas.spriteCount` + 逐个 `GetSprite(name)` 验证；另取未打包的同族 sprite 名验证返回 null（精准性）。设置类改动校验：重读 `AssetImporter.GetAtPath` 的 SerializedObject + 打开 `.meta` 看值。

## 坑三：图集渗色（重影/边界多出内容）

症状：打包后个别图标边缘出现邻居 sprite 的残影/多出的内容。

成因：紧排相邻 + Bilinear 过滤 + 默认 padding 4 太窄，边缘采样把邻居像素采进来；双重压缩再放大毛边。

修复（SerializedObject + SaveAndReimport 已实证）：`padding: 4 → 8`、`enableAlphaDilation: 1`（不透明边缘向外扩张进 padding，采样到的是自己的扩散像素）。图集默认 textureCompression=0 本身没开压缩；控制台"Source Texture is using compressed format"警告是**源贴图**自身压缩，进图集会有一次重编码损失，追求极致可把源图改未压缩。

仍残留的情况：若整图切帧的源图（如 Common.png）帧与帧紧贴，帧边缘像素烤在 sprite 矩形里——进不进图集都会看到，属源图问题需美术加内边距。

## Injector 窗口（批量注入工具）与现有图集清单

**`Tools/Sprite Atlas Injector`（`Assets/Editor/Art/SpriteAtlasInjectorWindow.cs`，2026-10-05 交付）**——坑二的工程化封装，图集补充一律走它：

- 编辑器窗口指定预制体 + 图集 → 「分析（不写入）」/「分析并注入」
- 递归收集预制体**全部 Sprite 引用**（SerializedObject 全字段扫描，含嵌套预制体与未激活节点）
- guid/fileID 由 `TryGetGUIDAndLocalFileIdentifier` 直接取（免 meta 解析）
- 去重四规则：预制体内重复 / 图集已有精确项 / 整图 `2800000` 超集 / 文件夹覆盖
- packables 列表末尾 YAML 追加（UTF-8 无 BOM）→ `ImportAsset(ForceUpdate)` → 回读校验报告
- 单/多图判定用 `fileID==21300000`（本项目 `TextureImporter.spriteMode` 编译不过）；「多图贴图按整图注入」开关把多图切帧换整图 `2800000`；Assets 外引用（uGUI 包内图标等）自动跳过

**图集清单（2026-10-05 状态）**：

| 图集 | 状态 |
|------|------|
| `Atlas/Avatar.spriteatlasv2` | packables 曾被清空（非脚本操作），待定是否按文件夹重打包；未恢复前 `GetSprite` 返回 null |
| `Atlas/HomePanel.spriteatlasv2` | 3 张整图 + 20 个 sprite 级 packable = 23 sprites，HomePanel 全部 Image 用图精准无冗余；面板新增图片后走 Injector 补 |
| `Atlas/CultivatePanel.spriteatlasv2` | 养成面板用图 |

**运行时取图**：`atlas.GetSprite(资源名)`，取不到（null）时 UI 保留场景默认图兜底——默认存档头像 `Student_Portrait_Airi_Small`、默认昵称 `PlayerName`。

## 关联

- [[08-踩坑与排查/UnityCLI工作流]] — 本组排查多在 UnityCLI 内跑脚本完成
- [[02-流程SOP/SOP-数据表流水线]] — 资产管线全景
