using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 保存 Hitbox 轨道作者期预览状态并驱动 Scene 绘制。
    /// </summary>
    internal static class HitboxAuthoringContext
    {
        // 是否在 Scene 中绘制作者期 Hitbox 线框。
        private static bool showAuthoringHitboxGizmos = true;
        // 当前在 Inspector 中选中的 Hitbox 片段资产，供 Scene 预览读取。
        private static BehaviorTimelineHitboxClipAsset selectedHitboxClipAsset;
        // Scene 预览注册引用计数，多个 Inspector 同时打开时保持注册。
        private static int hitboxScenePreviewRetainCount;

        /// <summary>获取或设置是否绘制作者期 Hitbox 线框。</summary>
        public static bool ShowAuthoringHitboxGizmos
        {
            get => showAuthoringHitboxGizmos;
            set => showAuthoringHitboxGizmos = value;
        }

        /// <summary>获取或设置当前选中的 Hitbox 片段资产。</summary>
        public static BehaviorTimelineHitboxClipAsset SelectedHitboxClipAsset
        {
            get => selectedHitboxClipAsset;
            set => selectedHitboxClipAsset = value;
        }

        /// <summary>
        /// 增加 Scene 预览注册引用计数，并确保预览已注册。
        /// </summary>
        public static void RetainHitboxScenePreview()
        {
            hitboxScenePreviewRetainCount = Mathf.Max(0, hitboxScenePreviewRetainCount) + 1;
            BehaviorHitboxScenePreview.SetRegistered(true);
        }

        /// <summary>
        /// 减少 Scene 预览注册引用计数，归零时注销预览。
        /// </summary>
        public static void ReleaseHitboxScenePreview()
        {
            hitboxScenePreviewRetainCount = Mathf.Max(0, hitboxScenePreviewRetainCount - 1);
            if (hitboxScenePreviewRetainCount == 0)
                BehaviorHitboxScenePreview.SetRegistered(false);
        }
    }

    /// <summary>
    /// 在 Scene 视图中绘制作者期 Hitbox 预览线框的注册与绘制工具。
    /// </summary>
    internal static class BehaviorHitboxScenePreview
    {
        // 是否已注册 Scene 绘制回调。
        private static bool isRegistered;

        /// <summary>
        /// 注册或注销 Scene 绘制回调。
        /// </summary>
        /// <param name="shouldRegister">true 时注册，false 时注销。</param>
        public static void SetRegistered(bool shouldRegister)
        {
            if (shouldRegister)
            {
                if (isRegistered)
                    return;

                UnityEditor.SceneView.duringSceneGui += OnSceneGui;
                isRegistered = true;
                return;
            }

            if (!isRegistered)
                return;

            UnityEditor.SceneView.duringSceneGui -= OnSceneGui;
            isRegistered = false;
        }

        /// <summary>
        /// Scene GUI 回调：读取选中 Hitbox 资产与 Reference Root，绘制预览。
        /// </summary>
        /// <param name="sceneView">触发绘制的 Scene 视图。</param>
        private static void OnSceneGui(UnityEditor.SceneView sceneView)
        {
            // 未开启预览或缺少资产/根节点时跳过绘制。
            if (!HitboxAuthoringContext.ShowAuthoringHitboxGizmos)
                return;

            BehaviorTimelineHitboxClipAsset hitboxClipAsset = HitboxAuthoringContext.SelectedHitboxClipAsset;
            if (hitboxClipAsset == null || hitboxClipAsset.hitboxData == null)
                return;

            if (!BehaviorEditorContext.TryGetReferenceRootForInspectedTimeline(out Transform referenceRoot))
                return;

            DrawHitboxPreview(hitboxClipAsset.hitboxData, referenceRoot);
        }

        /// <summary>
        /// 绘制单个 Hitbox 的形状线框与标签。
        /// </summary>
        /// <param name="hitbox">需要预览的 Hitbox 定义。</param>
        /// <param name="referenceRoot">角色根节点，用于解析挂点。</param>
        private static void DrawHitboxPreview(HitboxDef hitbox, Transform referenceRoot)
        {
            if (hitbox == null || referenceRoot == null)
                return;

            ResolvePreviewPose(hitbox, referenceRoot, out Vector3 center, out Quaternion rotation, out Vector3 size);

            // 半透明填充与高亮线框颜色。
            Color fillColor = new Color(1f, 0.25f, 0.25f, 0.08f);
            Color wireColor = new Color(1f, 0.3f, 0.3f, 0.95f);

            using (new UnityEditor.Handles.DrawingScope(wireColor, Matrix4x4.TRS(center, rotation, Vector3.one)))
            {
                UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                switch (hitbox.shape)
                {
                    case HitboxShape.Sphere:
                    {
                        // 球体：填充球 + 三个轴向线框圆。
                        float radius = Mathf.Abs(size.x);
                        UnityEditor.Handles.color = fillColor;
                        UnityEditor.Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, radius * 2f, EventType.Repaint);
                        UnityEditor.Handles.color = wireColor;
                        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
                        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.right, radius);
                        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
                        break;
                    }

                    case HitboxShape.Capsule:
                    {
                        DrawWireCapsule(size, wireColor, fillColor);
                        break;
                    }

                    case HitboxShape.Box:
                    default:
                    {
                        // 方体：填充立方 + 线框。
                        UnityEditor.Handles.color = fillColor;
                        UnityEditor.Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                        UnityEditor.Handles.color = wireColor;
                        UnityEditor.Handles.DrawWireCube(Vector3.zero, size);
                        break;
                    }
                }
            }

            // 在世界坐标显示 Hitbox 名称与形状尺寸。
            string label = string.IsNullOrWhiteSpace(hitbox.name) ? "Hitbox Preview" : hitbox.name;
            UnityEditor.Handles.Label(center, $"{label}\n{hitbox.shape}  Size={size}");
        }

        /// <summary>
        /// 解析 Hitbox 在参考骨骼或世界空间下的中心、旋转与尺寸。
        /// </summary>
        /// <param name="hitbox">Hitbox 定义。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="center">输出的世界中心。</param>
        /// <param name="rotation">输出的世界旋转。</param>
        /// <param name="size">输出的世界尺寸。</param>
        private static void ResolvePreviewPose(HitboxDef hitbox, Transform referenceRoot,
            out Vector3 center, out Quaternion rotation, out Vector3 size)
        {
            // 未指定参考骨骼时使用世界空间偏移。
            if (string.IsNullOrWhiteSpace(hitbox.referenceBone))
            {
                center = hitbox.positionOffset;
                rotation = Quaternion.Euler(hitbox.rotationOffset);
                size = Vector3.Scale(hitbox.size, hitbox.scaleOffset);
                return;
            }

            // 解析参考骨骼，缺失时回退到根节点。
            Transform referenceTransform = BehaviorReferenceBoneEditorUtility.FindChildByPath(referenceRoot, hitbox.referenceBone);
            Transform resolvedTransform = referenceTransform != null ? referenceTransform : referenceRoot;

            center = resolvedTransform.TransformPoint(hitbox.positionOffset);
            rotation = resolvedTransform.rotation * Quaternion.Euler(hitbox.rotationOffset);
            size = Vector3.Scale(hitbox.size, Vector3.Scale(resolvedTransform.lossyScale, hitbox.scaleOffset));
        }

        /// <summary>
        /// 绘制胶囊线框：两端圆盘 + 四段侧弧 + 四条侧线，纯球体时填充。
        /// </summary>
        /// <param name="size">胶囊尺寸（半径 + 总高）。</param>
        /// <param name="wireColor">线框颜色。</param>
        /// <param name="fillColor">填充颜色。</param>
        private static void DrawWireCapsule(Vector3 size, Color wireColor, Color fillColor)
        {
            float radius = Mathf.Abs(size.x);
            float totalHeight = Mathf.Max(radius * 2f, Mathf.Abs(size.y));
            float cylinderHeight = Mathf.Max(0f, totalHeight - radius * 2f);
            Vector3 topCenter = Vector3.up * (cylinderHeight * 0.5f);
            Vector3 bottomCenter = Vector3.down * (cylinderHeight * 0.5f);

            // 顶部与底部的圆盘。
            UnityEditor.Handles.color = wireColor;
            UnityEditor.Handles.DrawWireDisc(topCenter, Vector3.up, radius);
            UnityEditor.Handles.DrawWireDisc(bottomCenter, Vector3.up, radius);
            // 四个方向的侧弧。
            UnityEditor.Handles.DrawWireArc(topCenter, Vector3.forward, Vector3.left, 180f, radius);
            UnityEditor.Handles.DrawWireArc(topCenter, Vector3.right, Vector3.forward, 180f, radius);
            UnityEditor.Handles.DrawWireArc(bottomCenter, Vector3.forward, Vector3.right, 180f, radius);
            UnityEditor.Handles.DrawWireArc(bottomCenter, Vector3.right, Vector3.back, 180f, radius);

            Vector3[] sideOffsets =
            {
                Vector3.left * radius,
                Vector3.right * radius,
                Vector3.forward * radius,
                Vector3.back * radius
            };

            // 四根连接上下圆盘的侧线。
            for (int i = 0; i < sideOffsets.Length; i++)
                UnityEditor.Handles.DrawLine(topCenter + sideOffsets[i], bottomCenter + sideOffsets[i]);

            // 纯球体（高度等于直径）时填充实体。
            if (cylinderHeight <= 0f)
            {
                UnityEditor.Handles.color = fillColor;
                UnityEditor.Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, radius * 2f, EventType.Repaint);
            }
        }
    }

    /// <summary>
    /// 参考骨骼路径的编辑工具：生成下拉选项、相对路径解析与骨骼查找。
    /// </summary>
    public static class BehaviorReferenceBoneEditorUtility
    {
        // 世界坐标选项的显示文本。
        public const string WorldOptionLabel = "<World>";

        /// <summary>
        /// 格式化骨骼路径显示文本；空路径显示为世界坐标选项。
        /// </summary>
        /// <param name="referenceBone">骨骼相对路径。</param>
        /// <returns>显示用文本。</returns>
        public static string FormatReferenceBoneLabel(string referenceBone)
        {
            return string.IsNullOrWhiteSpace(referenceBone) ? WorldOptionLabel : referenceBone;
        }

        /// <summary>
        /// 构建根节点下全部骨骼路径的下拉选项。
        /// </summary>
        /// <param name="root">角色根节点。</param>
        /// <returns>骨骼路径选项数组，首个为世界坐标。</returns>
        public static string[] BuildReferenceBoneOptions(Transform root)
        {
            if (root == null)
                return new[] { WorldOptionLabel };

            List<string> options = new List<string> { WorldOptionLabel, root.name };
            AppendReferenceBoneOptions(root, root.name, options);
            return options.ToArray();
        }

        /// <summary>
        /// 尝试构建目标骨骼相对根节点的路径。
        /// </summary>
        /// <param name="root">角色根节点。</param>
        /// <param name="target">目标骨骼。</param>
        /// <param name="referenceBone">输出的相对路径。</param>
        /// <returns>目标为空时返回 true 且路径为空；目标不在根节点层级下返回 false。</returns>
        public static bool TryBuildRelativeBonePath(Transform root, Transform target, out string referenceBone)
        {
            if (target == null)
            {
                referenceBone = string.Empty;
                return false;
            }

            // 未指定根节点时按空路径处理。
            if (root == null)
            {
                referenceBone = string.Empty;
                return true;
            }

            referenceBone = BuildRelativeBonePath(root, target);
            return !string.IsNullOrWhiteSpace(referenceBone);
        }

        /// <summary>
        /// 构建目标骨骼相对根节点的路径字符串。
        /// </summary>
        /// <param name="root">角色根节点。</param>
        /// <param name="target">目标骨骼。</param>
        /// <returns>相对路径；目标不在层级下返回空字符串。</returns>
        public static string BuildRelativeBonePath(Transform root, Transform target)
        {
            if (root == null || target == null)
                return string.Empty;

            // 从目标向上收集路径段直到根节点。
            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Add(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            // 未到达根节点说明目标不在层级下。
            if (current != root)
                return string.Empty;

            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>
        /// 按相对路径查找骨骼 Transform。
        /// </summary>
        /// <param name="root">角色根节点。</param>
        /// <param name="path">骨骼相对路径。</param>
        /// <returns>找到的骨骼；未找到或路径为空时返回根节点/null。</returns>
        public static Transform FindChildByPath(Transform root, string path)
        {
            if (root == null)
                return null;

            // 空路径视为根节点自身。
            if (string.IsNullOrWhiteSpace(path))
                return root;

            string[] parts = path.Split('/');
            int startIndex = 0;
            if (parts.Length > 0 && string.Equals(parts[0], root.name, StringComparison.Ordinal))
                startIndex = 1;

            // 沿路径逐段查找子节点。
            Transform current = root;
            for (int i = startIndex; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                current = current.Find(parts[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        /// <summary>
        /// 递归收集当前节点全部子节点的路径到选项列表。
        /// </summary>
        /// <param name="current">当前节点。</param>
        /// <param name="currentPath">当前节点路径前缀。</param>
        /// <param name="options">选项列表。</param>
        private static void AppendReferenceBoneOptions(Transform current, string currentPath, List<string> options)
        {
            if (current == null || options == null)
                return;

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                string childPath = $"{currentPath}/{child.name}";
                options.Add(childPath);
                AppendReferenceBoneOptions(child, childPath, options);
            }
        }
    }

}
