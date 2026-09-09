namespace BehaviorEditor
{
    /// <summary>
    /// 声明具体轨道在单次 Timeline 导出结束时如何提交自己的临时数据。
    /// </summary>
    internal interface IBehaviorTrackExportState
    {
        /// <summary>
        /// 将轨道私有收集结果整理并写入对应的运行时轨道数据。
        /// </summary>
        /// <param name="context">当前导出上下文；不得为 null。</param>
        void Commit(BehaviorExportContext context);
    }
}
