using System;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 将一种 Timeline 轨道转换为运行时行为数据的编辑器契约。
    /// </summary>
    internal interface IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取该编译器可处理的 Timeline 轨道类型。
        /// </summary>
        /// <returns>具体的 TrackAsset 派生类型。</returns>
        Type TrackType { get; }

        /// <summary>
        /// 将匹配类型的轨道导出到行为数据上下文。
        /// </summary>
        /// <param name="track">待导出的已匹配轨道。</param>
        /// <param name="context">当前 Timeline 导出上下文。</param>
        void Export(TrackAsset track, BehaviorExportContext context);
    }
}
