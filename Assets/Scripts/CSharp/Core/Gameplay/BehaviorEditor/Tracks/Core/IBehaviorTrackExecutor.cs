namespace BehaviorEditor
{
    /// <summary>
    /// 单类行为轨道数据在一次播放过程中的运行时执行契约。
    /// </summary>
    public interface IBehaviorTrackExecutor
    {
        /// <summary>
        /// 获取当前轨道执行顺序。
        /// </summary>
        /// <returns>数值越小越先执行。</returns>
        int ExecutionOrder { get; }

        /// <summary>
        /// 开始一次新的行为播放。
        /// </summary>
        void Begin();

        /// <summary>
        /// 推进当前行为的轨道时间。
        /// </summary>
        /// <param name="elapsedTime">当前行为已播放时间，单位为秒。</param>
        void Tick(float elapsedTime);

        /// <summary>
        /// 停止行为并释放本轨道的临时状态。
        /// </summary>
        void Stop();
    }
}
