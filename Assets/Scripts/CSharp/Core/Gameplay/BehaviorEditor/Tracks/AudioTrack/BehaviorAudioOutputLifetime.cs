using UnityEngine;
using UnityEngine.Playables;

namespace BehaviorEditor
{
    /// <summary>在音频输出被宿主连带销毁时兜底释放原生播放图。</summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    public sealed class BehaviorAudioOutputLifetime : MonoBehaviour
    {
        // 仅属于当前运行时输出的图，不参与序列化。
        internal PlayableGraph Graph;

        #region 清理
        /// <summary>兼容宿主销毁后不再调用行为 Tick 或 Stop 的情况。</summary>
        private void OnDestroy()
        {
            if (Graph.IsValid()) Graph.Destroy();
        }
        #endregion
    }
}
