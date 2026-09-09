using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为事件时间轴片段的自定义 Inspector，配置执行资产与空间绑定。
    /// </summary>
    [UnityEditor.CustomEditor(typeof(BehaviorTimelineEventClipAsset))]
    public sealed class BehaviorTimelineEventClipAssetEditor : UnityEditor.Editor
    {
        // 当前选中的骨骼 Transform，用于读取骨骼相对路径。
        private Transform referenceBoneTarget;

        /// <summary>
        /// 绘制事件属性面板：执行资产与挂点绑定。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UnityEditor.SerializedProperty eventDataProperty = serializedObject.FindProperty("eventData");
            if (eventDataProperty == null)
            {
                // 序列化结构缺失时回退到默认面板。
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // 先完成空间绑定，再在面板底部配置项目侧执行资产。
            BehaviorTimelineTransformBindingEditorUtility.DrawFields(eventDataProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("execute"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
