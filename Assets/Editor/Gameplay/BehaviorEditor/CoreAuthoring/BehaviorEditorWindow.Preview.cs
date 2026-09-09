using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {

        /// <summary>
        /// 打开 Timeline 预览：绑定 Director、Animator 与音频源并求值一次。
        /// </summary>
        private void OpenTimelineForPreview()
        {
            if (sourceTimeline == null)
                return;

            PlayableDirector resolvedDirector = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            if (resolvedDirector == null)
            {
                Debug.LogWarning(
                    "没有找到可用于预览的 PlayableDirector。请在场景里选择或指定一个挂有 PlayableDirector 的对象后再打开预览。",
                    sourceTimeline);
                return;
            }

            previewDirector = resolvedDirector;
            previewDirector.playableAsset = sourceTimeline;

            // 由自动发现的轨道参与者准备各自需要的预览环境。
            BehaviorAuthoringParticipantCatalog.BeginAuthoring(authoringSessionContext);

            // 重置时间并重建求值。
            previewDirector.time = 0d;
            previewDirector.RebuildGraph();
            previewDirector.Evaluate();
            RefreshTimelineEditor(sourceTimeline, previewDirector);
            UnityEditor.Selection.activeObject = previewDirector.gameObject;
        }

        /// <summary>
        /// 清理本次作者期会话：停止预览、销毁临时创建的对象并恢复上下文。
        /// </summary>
        private void CleanupAuthoringSession()
        {
            TimelineAsset sessionTimeline = authoringSessionContext != null
                ? authoringSessionContext.Timeline
                : sourceTimeline;

            // 先让轨道参与者撤销自身绑定与临时资源，确保 Director 仍然可用。
            if (authoringSessionContext != null)
            {
                BehaviorAuthoringParticipantCatalog.EndAuthoring(authoringSessionContext);
                authoringSessionContext = null;
            }

            // 停止并解绑预览 Director。
            if (previewDirector != null)
            {
                previewDirector.playOnAwake = false;
                previewDirector.Stop();
                previewDirector.time = 0d;
                previewDirector.playableAsset = null;
                previewDirector.RebuildGraph();

                if (removePreviewDirectorOnFinish) UnityEditor.Undo.DestroyObjectImmediate(previewDirector);
            }

            // 清空自动指定的 Reference Root 与预览缓存。
            if (autoAssignedReferenceRoot) previewReferenceRoot = null;

            previewDirector = null;
            removePreviewDirectorOnFinish = false;
            autoAssignedReferenceRoot = false;
            BehaviorEditorContext.ClearActiveSession(sessionTimeline);

            if (sourceTimeline != null) RefreshTimelineEditor(sourceTimeline, null);
        }

        /// <summary>
        /// 刷新 Timeline 编辑器窗口，必要时排队延迟刷新。
        /// </summary>
        /// <param name="timelineAsset">需要刷新的 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的 Director。</param>
        private static void RefreshTimelineEditor(TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            UnityEditor.Timeline.TimelineEditorWindow timelineWindow = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            RestoreTimelineWindowContext(timelineWindow, timelineAsset, preferredDirector);

            // 组合刷新原因（统一按内容增删处理，覆盖全部变更场景）。
            UnityEditor.Timeline.RefreshReason reason =
                UnityEditor.Timeline.RefreshReason.ContentsAddedOrRemoved |
                UnityEditor.Timeline.RefreshReason.SceneNeedsUpdate |
                UnityEditor.Timeline.RefreshReason.WindowNeedsRedraw;

            UnityEditor.Timeline.TimelineEditor.Refresh(reason);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            if (timelineAsset == null)
                return;

            // 缓存延迟刷新参数，避免同一帧重复排队。
            pendingDelayedTimelineAsset = timelineAsset;
            pendingDelayedTimelineDirector = preferredDirector;
            if (pendingDelayedTimelineRefresh)
                return;

            pendingDelayedTimelineRefresh = true;
            UnityEditor.EditorApplication.delayCall += ExecuteDelayedTimelineRefresh;
        }

        /// <summary>
        /// 延迟执行的 Timeline 刷新回调，消费缓存的刷新参数。
        /// </summary>
        private static void ExecuteDelayedTimelineRefresh()
        {
            pendingDelayedTimelineRefresh = false;
            TimelineAsset delayedTimelineAsset = pendingDelayedTimelineAsset;
            PlayableDirector delayedTimelineDirector = pendingDelayedTimelineDirector;
            pendingDelayedTimelineAsset = null;
            pendingDelayedTimelineDirector = null;
            if (delayedTimelineAsset == null)
                return;

            // 恢复窗口上下文并重新刷新。
            UnityEditor.Timeline.TimelineEditorWindow delayedWindow = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            RestoreTimelineWindowContext(
                delayedWindow,
                delayedTimelineAsset,
                delayedTimelineDirector);
            UnityEditor.Timeline.TimelineEditor.Refresh(
                UnityEditor.Timeline.RefreshReason.ContentsAddedOrRemoved |
                UnityEditor.Timeline.RefreshReason.SceneNeedsUpdate |
                UnityEditor.Timeline.RefreshReason.WindowNeedsRedraw);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// 将 Timeline 窗口恢复到目标资产与 Director 的编辑上下文。
        /// </summary>
        /// <param name="timelineWindow">目标 Timeline 窗口。</param>
        /// <param name="timelineAsset">需要显示的 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的 Director。</param>
        private static void RestoreTimelineWindowContext(UnityEditor.Timeline.TimelineEditorWindow timelineWindow, TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            if (timelineWindow == null || timelineAsset == null)
                return;

            // 优先使用解析出的 Director。
            PlayableDirector resolvedDirector = ResolvePreviewDirectorForRefresh(timelineAsset, preferredDirector);
            if (resolvedDirector != null)
            {
                timelineWindow.SetTimeline(resolvedDirector);
                return;
            }

            // 回退到当前检查中的 Director 或资产。
            PlayableDirector inspectedDirector = UnityEditor.Timeline.TimelineEditor.inspectedDirector;
            if (inspectedDirector != null && inspectedDirector.playableAsset == timelineAsset)
            {
                timelineWindow.SetTimeline(inspectedDirector);
                return;
            }

            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset == timelineAsset)
                return;

            // 编辑器尚无上下文时直接设置资产。
            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset == null &&
                UnityEditor.Timeline.TimelineEditor.inspectedDirector == null)
            {
                timelineWindow.SetTimeline(timelineAsset);
            }
        }

        /// <summary>
        /// 解析用于打开预览的 PlayableDirector：优先缓存，其次选中对象，最后场景搜索。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的缓存 Director。</param>
        /// <returns>解析出的 Director；未找到时返回 null。</returns>
        private static PlayableDirector ResolvePreviewDirectorForOpen(TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            // 缓存 Director 可用时直接复用。
            if (preferredDirector != null &&
                (preferredDirector.playableAsset == null || preferredDirector.playableAsset == timelineAsset))
            {
                return preferredDirector;
            }

            // 选中对象上挂有匹配的 Director。
            if (UnityEditor.Selection.activeGameObject != null &&
                UnityEditor.Selection.activeGameObject.TryGetComponent(out PlayableDirector selectedDirector) &&
                selectedDirector.playableAsset == timelineAsset)
            {
                return selectedDirector;
            }

            // 在场景中搜索绑定该资产的 Director。
            PlayableDirector[] directors = UnityEngine.Resources.FindObjectsOfTypeAll<PlayableDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                PlayableDirector director = directors[i];
                if (director == null || !director.gameObject.scene.IsValid())
                    continue;

                if (director.playableAsset == timelineAsset)
                    return director;
            }

            return null;
        }

        /// <summary>
        /// 解析用于刷新预览的 PlayableDirector：优先缓存与选中，再委托打开路径。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的缓存 Director。</param>
        /// <returns>解析出的 Director；未找到时返回 null。</returns>
        private static PlayableDirector ResolvePreviewDirectorForRefresh(TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            if (preferredDirector != null &&
                (preferredDirector.playableAsset == null || preferredDirector.playableAsset == timelineAsset))
            {
                return preferredDirector;
            }

            if (UnityEditor.Selection.activeGameObject != null &&
                UnityEditor.Selection.activeGameObject.TryGetComponent(out PlayableDirector selectedDirector) &&
                selectedDirector.playableAsset == timelineAsset)
            {
                return selectedDirector;
            }

            return ResolvePreviewDirectorForOpen(timelineAsset, null);
        }

        /// <summary>
        /// 确保目标对象上存在可用的 PlayableDirector，缺失时自动补齐。
        /// </summary>
        /// <param name="target">作者期目标对象。</param>
        /// <param name="createdByTool">是否由工具自动创建了 Director。</param>
        /// <returns>可用的 PlayableDirector。</returns>
        private static PlayableDirector EnsurePreviewDirector(GameObject target, out bool createdByTool)
        {
            createdByTool = false;
            if (target == null)
                return null;

            PlayableDirector director = target.GetComponent<PlayableDirector>();
            if (director != null)
            {
                director.playOnAwake = false;
                return director;
            }

            director = UnityEditor.Undo.AddComponent<PlayableDirector>(target);
            director.playOnAwake = false;
            createdByTool = true;
            return director;
        }

    }
}
