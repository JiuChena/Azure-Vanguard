using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>严格导出原生轨道的单位内绑定路径与隐藏混音参数。</summary>
    internal static class NativeTrackBindingExportUtility
    {
        #region 导出
        /// <summary>将 Director 绑定转换为可复用的单位内路径。</summary>
        /// <param name="track">原生轨道。</param>
        /// <param name="context">当前导出环境。</param>
        /// <param name="audio">是否要求 AudioSource 绑定。</param>
        /// <param name="path">成功时为相对路径，空路径表示宿主。</param>
        /// <returns>绑定有效且属于参考根节点时为真。</returns>
        internal static bool TryGetPath(TrackAsset track, BehaviorExportContext context, bool audio, out string path)
        {
            path = null;
            Object binding = context.Director != null ? context.Director.GetGenericBinding(track) : null;
            Transform target = audio ? (binding as AudioSource)?.transform : (binding as GameObject)?.transform;
            Transform root = context.ReferenceRoot;
            if (target == null || root == null || (target != root && !target.IsChildOf(root)))
            {
                context.AddWarning(audio
                    ? $"AudioTrack '{track.name}' 缺少有效的单位内 AudioSource 绑定，回退单位根导出。"
                    : $"{track.GetType().Name} '{track.name}' 缺少有效的单位内 GameObject 绑定，已跳过导出。");
                return false;
            }

            // Transform.Find 无法唯一解析同名兄弟或带斜杠名称，导出时明确拒绝。
            var names = new List<string>();
            for (Transform current = target; current != root; current = current.parent)
            {
                int matches = 0;
                for (int i = 0; i < current.parent.childCount; i++)
                    if (current.parent.GetChild(i).name == current.name) matches++;
                if (matches != 1 || current.name.Contains("/"))
                {
                    context.AddWarning($"轨道 '{track.name}' 的绑定路径包含同名兄弟或斜杠名称 '{current.name}'，请重命名后导出。");
                    return false;
                }
                names.Add(current.name);
            }
            names.Reverse();
            path = string.Join("/", names);
            if (!audio && target == root)
                context.AddWarning($"ActivationTrack '{track.name}' 绑定了单位根节点：关闭它可能停止驱动 BE 的 MonoBehaviour，请确认由外部调度器继续推进并停止行为。");
            if (audio && target.GetComponents<AudioSource>().Length != 1)
            {
                context.AddWarning($"AudioTrack '{track.name}' 的绑定对象有多个 AudioSource，路径无法唯一识别组件，已跳过导出。");
                path = null;
                return false;
            }
            return true;
        }

        /// <summary>读取 Timeline 1.7 的隐藏浮点参数，并在版本不匹配时提示。</summary>
        /// <param name="owner">参数所属资源。</param>
        /// <param name="propertyPath">原生序列化路径。</param>
        /// <param name="fallback">读取失败时的默认值。</param>
        /// <param name="context">警告接收上下文。</param>
        /// <returns>实际参数或默认值。</returns>
        internal static float ReadFloat(Object owner, string propertyPath, float fallback, BehaviorExportContext context)
        {
            using var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property != null) return property.floatValue;
            context.AddWarning($"'{owner.name}' 无法读取 Timeline 参数 {propertyPath}，使用默认值 {fallback}；请检查 Timeline 包版本。");
            return fallback;
        }
        #endregion
    }
}
