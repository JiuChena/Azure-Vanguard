using System;
using UnityEngine;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {
        /// <summary>
        /// 清理资产名中的非法文件名字符，空名回退到默认名。
        /// </summary>
        /// <param name="rawName">原始资产名。</param>
        /// <returns>适合作为 Unity 资产文件名的名称。</returns>
        private static string SanitizeAssetName(string rawName)
        {
            const string fallback = "TimelineBehaviorClip";
            if (string.IsNullOrWhiteSpace(rawName)) return fallback;

            // 删除 Windows 不允许出现在文件名中的字符。
            string trimmed = rawName.Trim();
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(invalidChar.ToString(), string.Empty);

            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        /// <summary>
        /// 确保资产文件夹存在，缺失时逐级创建。
        /// </summary>
        /// <param name="folderPath">Unity 资产目录路径。</param>
        /// <returns>规范化后的有效资产目录路径。</returns>
        private static string EnsureFolder(string folderPath)
        {
            string normalized = string.IsNullOrWhiteSpace(folderPath)
                ? "Assets"
                : folderPath.Replace("\\", "/").TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return current;
        }
    }
}
