using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为编辑器主窗口：负责 Timeline 单向导出、作者期预览与会话管理。
    /// </summary>
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {
        // 作者期源 Timeline 资产引用。
        private TimelineAsset sourceTimeline;
        // 目标 BehaviorClip 资产引用；为 null 时在输出目录创建新资产。
        private BehaviorClip targetBehaviorClip;
        // 未指定目标资产时的输出目录。
        private string outputFolder = "Assets/BehaviorEditor";
        // 未指定目标资产时的输出资产名。
        private string outputAssetName = "TimelineBehaviorClip";
        // 回退用的环绕模式（Meta 轨道存在时优先用轨道配置）。
        private WrapMode wrapMode = WrapMode.Once;
        // 回退用的播放速度倍率。
        private float speedMultiplier = 1f;
        // 作者期预览用的 PlayableDirector。
        private PlayableDirector previewDirector;
        // 当前作者期会话上下文，参与者实例状态仅在本次会话中存活。
        private BehaviorAuthoringSessionContext authoringSessionContext;
        // 作者期指定的角色根节点（骨骼路径基准）。
        private GameObject previewReferenceRoot;
        // 结束作者期时是否需要移除自动创建的 Director。
        private bool removePreviewDirectorOnFinish;
        // 是否自动指定了 Reference Root（非手动指定）。
        private bool autoAssignedReferenceRoot;
        // 延迟刷新 Timeline 的挂起标记（编辑器重绘后执行）。
        private static bool pendingDelayedTimelineRefresh;
        // 延迟刷新使用的 Timeline 资产缓存。
        private static TimelineAsset pendingDelayedTimelineAsset;
        // 延迟刷新使用的 Director 缓存。
        private static PlayableDirector pendingDelayedTimelineDirector;

        /// <summary>打开行为编辑器主窗口。</summary>
        [UnityEditor.MenuItem("Tools/Behavior Editor/Timeline Exporter")]
        private static void Open()
        {
            BehaviorEditorWindow window =
                GetWindow<BehaviorEditorWindow>("Behavior Editor Timeline");
            window.minSize = new Vector2(460f, 420f);
        }

        /// <summary>
        /// 绘制窗口主界面：选择源 Timeline 与目标 BehaviorClip，配置导出与作者期参数。
        /// </summary>
        private void OnGUI()
        {
            UnityEditor.EditorGUILayout.LabelField("Timeline -> BehaviorClip", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.HelpBox(
                "轨道由各自编译器独立导出；未注册的 Timeline 轨道会在导出时提示跳过。",
                UnityEditor.MessageType.Info);

            sourceTimeline = (TimelineAsset)UnityEditor.EditorGUILayout.ObjectField(
                "Source Timeline", sourceTimeline, typeof(TimelineAsset), false);
            targetBehaviorClip = (BehaviorClip)UnityEditor.EditorGUILayout.ObjectField(
                "Target BehaviorClip", targetBehaviorClip, typeof(BehaviorClip), false);

            if (targetBehaviorClip == null)
            {
                outputFolder = UnityEditor.EditorGUILayout.TextField("Output Folder", outputFolder);
                outputAssetName = UnityEditor.EditorGUILayout.TextField("Output Asset Name", outputAssetName);
            }

            GUILayout.Space(8f);
            UnityEditor.EditorGUILayout.LabelField("Playback Settings", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.HelpBox(
                "如果 Timeline 中存在 Behavior Meta 轨道并放置了片段，导出时会优先使用轨道里的配置。下面这些字段会作为回退默认值保留。",
                UnityEditor.MessageType.None);
            wrapMode = (WrapMode)UnityEditor.EditorGUILayout.EnumPopup("Wrap Mode", wrapMode);
            speedMultiplier = Mathf.Max(0.01f,
                UnityEditor.EditorGUILayout.FloatField("Speed Multiplier", speedMultiplier));
            GUILayout.Space(8f);
            UnityEditor.EditorGUILayout.LabelField("Authoring", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.HelpBox(
                "Reference Root 是当前行为作者期使用的角色根节点，也是骨骼路径计算基准。开始编辑后会准备 Timeline 会话；各轨道自行处理自己需要的预览绑定。",
                UnityEditor.MessageType.None);
            previewReferenceRoot = (GameObject)UnityEditor.EditorGUILayout.ObjectField(
                "Reference Root", previewReferenceRoot, typeof(GameObject), true);
            GUILayout.Space(10f);
            bool blockAuthoringInPlayMode = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
            if (blockAuthoringInPlayMode)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "Behavior authoring is disabled while Unity is in Play Mode.",
                    UnityEditor.MessageType.Warning);
            }

            using (new UnityEditor.EditorGUI.DisabledScope(sourceTimeline == null || blockAuthoringInPlayMode))
            {
                if (GUILayout.Button("Begin Behavior Authoring", GUILayout.Height(28f)))
                    BeginBehaviorAuthoring();

                if (GUILayout.Button("End Editing And Export", GUILayout.Height(32f)))
                    EndBehaviorAuthoring();
            }
        }

        /// <summary>
        /// 启用时不恢复已结束的作者期会话状态。
        /// </summary>
        private void OnEnable()
        {
        }

        /// <summary>
        /// 停用时清理作者期会话。
        /// </summary>
        private void OnDisable()
        {
            CleanupAuthoringSession();
        }

        /// <summary>
        /// 开始编辑行为
        /// </summary>
        private void BeginBehaviorAuthoring()
        {
            if (sourceTimeline == null) return;
            if (!EnsureEditModeOperationAllowed("Begin Behavior Authoring")) return;

            //编辑目标判空，是则取当前选中场景上的激活对象
            GameObject target = previewReferenceRoot == null
                ? UnityEditor.Selection.activeGameObject
                : previewReferenceRoot;
            if (target == null)
            {
                Debug.LogWarning("没有可用于编辑行为的角色模型。请先在场景中选中一个角色对象，或在 Reference Root 中指定角色根节点。", this);
                return;
            }

            // 新会话开始前先完整清理旧会话，避免参与者状态被覆盖后无法回收。
            if (authoringSessionContext != null)
                CleanupAuthoringSession();

            previewDirector = EnsurePreviewDirector(target, out removePreviewDirectorOnFinish);
            if (previewReferenceRoot == null || autoAssignedReferenceRoot)
            {
                previewReferenceRoot = target;
                autoAssignedReferenceRoot = true;
            }

            BehaviorEditorContext.SetActiveSession(sourceTimeline, previewReferenceRoot);

            previewDirector.playableAsset = sourceTimeline;
            previewDirector.playOnAwake = false;
            previewDirector.time = 0d;
            previewDirector.Stop();
            authoringSessionContext = new BehaviorAuthoringSessionContext(
                sourceTimeline,
                previewDirector,
                previewReferenceRoot);

            OpenTimelineForPreview();
        }

        /// <summary>
        /// 结束行为编辑：导出 BehaviorClip 并清理本次作者期会话。
        /// </summary>
        private void EndBehaviorAuthoring()
        {
            if (sourceTimeline == null) return;
            if (!EnsureEditModeOperationAllowed("End Editing And Export")) return;

            try { ExportToBehaviorClip(); }
            finally { CleanupAuthoringSession(); }
        }

        /// <summary>
        /// 校验当前是否处于允许编辑的时机（非 Play Mode）。
        /// </summary>
        /// <param name="operationName">被阻止的操作名称，用于日志输出。</param>
        /// <returns>允许执行时返回 true。</returns>
        private bool EnsureEditModeOperationAllowed(string operationName)
        {
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return true;

            Debug.LogWarning($"{operationName} is unavailable while Unity is in Play Mode.", this);
            return false;
        }

    }
}
