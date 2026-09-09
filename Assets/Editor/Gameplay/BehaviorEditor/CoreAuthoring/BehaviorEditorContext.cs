using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 保存当前作者期会话可共享的 Timeline 与 Reference Root。
    /// </summary>
    internal static class BehaviorEditorContext
    {
        // 当前作者期会话的 Timeline，用于校验全局 Timeline 窗口上下文。
        private static TimelineAsset activeTimeline;
        // 当前作者期指定的角色根节点，用于共享骨骼路径解析。
        private static GameObject referenceRootObject;

        /// <summary>
        /// 设置当前作者期会话的 Timeline 与角色根节点。
        /// </summary>
        /// <param name="timeline">当前作者期编辑的 Timeline 资产；不得为 null。</param>
        /// <param name="referenceRoot">角色根节点；允许为 null。</param>
        public static void SetActiveSession(TimelineAsset timeline, GameObject referenceRoot)
        {
            activeTimeline = timeline;
            referenceRootObject = referenceRoot;
        }

        /// <summary>
        /// 清理指定 Timeline 所属作者期会话的共享状态。
        /// </summary>
        /// <param name="timeline">需要关闭的 Timeline 资产；仅匹配当前会话时清理。</param>
        public static void ClearActiveSession(TimelineAsset timeline)
        {
            if (activeTimeline != timeline)
                return;

            activeTimeline = null;
            referenceRootObject = null;
        }

        /// <summary>
        /// 获取当前 Timeline 窗口对应会话的角色根节点。
        /// </summary>
        /// <param name="referenceRoot">输出当前会话的角色根节点 Transform。</param>
        /// <returns>当前 inspected Timeline 与作者期会话匹配且根节点有效时返回 true。</returns>
        public static bool TryGetReferenceRootForInspectedTimeline(out Transform referenceRoot)
        {
            referenceRoot = null;
            if (activeTimeline == null || referenceRootObject == null)
                return false;

            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset != activeTimeline)
                return false;

            referenceRoot = referenceRootObject.transform;
            return referenceRoot != null;
        }
    }
}
