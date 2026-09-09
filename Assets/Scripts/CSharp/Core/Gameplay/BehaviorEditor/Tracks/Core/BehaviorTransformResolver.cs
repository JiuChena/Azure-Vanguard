using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 解析并缓存行为宿主下的骨骼层级路径。
    /// </summary>
    public sealed class BehaviorTransformResolver
    {
        // 骨骼路径到已解析 Transform 的缓存。
        private readonly Dictionary<string, Transform> transformCache = new Dictionary<string, Transform>(StringComparer.Ordinal);
        // 已确认无效的路径集合，避免重复警告。
        private readonly HashSet<string> missingPaths = new HashSet<string>(StringComparer.Ordinal);
        // 路径解析的宿主根节点。
        private readonly Transform root;
        // 输出解析警告时关联的 Unity 对象。
        private readonly UnityEngine.Object logContext;

        /// <summary>
        /// 创建指定行为宿主的骨骼路径解析服务。
        /// </summary>
        /// <param name="root">路径起点；为 null 时解析返回 null。</param>
        /// <param name="logContext">警告日志关联对象；允许为 null。</param>
        public BehaviorTransformResolver(Transform root, UnityEngine.Object logContext)
        {
            this.root = root;
            this.logContext = logContext;
        }

        /// <summary>
        /// 解析参考骨骼；无效路径会警告一次并退回宿主根节点。
        /// </summary>
        /// <param name="path">以宿主根节点为起点的层级路径；为空时返回 null。</param>
        /// <returns>找到的 Transform、无效路径时的宿主根节点，或根节点无效时的 null。</returns>
        public Transform Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || root == null) return null;
            if (transformCache.TryGetValue(path, out Transform cached)) return cached;

            // 先匹配当前相对路径，再兼容带旧根节点标记的导出路径。
            Transform found = FindChildByPath(root, path);
            if (found == null && TryNormalizeHostRelativePath(path, out string normalizedPath))
            {
                found = string.IsNullOrWhiteSpace(normalizedPath) ? root : FindChildByPath(root, normalizedPath);
                if (found != null) transformCache[normalizedPath] = found;
            }

            // 缓存缺失结果并只记录一次，避免运行时反复查找和刷屏。
            if (found == null)
            {
                if (missingPaths.Add(path))
                    Debug.LogWarning($"未找到行为骨骼路径：{path}，将退回行为宿主根节点。", logContext);
                transformCache[path] = root;
                return root;
            }

            transformCache[path] = found;
            return found;
        }

        #region Private

        /// <summary>
        /// 尝试移除旧导出路径中的宿主根节点标记。
        /// </summary>
        /// <param name="path">原始层级路径。</param>
        /// <param name="normalizedPath">成功时返回相对宿主根节点的路径。</param>
        /// <returns>路径包含可移除的旧根节点标记时返回 true。</returns>
        private bool TryNormalizeHostRelativePath(string path, out string normalizedPath)
        {
            normalizedPath = null;
            if (string.IsNullOrWhiteSpace(path)) return false;

            // 首段若已是根节点的直接子物体，说明该路径本身就是相对路径。
            string trimmedPath = path.Trim();
            int slashIndex = trimmedPath.IndexOf('/');
            if (slashIndex < 0)
            {
                if (!LooksLikeLegacyRootMarker(trimmedPath)) return false;
                normalizedPath = string.Empty;
                return true;
            }

            string firstSegment = trimmedPath.Substring(0, slashIndex);
            if (HasDirectChildNamed(firstSegment)) return false;
            normalizedPath = trimmedPath.Substring(slashIndex + 1).TrimStart('/');
            return !string.IsNullOrWhiteSpace(normalizedPath) || LooksLikeLegacyRootMarker(firstSegment);
        }

        /// <summary>
        /// 判断根节点是否直接包含指定名称的子节点。
        /// </summary>
        /// <param name="childName">待匹配的子节点名称。</param>
        /// <returns>存在同名直接子节点时返回 true。</returns>
        private bool HasDirectChildNamed(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName) || root == null) return false;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// 判断路径首段是否符合旧根节点标记的宽松格式。
        /// </summary>
        /// <param name="value">待判断的路径首段。</param>
        /// <returns>同时包含字母和数字且只含允许字符时返回 true。</returns>
        private static bool LooksLikeLegacyRootMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            bool hasLetter = false;
            bool hasDigit = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetter(character)) hasLetter = true;
                else if (char.IsDigit(character)) hasDigit = true;
                else if (character != '_' && character != '-') return false;
            }

            return hasLetter && hasDigit;
        }

        /// <summary>
        /// 按斜杠分隔的层级路径在指定根节点下查找 Transform。
        /// </summary>
        /// <param name="searchRoot">路径查找起点；为 null 时返回 null。</param>
        /// <param name="path">以斜杠分隔的层级路径。</param>
        /// <returns>找到的 Transform；任一层级不存在时返回 null。</returns>
        private static Transform FindChildByPath(Transform searchRoot, string path)
        {
            if (searchRoot == null || string.IsNullOrWhiteSpace(path)) return null;
            string[] parts = path.Split('/');
            int startIndex = parts.Length > 0 && string.Equals(parts[0], searchRoot.name, StringComparison.Ordinal) ? 1 : 0;
            Transform current = searchRoot;
            for (int index = startIndex; index < parts.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(parts[index])) continue;
                current = current.Find(parts[index]);
                if (current == null) return null;
            }

            return current;
        }

        #endregion
    }
}
