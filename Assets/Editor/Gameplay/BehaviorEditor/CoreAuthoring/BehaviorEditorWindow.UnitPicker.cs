using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow
    {
        // 主窗口滚动位置，不影响原有导出表单。
        private Vector2 windowScrollPosition;
        // 可选单位快捷选择区的展开状态。
        [SerializeField, Tooltip("展开单位行为引用收集与快捷选择区域")]
        private bool showUnitBehaviorPicker;
        // 收集引用的单位实例、预制体或单位配置 SO。
        [SerializeField, Header("单位行为引用"), Tooltip("单位对象或配置 SO；仅用于收集序列化引用")]
        private Object behaviorTargetUnit;
        // 显式指定的单位资产目录，不自动扫描整个项目。
        [SerializeField, Tooltip("拖入单位资产子目录，收集其中所有 Timeline 和 BehaviorClip")]
        private DefaultAsset behaviorUnitFolder;
        // 当前单位的行为引用集合，可手动追加、替换和移除。
        [SerializeField, Tooltip("收集到的行为资产引用；也可手动补充 Timeline 和 BehaviorClip")]
        private List<Object> unitBehaviorReferences = new List<Object>();
        // 是否展示引用列表以解决缺失和重名冲突。
        [SerializeField, Tooltip("展开引用列表以补充或移除行为资产")]
        private bool showUnitBehaviorReferences;
        // 快捷选择时将有效场景单位作为作者期参考根节点。
        [SerializeField, Tooltip("快捷选择时使用场景中的目标单位作为 Reference Root；配置和预制体不自动用作预览根")]
        private bool useUnitAsReferenceRoot = true;
        // 缓存名称分组，只有引用变化或主动刷新时重建。
        private List<BehaviorUnitAssetCollection.Pair> unitBehaviorPairs;
        // 当前编辑配对行的高亮背景色：半透明深色，深浅皮肤下都可读。
        private static readonly Color currentPairRowColor = new Color(0.08f, 0.20f, 0.34f, 0.35f);

        #region Unit Picker UI

        /// <summary>绘制可选的单位行为引用及同名配对区，不改变原有手动导出流程。</summary>
        private void DrawUnitBehaviorPicker()
        {
            // 作者期和播放期间锁定快捷切换，避免旧会话被新的导出目标覆盖。
            showUnitBehaviorPicker = EditorGUILayout.Foldout(showUnitBehaviorPicker, "单位行为快捷选择（可选）", true);
            if (!showUnitBehaviorPicker) return;
            bool locked = authoringSessionContext != null || EditorApplication.isPlayingOrWillChangePlaymode;
            if (locked) EditorGUILayout.HelpBox("快捷选择已锁定：请先结束当前编辑并导出，且退出 Play Mode。", MessageType.Info);
            using (new EditorGUI.DisabledScope(locked))
            {
                EditorGUILayout.HelpBox("指定单位和/或单位资产目录后点击收集。同名配对区只切换引用，不会自动导出或覆盖资产。", MessageType.None);
                EditorGUI.BeginChangeCheck();
                Object newUnit = EditorGUILayout.ObjectField("目标单位 / 配置", behaviorTargetUnit, typeof(Object), true);
                DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField("单位资产目录", behaviorUnitFolder, typeof(DefaultAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    // 更换单位范围时清空旧列表，原有 Source/Target 手动选择仍保持不变。
                    behaviorTargetUnit = newUnit;
                    behaviorUnitFolder = newFolder;
                    unitBehaviorReferences.Clear();
                    RebuildUnitBehaviorPairs();
                }
                string folder = AssetDatabase.GetAssetPath(behaviorUnitFolder);
                bool validFolder = behaviorUnitFolder == null || (folder.StartsWith("Assets/", System.StringComparison.Ordinal) && AssetDatabase.IsValidFolder(folder));
                bool validUnit = behaviorTargetUnit == null || behaviorTargetUnit is GameObject ||
                    behaviorTargetUnit is Component || behaviorTargetUnit is ScriptableObject;
                if (!validFolder) EditorGUILayout.HelpBox("目录必须是 Assets 下的单位子目录，不能选择文件或 Assets 根目录。", MessageType.Warning);
                if (!validUnit) EditorGUILayout.HelpBox("目标单位仅接受 GameObject、Component 或配置 ScriptableObject。", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!validFolder || !validUnit || (behaviorTargetUnit == null && behaviorUnitFolder == null)))
                {
                    if (GUILayout.Button("收集 / 刷新单位行为引用（替换列表）"))
                    {
                        // 刷新替换当前收集列表；手动额外引用可在下方重新补充。
                        unitBehaviorReferences = BehaviorUnitAssetCollection.Collect(behaviorTargetUnit, folder);
                        RebuildUnitBehaviorPairs();
                    }
                }
                useUnitAsReferenceRoot = EditorGUILayout.Toggle("使用单位作为 Reference Root", useUnitAsReferenceRoot);
                DrawUnitBehaviorReferences();

                // 每行显示配对状态；只允许唯一 Timeline 的无歧义组快捷切换。
                EditorGUILayout.LabelField($"行为名称：{unitBehaviorPairs.Count} 组（精确同名，区分大小写）", EditorStyles.boldLabel);
                foreach (BehaviorUnitAssetCollection.Pair pair in unitBehaviorPairs)
                {
                    string status = pair.HasConflict ? $"重名冲突 T:{pair.Timelines.Count} / SO:{pair.Clips.Count}" :
                        pair.Timelines.Count == 0 ? "缺少 Timeline" : pair.Clips.Count == 0 ? "缺少 SO（检查输出路径）" : "已配对";
                    Rect rowRect = EditorGUILayout.BeginHorizontal();
                    // 当前编辑源所属的配对行加深背景，标出正在做的一组；跟随表单真实状态，作者期锁定时同样生效。
                    bool isCurrentPair = sourceTimeline != null && pair.Timelines.Contains(sourceTimeline);
                    if (isCurrentPair && Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rowRect, currentPairRowColor);
                    EditorGUILayout.LabelField(new GUIContent(pair.Name, status), GUILayout.MinWidth(70f));
                    EditorGUILayout.LabelField(status, GUILayout.MinWidth(130f));
                    using (new EditorGUI.DisabledScope(pair.HasConflict || pair.Timelines.Count != 1))
                    {
                        if (GUILayout.Button("选择", GUILayout.Width(52f))) SelectUnitBehaviorPair(pair);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (unitBehaviorPairs.Count == 0) EditorGUILayout.HelpBox("暂无引用。可收集单位资产，或展开引用列表手动添加。", MessageType.None);
                if (sourceTimeline != null && targetBehaviorClip == null)
                {
                    EditorGUILayout.HelpBox("未指定目标 SO：请检查下方 Output Folder。导出会沿用原有规则，复用该路径的同名 SO，或在不存在时创建。", MessageType.Warning);
                }
                EditorGUILayout.Space(8f);
            }
        }

        /// <summary>编辑引用列表并允许一次拖入多个行为资产。</summary>
        private void DrawUnitBehaviorReferences()
        {
            // 列表可显式解决缺失或重名，不通过猜测自动排除资产。
            showUnitBehaviorReferences = EditorGUILayout.Foldout(showUnitBehaviorReferences, $"行为资产引用（{unitBehaviorReferences.Count}）", true);
            if (!showUnitBehaviorReferences) return;
            bool changed = false;
            for (int i = 0; i < unitBehaviorReferences.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Object current = unitBehaviorReferences[i];
                    Object replacement = EditorGUILayout.ObjectField(current, typeof(Object), false);
                    if (replacement != current && (replacement == null || replacement is UnityEngine.Timeline.TimelineAsset || replacement is BehaviorClip))
                    {
                        unitBehaviorReferences[i] = replacement;
                        changed = true;
                    }
                    if (GUILayout.Button("移除", GUILayout.Width(48f)))
                    {
                        unitBehaviorReferences.RemoveAt(i--);
                        changed = true;
                    }
                }
            }
            if (GUILayout.Button("添加引用槽")) unitBehaviorReferences.Add(null);

            // 批量拖入仅接收两种行为资产，忽略其他类型并去重。
            Rect dropArea = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "将多个 Timeline / BehaviorClip 拖到这里");
            Event evt = Event.current;
            if (GUI.enabled && dropArea.Contains(evt.mousePosition) &&
                (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object reference in DragAndDrop.objectReferences)
                    {
                        if ((reference is UnityEngine.Timeline.TimelineAsset || reference is BehaviorClip) &&
                            !unitBehaviorReferences.Contains(reference))
                        {
                            unitBehaviorReferences.Add(reference);
                            changed = true;
                        }
                    }
                }
                evt.Use();
            }
            if (changed) RebuildUnitBehaviorPairs();
        }

        #endregion

        #region Unit Selection

        /// <summary>重建已有引用的名称分组；不访问资产数据库。</summary>
        private void RebuildUnitBehaviorPairs()
        {
            // 防御旧窗口序列化状态中的空集合。
            if (unitBehaviorReferences == null) unitBehaviorReferences = new List<Object>();
            unitBehaviorPairs = BehaviorUnitAssetCollection.BuildPairs(unitBehaviorReferences);
        }

        /// <summary>将无歧义配对填入原有表单；不开始作者期、不导出、不修改资产。</summary>
        /// <param name="pair">唯一 Timeline 且没有重名冲突的配对；允许 null。</param>
        private void SelectUnitBehaviorPair(BehaviorUnitAssetCollection.Pair pair)
        {
            // 即使从非 GUI 调用，也禁止更换正在运行的作者期上下文。
            if (pair == null || pair.HasConflict || pair.Timelines.Count != 1 ||
                authoringSessionContext != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            sourceTimeline = pair.Timelines[0];
            targetBehaviorClip = pair.Clips.Count == 1 ? pair.Clips[0] : null;
            outputAssetName = pair.Name;

            // 仅场景单位可自动作为参考根；不把预制体资产用于预览组件操作。
            GameObject root = behaviorTargetUnit as GameObject;
            if (behaviorTargetUnit is Component component) root = component.gameObject;
            if (useUnitAsReferenceRoot && root != null && !EditorUtility.IsPersistent(root) && root.scene.IsValid())
            {
                previewReferenceRoot = root;
                autoAssignedReferenceRoot = false;
            }
        }

        #endregion
    }
}
