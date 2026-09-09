using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 特效池宿主组件，按固定间隔驱动 VFXPool 清理已随父物体销毁的孤儿实例。
    /// </summary>
    public class VFXPoolHost : MonoBehaviour
    {
        // 孤儿检查间隔（秒），与 VFXPool 的 CheckInterval 保持一致
        private const float CheckInterval = 5f;

        // 距上次检查的累计时间
        private float _elapsed;

        /// <summary>
        /// 每帧累计时间，到达间隔时执行一次孤儿清理。
        /// </summary>
        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < CheckInterval) return;

            _elapsed = 0f;
            VFXPool.Instance.CheckOrphaned();
        }
    }
}