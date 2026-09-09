using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {
        /// <summary>
        /// 将 Timeline 全部轨道导出编译为 BehaviorClip 资产。
        /// </summary>
        private void ExportToBehaviorClip()
        {
            if (sourceTimeline == null) return;

            BehaviorClip target = ResolveTargetBehaviorClip();
            if (target == null) return;

            // 创建导出环境，由自动发现的各轨道编译器独立写入自己的运行时数据。
            PlayableDirector exportDirector = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            Transform exportReferenceRoot = ResolveExportReferenceRoot();
            BehaviorPlaybackSettings fallbackSettings = new BehaviorPlaybackSettings
            {
                wrapMode = wrapMode,
                speedMultiplier = Mathf.Max(0.01f, speedMultiplier)
            };
            BehaviorExportContext exportContext = new BehaviorExportContext(
                sourceTimeline,
                exportDirector,
                exportReferenceRoot,
                fallbackSettings);

            // 遍历全部轨道，按轨道实际类型自动分发对应编译器。
            foreach (TrackAsset track in EnumerateTimelineTracks(sourceTimeline))
            {
                if (track == null || track.mutedInHierarchy) continue;

                if (!BehaviorTrackCompilerCatalog.TryExport(track, exportContext))
                    exportContext.AddWarning($"轨道 '{track.name}' ({track.GetType().Name}) 没有注册 Behavior 导出编译器，已跳过。");
            }

            // 提交多态轨道数据，运行时直接由轨道数据创建对应执行器。
            UnityEditor.Undo.RegisterCompleteObjectUndo(target, "Export BehaviorClip");
            exportContext.CommitTo(target);
            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.Selection.activeObject = target;

            for (int i = 0; i < exportContext.Warnings.Count; i++)
                Debug.LogWarning($"[Timeline Export] {exportContext.Warnings[i]}", sourceTimeline);

            Debug.Log(
                $"Timeline 已导出到 BehaviorClip：{target.name}\n" +
                $"Tracks={target.trackData?.Count ?? 0}, Duration={target.playbackSettings?.duration ?? 0f:F2}s",
                target);
        }

        /// <summary>
        /// 解析导出目标 BehaviorClip：优先缓存，否则在输出目录加载或创建。
        /// </summary>
        /// <returns>可写入的 BehaviorClip 资产。</returns>
        private BehaviorClip ResolveTargetBehaviorClip()
        {
            if (targetBehaviorClip != null) return targetBehaviorClip;

            // 在输出目录中复用已有资产，缺失时才创建。
            string folder = EnsureFolder(outputFolder);
            string assetName = SanitizeAssetName(outputAssetName);
            string assetPath = $"{folder}/{assetName}.asset";
            BehaviorClip existing = UnityEditor.AssetDatabase.LoadAssetAtPath<BehaviorClip>(assetPath);
            if (existing != null)
            {
                targetBehaviorClip = existing;
                return existing;
            }

            BehaviorClip created = CreateInstance<BehaviorClip>();
            created.name = assetName;
            UnityEditor.AssetDatabase.CreateAsset(created, assetPath);
            targetBehaviorClip = created;
            return created;
        }

        /// <summary>
        /// 解析导出时的 Reference Root：优先 Reference Root，其次 Animator，最后 Director。
        /// </summary>
        /// <returns>导出参考根节点；未找到时返回 null。</returns>
        private Transform ResolveExportReferenceRoot()
        {
            if (previewReferenceRoot != null) return previewReferenceRoot.transform;
            if (previewReferenceRoot != null) return previewReferenceRoot.transform;

            PlayableDirector director = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            return director != null ? director.transform : null;
        }
    }
}
