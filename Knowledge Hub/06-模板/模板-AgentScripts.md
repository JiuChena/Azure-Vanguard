---
tags: [project, template, agentscripts, editor-scripting]
created: 2026-10-14
updated: 2026-10-14
---

# 模板-AgentScripts

> 项目根 `AgentScripts/` 下的 UnityCLI 驱动脚本惯例。跑法与编译三重验证见 [[01-项目总览/工具与环境]]；踩坑见 [[08-踩坑与排查/UnityCLI工作流]]。

## 两类脚本

| 类型 | 命名 | 职责 | 留盘？ |
|---|---|---|---|
| Setup/Wire/Repair | `Setup<对象>.cs` / `Wire<对象>.cs` | 幂等接线：挂组件、绑引用、登记 Addressables | 留盘（可重跑） |
| Verify | `Verify<对象>.cs` | 断言验证：回读/回环/数值 | 留盘 |
| Probe/一次性 | `Probe<对象>.cs` / 临时修复 | 探查 dump / 一次性迁移 | **跑完即删** |

## 入口约定

```csharp
// 无命名空间（AgentScripts 独立于编译域）
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>一次性落地脚本：XXX（幂等，可重跑）。</summary>
public static class SetupXxx
{
    public static string Run()
    {
        StringBuilder log = new StringBuilder();

        // ... 操作 ...

        return log.Append("SetupXxx OK").ToString();
    }
}
```

跑法：
```bash
"C:/Users/jiiuc/AppData/Local/Unity/bin/unity.exe" command run_script \
  --project-path "D:/Unity3D/Unity Programs/Azure Vanguard" \
  --file AgentScripts/SetupXxx.cs --entry SetupXxx.Run --timeout 90
```

## 幂等接线脚本骨架（SerializedObject 模式）

```csharp
// 预制体接线：LoadPrefabContents + SerializedObject + 盘上回读
string path = "Assets/Prefabs/UI/My/MyPanel.prefab";
GameObject root = PrefabUtility.LoadPrefabContents(path);
try
{
    // 1. 挂组件（已挂则复用——幂等）
    var control = root.GetComponent<MyPanel>() ?? root.AddComponent<MyPanel>();

    // 2. SerializedObject 写私有字段（Inspector 引用不手拖，禁按名 Find）
    var so = new SerializedObject(control);
    so.FindProperty("status").objectReferenceValue =
        root.transform.Find("Top/Right").GetComponent<MyPanel_Status>();
    so.FindProperty("avatarAtlas").objectReferenceValue = atlas;
    so.ApplyModifiedPropertiesWithoutUndo();

    // 3. 数组扩容陷阱：arraySize 扩容会把最后一个旧元素复制进新槽——
    //    扩完必须逐槽显式赋值，不能假设新槽为默认值
    // var pages = so.FindProperty("pages");
    // pages.arraySize = 4;
    // pages.GetArrayElementAtIndex(3).objectReferenceValue = xxx;  // 显式写！

    PrefabUtility.SaveAsPrefabAsset(root, path);
}
finally { PrefabUtility.UnloadPrefabContents(root); }

// 4. 盘上回读逐项断言（"接线成功"以盘上回读为准，不是以写入成功为准）
GameObject verify = AssetDatabase.LoadAssetAtPath<GameObject>(path);
var vso = new SerializedObject(verify.GetComponent<MyPanel>());
bool ok = vso.FindProperty("status").objectReferenceValue != null
       && vso.FindProperty("avatarAtlas").objectReferenceValue != null;
// 每项打印，False 即 FAIL
```

## SO 资产创建骨架

```csharp
string assetPath = "Assets/Settings/Ability Configurations/Xxx.asset";
var asset = AssetDatabase.LoadAssetAtPath<XxxSO>(assetPath);
if (asset == null)
{
    asset = ScriptableObject.CreateInstance<XxxSO>();
    AssetDatabase.CreateAsset(asset, assetPath);
}
var so = new SerializedObject(asset);
so.FindProperty("_entityCategory").enumValueIndex = 0;
so.FindProperty("_registrationId").enumValueIndex = 1;
so.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
```

## Verify 脚本纪律（验证口径）

- **期望值硬编码独立推，不抄实现**——两错一致的对账是同义反复（VerifyTrinket 端到端教训：真实档非处女态，期望要对档上已有状态做独立重导补偿；bare 态先校准）
- 存档类验证：**备份/还原真实档**，不污染（先例 VerifyCharacterProgression / VerifyTrinket 三档备份）
- Addressables 编辑态死路：表加载用 LocalFileTableSource 直读 .bytes（[[08-踩坑与排查/Addressables与资源]]）
- 新账号类探针**延迟发放**（OnEnable 提前落盘会污染 IsNewAccount 判据）
- 结果可判定：返回字符串带明确 PASS/FAIL 与计数（先例 `24 通过 / 0 失败`）

## using 自检清单（写前过一遍）

- [ ] UnityEngine / UnityEditor（Editor 类）
- [ ] UnityEngine.UI / TMPro / UnityEngine.U2D（UI 件）
- [ ] UnityEditor.AddressableAssets（AddressableAssetSettingsDefaultObject 在父命名空间，非 .Settings）
- [ ] Core 层类型：先 grep `^namespace`——InventoryManager 等 Framework.Gameplay.*；BetterHSM 族 Core.Gear；GAS SO 类 Framework.Gameplay.Abilities.Configuration
- [ ] System.Text（StringBuilder）
