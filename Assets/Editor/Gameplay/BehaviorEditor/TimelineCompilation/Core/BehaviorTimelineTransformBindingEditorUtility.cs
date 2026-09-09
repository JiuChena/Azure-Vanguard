using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 为需要骨骼挂点的 Timeline 轨道绘制共享空间绑定控件。
    /// </summary>
    internal static class BehaviorTimelineTransformBindingEditorUtility
    {
        /// <summary>
        /// 绘制骨骼路径、位置偏移、旋转偏移和缩放偏移字段。
        /// </summary>
        /// <param name="dataProperty">包含通用空间绑定字段的轨道数据属性。</param>
        /// <param name="referenceBoneTarget">当前选中的骨骼 Transform 引用。</param>
        public static void DrawFields(UnityEditor.SerializedProperty dataProperty, ref Transform referenceBoneTarget)
        {
            if (dataProperty == null) return;

            // 绘制基础空间数据与骨骼路径作者工具。
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Binding", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.SerializedProperty referenceBoneProperty = dataProperty.FindPropertyRelative("referenceBone");
            SyncReferenceBoneTarget(referenceBoneProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.PropertyField(referenceBoneProperty);
            DrawReferenceBoneAuthoringTools(referenceBoneProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.PropertyField(dataProperty.FindPropertyRelative("positionOffset"));
            UnityEditor.EditorGUILayout.PropertyField(dataProperty.FindPropertyRelative("rotationOffset"));
            UnityEditor.EditorGUILayout.PropertyField(dataProperty.FindPropertyRelative("scaleOffset"));
        }

        /// <summary>
        /// 绘制骨骼路径读取工具：读取目标骨骼路径、使用世界坐标、快速选择下拉。
        /// </summary>
        /// <param name="referenceBoneProperty">骨骼路径序列化属性。</param>
        /// <param name="referenceBoneTarget">当前选中的骨骼 Transform 引用。</param>
        private static void DrawReferenceBoneAuthoringTools(UnityEditor.SerializedProperty referenceBoneProperty,
            ref Transform referenceBoneTarget)
        {
            if (referenceBoneProperty == null) return;

            // 未指定 Reference Root 时提示先到编辑器窗口指定。
            if (!BehaviorEditorContext.TryGetReferenceRootForInspectedTimeline(out Transform referenceRoot))
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "当前没有可用的 Reference Root。请先到 Behavior Editor Timeline 窗口里指定角色根节点，再回到片段属性栏读取骨骼路径。",
                    UnityEditor.MessageType.Info);
                return;
            }

            SyncReferenceBoneTarget(referenceBoneProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.LabelField("Reference Root", referenceRoot.name);
            referenceBoneTarget = (Transform)UnityEditor.EditorGUILayout.ObjectField(
                "Target Bone", referenceBoneTarget, typeof(Transform), true);

            // 目标骨骼不在 Reference Root 层级下时给出警告。
            if (referenceBoneTarget != null && referenceBoneTarget != referenceRoot &&
                !referenceBoneTarget.IsChildOf(referenceRoot))
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "当前目标骨骼不在 Reference Root 的层级下，无法生成相对骨骼路径。",
                    UnityEditor.MessageType.Warning);
            }

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Read Path From Target"))
                {
                    // 将选中 Transform 转为相对 Reference Root 的层级路径。
                    if (referenceBoneTarget == null)
                        Debug.LogWarning("没有选择 Target Bone，无法读取骨骼路径。");
                    else if (BehaviorReferenceBoneEditorUtility.TryBuildRelativeBonePath(
                                 referenceRoot, referenceBoneTarget, out string resolvedPath))
                    {
                        referenceBoneProperty.stringValue = resolvedPath;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"目标骨骼 '{referenceBoneTarget.name}' 不在 Reference Root '{referenceRoot.name}' 的层级下，无法生成路径。");
                    }
                }

                if (GUILayout.Button("Use World"))
                {
                    // 清空路径后，运行时将位置与旋转偏移解释为世界空间值。
                    referenceBoneProperty.stringValue = string.Empty;
                    referenceBoneTarget = null;
                }
            }

            // 快速选择下拉：列出全部骨骼路径，当前值缺失时附加占位项。
            string[] options = BehaviorReferenceBoneEditorUtility.BuildReferenceBoneOptions(referenceRoot);
            string missingValue = null;
            int currentIndex = ResolveReferenceBoneOptionIndex(options, referenceBoneProperty.stringValue);
            if (currentIndex < 0 && !string.IsNullOrWhiteSpace(referenceBoneProperty.stringValue))
            {
                missingValue = referenceBoneProperty.stringValue;
                Array.Resize(ref options, options.Length + 1);
                currentIndex = options.Length - 1;
                options[currentIndex] = $"(Missing: {missingValue})";
            }

            if (currentIndex < 0) currentIndex = 0;
            int nextIndex = UnityEditor.EditorGUILayout.Popup("Quick Select", currentIndex, options);
            if (nextIndex != currentIndex)
            {
                referenceBoneProperty.stringValue = ResolveReferenceBoneOptionValue(options, nextIndex, missingValue);
                SyncReferenceBoneTarget(referenceBoneProperty, ref referenceBoneTarget);
            }
        }

        /// <summary>
        /// 在选项数组中查找当前骨骼路径的索引。
        /// </summary>
        /// <param name="options">骨骼路径选项数组。</param>
        /// <param name="currentValue">当前骨骼路径值。</param>
        /// <returns>匹配索引；未找到时返回 -1。</returns>
        private static int ResolveReferenceBoneOptionIndex(string[] options, string currentValue)
        {
            if (options == null || options.Length == 0) return -1;

            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], currentValue, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        /// <summary>
        /// 根据选中索引解析骨骼路径值；选中缺失占位项时保留原缺失值。
        /// </summary>
        /// <param name="options">骨骼路径选项数组。</param>
        /// <param name="selectedIndex">当前选中的选项索引。</param>
        /// <param name="missingValue">当前缺失的骨骼路径值。</param>
        /// <returns>需要写回序列化属性的骨骼路径。</returns>
        private static string ResolveReferenceBoneOptionValue(string[] options, int selectedIndex, string missingValue)
        {
            if (options == null || options.Length == 0 || selectedIndex < 0 || selectedIndex >= options.Length)
                return string.Empty;

            // 选中缺失占位项时写回原缺失值。
            if (!string.IsNullOrWhiteSpace(missingValue) && selectedIndex == options.Length - 1 &&
                string.Equals(options[selectedIndex], $"(Missing: {missingValue})", StringComparison.Ordinal))
            {
                return missingValue;
            }

            return selectedIndex == 0 ? string.Empty : options[selectedIndex];
        }

        /// <summary>
        /// 将骨骼路径字符串解析为对应 Transform 并同步到缓存字段。
        /// </summary>
        /// <param name="referenceBoneProperty">骨骼路径序列化属性。</param>
        /// <param name="referenceBoneTarget">需要同步的骨骼 Transform 引用。</param>
        private static void SyncReferenceBoneTarget(UnityEditor.SerializedProperty referenceBoneProperty,
            ref Transform referenceBoneTarget)
        {
            if (referenceBoneProperty == null ||
                !BehaviorEditorContext.TryGetReferenceRootForInspectedTimeline(out Transform referenceRoot))
            {
                referenceBoneTarget = null;
                return;
            }

            // 路径为空表示世界坐标，无骨骼可同步。
            if (string.IsNullOrWhiteSpace(referenceBoneProperty.stringValue))
            {
                referenceBoneTarget = null;
                return;
            }

            referenceBoneTarget = BehaviorReferenceBoneEditorUtility.FindChildByPath(
                referenceRoot, referenceBoneProperty.stringValue);
        }
    }
}
