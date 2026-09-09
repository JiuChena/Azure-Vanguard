namespace Core.Gear
{
    /// <summary>
    /// 共享 Canvas 根节点下的 UI 层级挂点。
    /// </summary>
    public enum UILayer
    {
        /// <summary>底层，用于背景与主场景 UI。</summary>
        Bot,
        /// <summary>中层，用于常规功能面板。</summary>
        Mid,
        /// <summary>顶层，用于弹出层与浮层。</summary>
        Top,
        /// <summary>系统层，用于系统级提示与全局覆盖。</summary>
        System,
    }
}
