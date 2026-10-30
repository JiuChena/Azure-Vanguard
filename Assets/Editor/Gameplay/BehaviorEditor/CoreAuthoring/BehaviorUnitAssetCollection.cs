using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace BehaviorEditor
{
    /// <summary>收集单位行为资产并按精确名称生成不歧义的配对。</summary>
    internal static class BehaviorUnitAssetCollection
    {
        /// <summary>同名行为资产的配对结果。</summary>
        internal sealed class Pair
        {
            // 配对使用的精确资产名称。
            internal readonly string Name;
            // 同名 Timeline；重复引用去重，但不同资产保留用于检测冲突。
            internal readonly List<TimelineAsset> Timelines = new List<TimelineAsset>();
            // 同名 BehaviorClip；不同资产保留用于检测冲突。
            internal readonly List<BehaviorClip> Clips = new List<BehaviorClip>();

            internal bool HasConflict => Timelines.Count > 1 || Clips.Count > 1;

            /// <summary>创建指定名称的配对组。</summary>
            /// <param name="name">精确资产名。</param>
            internal Pair(string name)
            {
                // 保存名称，后续仅追加同名资产。
                Name = name;
            }
        }

        #region Collection

        /// <summary>收集单位层级和配置引用，以及显式指定目录中的行为资产；不修改任何对象。</summary>
        /// <param name="unit">单位 GameObject、Component 或配置 SO；允许 null。</param>
        /// <param name="folder">单位目录；空值不扫描目录，不接受 Assets 根目录。</param>
        /// <returns>去重后的 Timeline 和 BehaviorClip 引用。</returns>
        internal static List<Object> Collect(Object unit, string folder)
        {
            // 遍历单位组件及其配置 SO，避免循环引用导致递归或重复读取。
            var result = new List<Object>();
            var visited = new HashSet<Object>();
            var pending = new Queue<Object>();
            GameObject root = unit as GameObject;
            if (unit is Component component) root = component.gameObject;
            if (root != null)
            {
                foreach (Component child in root.GetComponentsInChildren<Component>(true))
                    if (child != null && !(child is Transform)) pending.Enqueue(child);
            }
            else if (unit is ScriptableObject) pending.Enqueue(unit);

            while (pending.Count > 0)
            {
                Object current = pending.Dequeue();
                if (current == null || !visited.Add(current)) continue;
                if (current is TimelineAsset || current is BehaviorClip)
                {
                    result.Add(current);
                    continue;
                }

                // 只沿序列化 SO 引用扩展；不跟随其他场景对象、材质或 Timeline 轨道图。
                using (var serialized = new SerializedObject(current))
                {
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object reference = property.objectReferenceValue;
                        if (reference is ScriptableObject) pending.Enqueue(reference);
                    }
                }
            }

            // 目录扫描仅作用于用户指定的单位子目录，避免混入其他单位资产。
            if (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/", StringComparison.Ordinal) &&
                AssetDatabase.IsValidFolder(folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:TimelineAsset t:BehaviorClip", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if ((asset is TimelineAsset || asset is BehaviorClip) && visited.Add(asset)) result.Add(asset);
                    }
                }
            }
            return result;
        }

        #endregion

        #region Pairing

        /// <summary>精确同名配对；重复对象去重，重名不同对象保留为冲突。</summary>
        /// <param name="references">行为资产引用；允许 null、空元素及非行为资产。</param>
        /// <returns>按名称稳定排序的配对组。</returns>
        internal static List<Pair> BuildPairs(IEnumerable<Object> references)
        {
            // 使用序数比较，不猜测后缀、不忽略大小写，避免误覆盖其他行为。
            var groups = new SortedDictionary<string, Pair>(StringComparer.Ordinal);
            var visited = new HashSet<Object>();
            if (references == null) return new List<Pair>();
            foreach (Object reference in references)
            {
                if (reference == null || !(reference is TimelineAsset || reference is BehaviorClip) ||
                    !visited.Add(reference)) continue;
                if (!groups.TryGetValue(reference.name, out Pair pair))
                {
                    pair = new Pair(reference.name);
                    groups.Add(reference.name, pair);
                }
                if (reference is TimelineAsset timeline) pair.Timelines.Add(timeline);
                else pair.Clips.Add((BehaviorClip)reference);
            }
            return new List<Pair>(groups.Values);
        }

        #endregion
    }
}
