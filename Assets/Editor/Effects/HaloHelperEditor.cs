#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Effects.Editor
{
    /// <summary>
    /// HaloHelper 自定义 Inspector：在原面板下方追加「自动查找目标 Target」按钮，
    /// 按统一骨骼路径（跨角色一致，准绳取自 CH0221 预制体）自动补 followTarget，免去逐预制体手拖。
    /// </summary>
    [CustomEditor(typeof(HaloHelper))]
    internal sealed class HaloHelperEditor : UnityEditor.Editor
    {
        //跟随目标的统一骨骼路径：biped 骨骼命名跨角色一致（头部骨骼）
        private const string HeadBonePath =
            "bone_root/Bip001/Bip001 Pelvis/Bip001 Spine/Bip001 Spine1/Bip001 Neck/Bip001 Head";

        //路径未命中时的兜底骨骼名（层级有差异时按名唯一定位）
        private const string HeadBoneName = "Bip001 Head";

        //偏移基准空间的统一节点：biped 身体朝向承载者（动画整体转向由它驱动，脊柱/颈部细节不干扰）
        private const string BodyNodePath = "bone_root/Bip001";

        /// <summary>绘制默认 Inspector、自动查找与偏移绑定按钮、绑定状态一览。</summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("自动查找目标 Target", GUILayout.Height(24f)))
                AutoFindTarget();

            //绑定当前偏移：固化 Halo 与 Target 的相对姿态为序列化基准（Start 优先采用）
            if (GUILayout.Button("绑定当前偏移 Offset", GUILayout.Height(24f)))
            {
                ((HaloHelper)target).BindOffsetFromCurrentPose();
                serializedObject.Update();
            }

            //绑定状态一览：未绑定时跟随基准来自 Start 实测
            SerializedProperty boundProp = serializedObject.FindProperty("hasBoundOffset");
            if (boundProp != null && boundProp.boolValue)
            {
                Vector3 offset = serializedObject.FindProperty("boundLocalOffset").vector3Value;
                //offsetSpace 是私有序列化字段，经 SerializedObject 读取
                SerializedProperty spaceProp = serializedObject.FindProperty("offsetSpace");
                Transform space = spaceProp != null ? (Transform)spaceProp.objectReferenceValue : null;
                string spaceName = space != null ? space.name : "跟随目标自身";
                EditorGUILayout.HelpBox($"已绑定偏移（{spaceName} 本地空间）：{offset}", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("未绑定偏移：跟随基准取 Start 时相对姿态", MessageType.None);
            }
        }

        /// <summary>
        /// 自动查找跟随目标：定位模型根后先按统一骨骼路径命中，失败按骨骼名唯一定位；
        /// 写回 followTarget（ApplyModifiedProperties 自带 Undo），结果与命中方式打日志。
        /// </summary>
        private void AutoFindTarget()
        {
            HaloHelper helper = (HaloHelper)target;
            Transform root = FindModelRoot(helper.transform);
            if (root == null)
            {
                Debug.LogError("[HaloHelper] 未定位到模型根，无法查找目标", helper);
                return;
            }

            //先按路径精确命中，失败按名兜底（要求唯一，多个同名视为歧义不写）
            Transform found = root.Find(HeadBonePath);
            string how = $"路径命中：{HeadBonePath}";
            if (found == null)
            {
                how = $"按名命中：{HeadBoneName}";
                found = FindUniqueByName(root, HeadBoneName);
                if (found == null)
                {
                    Debug.LogError($"[HaloHelper] {root.name} 下既无路径 {HeadBonePath} 也无骨骼 {HeadBoneName}", helper);
                    return;
                }
            }

            SerializedObject so = new SerializedObject(helper);
            so.FindProperty("followTarget").objectReferenceValue = found;
            //偏移基准空间指向身体朝向节点（biped 为 Bip001；缺失时退化模型根）：方向随身体转向、不受脊柱/颈部动作干扰
            Transform bodyNode = root.Find(BodyNodePath) != null ? root.Find(BodyNodePath) : root;
            so.FindProperty("offsetSpace").objectReferenceValue = bodyNode;
            so.ApplyModifiedProperties();
            Debug.Log($"[HaloHelper] followTarget ← {GetPath(found, root)}；offsetSpace ← {bodyNode.name}（{how}）", helper);
        }

        /// <summary>
        /// 定位模型根：预制体模式取预制体根，场景实例取最外层预制体实例根，其余取层级最顶。
        /// </summary>
        private static Transform FindModelRoot(Transform node)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(node.gameObject)) return stage.prefabContentsRoot.transform;

            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(node.gameObject);
            return instanceRoot != null ? instanceRoot.transform : node.root;
        }

        /// <summary>按名在根下唯一定位骨骼；命中多个返回 null（歧义保护）。</summary>
        private static Transform FindUniqueByName(Transform root, string boneName)
        {
            Transform found = null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != boneName) continue;
                if (found != null) return null;
                found = candidate;
            }
            return found;
        }

        /// <summary>节点相对根的路径（根名不含在内）。</summary>
        private static string GetPath(Transform node, Transform root)
        {
            string path = node.name;
            while (node.parent != null && node != root)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
