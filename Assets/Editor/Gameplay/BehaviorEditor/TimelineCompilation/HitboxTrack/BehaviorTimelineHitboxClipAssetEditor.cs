using System;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// Hitbox 时间轴片段的自定义 Inspector：编辑形状、挂点、尺寸、伤害参数并联动 Scene 预览。
    /// </summary>
    [UnityEditor.CustomEditor(typeof(BehaviorTimelineHitboxClipAsset))]
    public sealed class BehaviorTimelineHitboxClipAssetEditor : UnityEditor.Editor
    {
        // Hitbox 尺寸的最小值，防止形状退化为零或负体积。
        private const float MinimumHitboxSize = 0.1f;
        // 当前选中的骨骼 Transform，用于读取骨骼相对路径。
        private Transform referenceBoneTarget;

        /// <summary>
        /// 启用时保留 Hitbox Scene 预览的注册计数。
        /// </summary>
        private void OnEnable()
        {
            HitboxAuthoringContext.RetainHitboxScenePreview();
        }

        /// <summary>
        /// 停用时清空选中资产并释放 Scene 预览注册。
        /// </summary>
        private void OnDisable()
        {
            // 当前选中的 Hitbox 资产由本 Inspector 持有，停用时清空引用。
            if (HitboxAuthoringContext.SelectedHitboxClipAsset == target)
                HitboxAuthoringContext.SelectedHitboxClipAsset = null;

            HitboxAuthoringContext.ReleaseHitboxScenePreview();
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>
        /// 绘制 Hitbox 属性面板：形状、挂点、尺寸、伤害参数，并联动 Scene 预览。
        /// </summary>
        public override void OnInspectorGUI()
        {
            // 记录当前编辑的 Hitbox 资产供 Scene 预览读取。
            HitboxAuthoringContext.SelectedHitboxClipAsset = target as BehaviorTimelineHitboxClipAsset;
            serializedObject.Update();
            UnityEditor.EditorGUI.BeginChangeCheck();

            UnityEditor.SerializedProperty hitboxDataProperty = serializedObject.FindProperty("hitboxData");
            if (hitboxDataProperty == null)
            {
                // 序列化结构缺失时回退到默认面板。
                DrawDefaultInspector();
                bool hasDefaultChanges = serializedObject.ApplyModifiedProperties();
                if (hasDefaultChanges)
                    UnityEditor.SceneView.RepaintAll();
                return;
            }

            UnityEditor.EditorGUILayout.HelpBox(
                "Hitbox 的生效时间和持续时间取自 Timeline 片段本身，这里只编辑形状、挂点、数值和命中效果。",
                UnityEditor.MessageType.None);

            // 预览开关：控制 Scene 中是否绘制 Hitbox 线框。
            bool showPreview = UnityEditor.EditorGUILayout.ToggleLeft(
                "Show Scene Hitbox Preview",
                HitboxAuthoringContext.ShowAuthoringHitboxGizmos);
            if (showPreview != HitboxAuthoringContext.ShowAuthoringHitboxGizmos)
            {
                HitboxAuthoringContext.ShowAuthoringHitboxGizmos = showPreview;
                UnityEditor.SceneView.RepaintAll();
            }

            // 缺少 Reference Root 时提示预览不可用。
            if (!BehaviorEditorContext.TryGetReferenceRootForInspectedTimeline(out _))
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "当前 Timeline 没有可用的作者期 Reference Root，Scene 里的 Hitbox 预览不会显示。请在 Behavior Editor Timeline 窗口中指定 Reference Root，并开始编辑当前 Timeline。",
                    UnityEditor.MessageType.Info);
            }

            // 基础字段：名称与形状。
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("name"));
            DrawShapeField(hitboxDataProperty);

            // 挂点绑定与形状专属尺寸。
            BehaviorTimelineTransformBindingEditorUtility.DrawFields(
                hitboxDataProperty,
                ref referenceBoneTarget);
            DrawShapeSpecificSizeFields(hitboxDataProperty);

            // 命中执行配置。
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("HitExecute", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("execute"));

            // 面板有变更时刷新 Scene 预览。
            bool uiChanged = UnityEditor.EditorGUI.EndChangeCheck();
            bool applied = serializedObject.ApplyModifiedProperties();
            if (uiChanged || applied)
                UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>
        /// 绘制形状选择字段，切换形状时自动归一化尺寸。
        /// </summary>
        /// <param name="hitboxDataProperty">Hitbox 数据序列化属性。</param>
        private static void DrawShapeField(UnityEditor.SerializedProperty hitboxDataProperty)
        {
            if (!TryGetShapeAndSizeProperties(
                    hitboxDataProperty,
                    out UnityEditor.SerializedProperty shapeProperty,
                    out UnityEditor.SerializedProperty sizeProperty))
                return;

            UnityEditor.EditorGUI.BeginChangeCheck();
            UnityEditor.EditorGUILayout.PropertyField(shapeProperty);
            if (!UnityEditor.EditorGUI.EndChangeCheck())
                return;

            // 形状变化后按新形状的约束归一化尺寸。
            NormalizeSizeForShape((HitboxShape)shapeProperty.enumValueIndex, sizeProperty);
        }

        /// <summary>
        /// 按当前形状绘制专属的尺寸字段。
        /// </summary>
        /// <param name="hitboxDataProperty">Hitbox 数据序列化属性。</param>
        private static void DrawShapeSpecificSizeFields(UnityEditor.SerializedProperty hitboxDataProperty)
        {
            if (!TryGetShapeAndSizeProperties(
                    hitboxDataProperty,
                    out UnityEditor.SerializedProperty shapeProperty,
                    out UnityEditor.SerializedProperty sizeProperty))
                return;

            HitboxShape shape = (HitboxShape)shapeProperty.enumValueIndex;
            switch (shape)
            {
                case HitboxShape.Sphere:
                    DrawSphereSizeFields(sizeProperty);
                    break;

                case HitboxShape.Capsule:
                    DrawCapsuleSizeFields(sizeProperty);
                    break;

                case HitboxShape.Box:
                default:
                    DrawBoxSizeFields(sizeProperty);
                    break;
            }
        }

        /// <summary>
        /// 绘制方体尺寸字段，分量不允许为负。
        /// </summary>
        /// <param name="sizeProperty">尺寸序列化属性。</param>
        private static void DrawBoxSizeFields(UnityEditor.SerializedProperty sizeProperty)
        {
            Vector3 currentSize = sizeProperty.vector3Value;
            Vector3 nextSize = UnityEditor.EditorGUILayout.Vector3Field("Box Size", currentSize);
            nextSize.x = Mathf.Max(0f, nextSize.x);
            nextSize.y = Mathf.Max(0f, nextSize.y);
            nextSize.z = Mathf.Max(0f, nextSize.z);
            sizeProperty.vector3Value = nextSize;
        }

        /// <summary>
        /// 获取形状与尺寸序列化属性。
        /// </summary>
        /// <param name="hitboxDataProperty">Hitbox 数据序列化属性。</param>
        /// <param name="shapeProperty">输出形状属性。</param>
        /// <param name="sizeProperty">输出尺寸属性。</param>
        /// <returns>两个属性都存在时返回 true。</returns>
        private static bool TryGetShapeAndSizeProperties(
            UnityEditor.SerializedProperty hitboxDataProperty,
            out UnityEditor.SerializedProperty shapeProperty,
            out UnityEditor.SerializedProperty sizeProperty)
        {
            shapeProperty = null;
            sizeProperty = null;
            if (hitboxDataProperty == null)
                return false;

            shapeProperty = hitboxDataProperty.FindPropertyRelative("shape");
            sizeProperty = hitboxDataProperty.FindPropertyRelative("size");
            return shapeProperty != null && sizeProperty != null;
        }

        /// <summary>
        /// 绘制球体半径字段，三个分量保持相等。
        /// </summary>
        /// <param name="sizeProperty">尺寸序列化属性。</param>
        private static void DrawSphereSizeFields(UnityEditor.SerializedProperty sizeProperty)
        {
            float radius = Mathf.Max(MinimumHitboxSize, sizeProperty.vector3Value.x);
            float nextRadius = Mathf.Max(MinimumHitboxSize, UnityEditor.EditorGUILayout.FloatField("Radius", radius));
            sizeProperty.vector3Value = new Vector3(nextRadius, nextRadius, nextRadius);
        }

        /// <summary>
        /// 绘制胶囊半径与高度字段，高度不小于两倍半径。
        /// </summary>
        /// <param name="sizeProperty">尺寸序列化属性。</param>
        private static void DrawCapsuleSizeFields(UnityEditor.SerializedProperty sizeProperty)
        {
            Vector3 currentSize = sizeProperty.vector3Value;
            float radius = Mathf.Max(MinimumHitboxSize, currentSize.x);
            float height = Mathf.Max(radius * 2f, currentSize.y);

            float nextRadius = Mathf.Max(MinimumHitboxSize, UnityEditor.EditorGUILayout.FloatField("Radius", radius));
            float nextHeight = Mathf.Max(nextRadius * 2f,
                UnityEditor.EditorGUILayout.FloatField("Height", height));

            sizeProperty.vector3Value = new Vector3(nextRadius, nextHeight, nextRadius);
        }

        /// <summary>
        /// 按形状约束归一化尺寸，保证形状有效。
        /// </summary>
        /// <param name="shape">目标形状。</param>
        /// <param name="sizeProperty">尺寸序列化属性。</param>
        private static void NormalizeSizeForShape(HitboxShape shape, UnityEditor.SerializedProperty sizeProperty)
        {
            Vector3 size = sizeProperty.vector3Value;
            switch (shape)
            {
                case HitboxShape.Sphere:
                {
                    // 球体三个分量取同一主正值作为半径。
                    float radius = Mathf.Max(MinimumHitboxSize, ResolvePrimaryPositiveValue(size));
                    sizeProperty.vector3Value = new Vector3(radius, radius, radius);
                    break;
                }

                case HitboxShape.Capsule:
                {
                    // 胶囊：半径取主正值，高度不小于两倍半径。
                    float radius = Mathf.Max(MinimumHitboxSize, ResolvePrimaryPositiveValue(size));
                    float height = Mathf.Max(radius * 2f, Mathf.Abs(size.y));
                    sizeProperty.vector3Value = new Vector3(radius, height, radius);
                    break;
                }

                case HitboxShape.Box:
                default:
                {
                    // 方体：分量钳制非负，全零时回退到最小尺寸。
                    Vector3 normalizedSize = new Vector3(
                        Mathf.Max(0f, size.x),
                        Mathf.Max(0f, size.y),
                        Mathf.Max(0f, size.z));
                    if (normalizedSize == Vector3.zero)
                        normalizedSize = Vector3.one * MinimumHitboxSize;
                    sizeProperty.vector3Value = normalizedSize;
                    break;
                }
            }
        }

        /// <summary>
        /// 从尺寸向量中取第一个正分量，用于推导半径。
        /// </summary>
        /// <param name="size">尺寸向量。</param>
        /// <returns>第一个正分量；全非正时返回 0。</returns>
        private static float ResolvePrimaryPositiveValue(Vector3 size)
        {
            if (size.x > 0f)
                return size.x;

            if (size.y > 0f)
                return size.y;

            if (size.z > 0f)
                return size.z;

            return 0f;
        }
    }

}
