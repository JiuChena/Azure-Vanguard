using System;

namespace BehaviorEditor
{
    /// <summary>
    /// 声明 Timeline 轨道编译器所支持的轨道类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class BehaviorTrackCompilerAttribute : Attribute
    {
        // 编译器可处理的具体 Timeline 轨道类型。
        public Type TrackType { get; }

        /// <summary>
        /// 创建轨道编译器声明特性。
        /// </summary>
        /// <param name="trackType">编译器可处理的 TrackAsset 派生类型。</param>
        public BehaviorTrackCompilerAttribute(Type trackType)
        {
            TrackType = trackType;
        }
    }
}
