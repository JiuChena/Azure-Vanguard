namespace BehaviorEditor
{
    /// <summary>
    /// 声明轨道可向行为总调度器提供 Scene Gizmo 绘制。
    /// </summary>
    public interface IBehaviorTrackGizmoDrawer
    {
        /// <summary>
        /// 绘制当前轨道的运行时调试图形。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        void DrawGizmos(float elapsedTime);
    }
}
