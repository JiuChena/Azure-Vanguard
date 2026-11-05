using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools 菜单：选中资源或场景物体后 Debug 出完整路径（2026-11-01）。
/// - Project 面板选中资源（prefab/material/texture/script/SO/配置表 .bytes 等）→ Assets/... 资产路径
/// - Hierarchy 场景物体（或组件）→ 层级路径（Root/Child/...）
/// 输出到 Console（可复制）。
/// </summary>
public static class SelectionPathDebugger
{
    [MenuItem("Tools/Debug Selection Path")]
    private static void LogSelectionPath()
    {
        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            Debug.Log("[SelectionPath] 未选中任何对象");
            return;
        }

        foreach (Object obj in selected)
        {
            if (obj == null) continue;
            Debug.Log($"[SelectionPath] {obj.name}: {ResolvePath(obj)}", obj);
        }
    }

    // 资源 → 资产路径；场景物体/组件 → 层级路径
    private static string ResolvePath(Object obj)
    {
        string assetPath = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(assetPath)) return assetPath;

        if (obj is GameObject go) return HierarchyPath(go.transform);
        if (obj is Component comp) return HierarchyPath(comp.transform);
        return "(无路径：非资产也非场景对象)";
    }

    // Transform 层级路径（根/子/孙）
    private static string HierarchyPath(Transform t)
    {
        if (t == null) return "(null)";
        string path = t.name;
        Transform parent = t.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
