#if UNITY_EDITOR
using System.Text;

namespace BehaviorEditor
{
    /// <summary>
    /// 提供 BehaviorEditor 新轨道的五份源码模板。
    /// </summary>
    internal static class BehaviorTrackScriptTemplates
    {
        /// <summary>
        /// 构建 Timeline 轨道声明脚本。
        /// </summary>
        /// <param name="trackName">轨道类型名称。</param>
        /// <returns>轨道声明源码。</returns>
        public static string BuildTrack(string trackName)
        {
            StringBuilder source = CreateHeader();
            source.AppendLine("using UnityEngine.Timeline;");
            source.AppendLine();
            source.AppendLine("namespace BehaviorEditor");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// {trackName} 行为轨道。");
            source.AppendLine("    /// </summary>");
            source.AppendLine("    [TrackColor(0.35f, 0.65f, 0.95f)]");
            source.AppendLine($"    [TrackClipType(typeof(BehaviorTimeline{trackName}ClipAsset))]");
            source.AppendLine($"    public sealed class BehaviorTimeline{trackName}Track : TrackAsset");
            source.AppendLine("    {");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        /// <summary>
        /// 构建 Timeline 片段资产脚本。
        /// </summary>
        /// <param name="trackName">轨道类型名称。</param>
        /// <returns>片段资产源码。</returns>
        public static string BuildClipAsset(string trackName)
        {
            StringBuilder source = CreateHeader();
            source.AppendLine("using UnityEngine;");
            source.AppendLine("using UnityEngine.Playables;");
            source.AppendLine("using UnityEngine.Timeline;");
            source.AppendLine();
            source.AppendLine("namespace BehaviorEditor");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// {trackName} Timeline 片段资产。");
            source.AppendLine("    /// </summary>");
            source.AppendLine($"    public sealed class BehaviorTimeline{trackName}ClipAsset : PlayableAsset, ITimelineClipAsset");
            source.AppendLine("    {");
            source.AppendLine("        // 当前片段导出的运行时参数。");
            source.AppendLine("        [Tooltip(\"当前片段导出的运行时参数。\")]");
            source.AppendLine("        public float value;");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine("        /// 获取当前片段支持的 Timeline 编辑能力。");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        public ClipCaps clipCaps => ClipCaps.ClipIn;");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine("        /// 创建作者期占位 Playable。");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        /// <param name=\"graph\">当前 Timeline 播放图。</param>");
            source.AppendLine("        /// <param name=\"owner\">当前 Timeline 宿主对象。</param>");
            source.AppendLine("        /// <returns>BehaviorEditor 使用的空 Playable。</returns>");
            source.AppendLine("        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)");
            source.AppendLine("        {");
            source.AppendLine("            return ScriptPlayable<BehaviorTimelineNullPlayableBehaviour>.Create(graph);");
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        /// <summary>
        /// 构建运行时轨道数据脚本。
        /// </summary>
        /// <param name="trackName">轨道类型名称。</param>
        /// <returns>运行时轨道数据源码。</returns>
        public static string BuildTrackData(string trackName)
        {
            StringBuilder source = CreateHeader();
            source.AppendLine("using System;");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using UnityEngine;");
            source.AppendLine();
            source.AppendLine("namespace BehaviorEditor");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// 保存导出后的 {trackName} 运行时数据。");
            source.AppendLine("    /// </summary>");
            source.AppendLine("    [Serializable]");
            source.AppendLine($"    public sealed class {trackName}TrackData : BehaviorTrackData");
            source.AppendLine("    {");
            source.AppendLine("        // 当前轨道导出的片段数据集合。");
            source.AppendLine("        [Tooltip(\"按时间窗保存的运行时片段数据。\")]");
            source.AppendLine($"        public List<{trackName}TrackSegment> segments = new List<{trackName}TrackSegment>();");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine($"        /// 创建 {trackName} 轨道默认数据。");
            source.AppendLine("        /// </summary>");
            source.AppendLine($"        public {trackName}TrackData()");
            source.AppendLine("        {");
            source.AppendLine("            executionOrder = 0;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine("        /// 获取轨道诊断名称。");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        /// <returns>当前轨道的显示名称。</returns>");
            source.AppendLine($"        public override string DisplayName => \"{trackName}\";");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine("        /// 创建当前轨道的播放执行器。");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        /// <param name=\"context\">当前行为播放上下文。</param>");
            source.AppendLine("        /// <returns>当前轨道的运行时执行器。</returns>");
            source.AppendLine("        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)");
            source.AppendLine("        {");
            source.AppendLine($"            return new {trackName}TrackExecutor(this, context);");
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// 保存一个 {trackName} 运行时片段。");
            source.AppendLine("    /// </summary>");
            source.AppendLine("    [Serializable]");
            source.AppendLine($"    public sealed class {trackName}TrackSegment");
            source.AppendLine("    {");
            source.AppendLine("        // 片段开始时间，单位：秒。");
            source.AppendLine("        [Tooltip(\"片段开始时间，单位：秒。\")]");
            source.AppendLine("        public float startTime;");
            source.AppendLine("        // 片段持续时间，单位：秒。");
            source.AppendLine("        [Tooltip(\"片段持续时间，单位：秒。\")]");
            source.AppendLine("        public float duration;");
            source.AppendLine("        // 由 Timeline 片段导出的自定义参数。");
            source.AppendLine("        [Tooltip(\"由 Timeline 片段导出的自定义参数。\")]");
            source.AppendLine("        public float value;");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        /// <summary>
        /// 构建运行时轨道执行器脚本。
        /// </summary>
        /// <param name="trackName">轨道类型名称。</param>
        /// <returns>运行时轨道执行器源码。</returns>
        public static string BuildTrackExecutor(string trackName)
        {
            StringBuilder source = CreateHeader();
            source.AppendLine("using UnityEngine;");
            source.AppendLine();
            source.AppendLine("namespace BehaviorEditor");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// 驱动 {trackName} 片段播放的运行时执行器。");
            source.AppendLine("    /// </summary>");
            source.AppendLine($"    internal sealed class {trackName}TrackExecutor : IBehaviorTrackExecutor");
            source.AppendLine("    {");
            source.AppendLine("        // 当前轨道的静态运行时数据。");
            source.AppendLine($"        private readonly {trackName}TrackData data;");
            source.AppendLine("        // 当前行为播放上下文。");
            source.AppendLine("        private readonly BehaviorExecutionContext context;");
            source.AppendLine();
            source.AppendLine("        /// <summary>获取轨道执行顺序。</summary>");
            source.AppendLine("        public int ExecutionOrder => data.executionOrder;");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine($"        /// 创建 {trackName} 运行时执行器。");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        /// <param name=\"data\">当前轨道运行时数据。</param>");
            source.AppendLine("        /// <param name=\"context\">当前行为播放上下文。</param>");
            source.AppendLine($"        public {trackName}TrackExecutor({trackName}TrackData data, BehaviorExecutionContext context)");
            source.AppendLine("        {");
            source.AppendLine("            this.data = data;");
            source.AppendLine("            this.context = context;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        /// <summary>开始一次新的轨道播放。</summary>");
            source.AppendLine("        public void Begin()");
            source.AppendLine("        {");
            source.AppendLine("            // 在这里缓存本轨道需要的宿主组件或重置运行时状态。");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine("        /// 推进当前轨道并执行处于时间窗内的片段。");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        /// <param name=\"elapsedTime\">当前行为已播放时间，单位：秒。</param>");
            source.AppendLine("        public void Tick(float elapsedTime)");
            source.AppendLine("        {");
            source.AppendLine("            if (data == null || data.segments == null || context == null) return;");
            source.AppendLine();
            source.AppendLine("            // 遍历当前时间命中的片段，在这里填入该轨道的实际执行逻辑。");
            source.AppendLine("            for (int index = 0; index < data.segments.Count; index++)");
            source.AppendLine("            {");
            source.AppendLine($"                {trackName}TrackSegment segment = data.segments[index];");
            source.AppendLine("                if (segment == null || elapsedTime < segment.startTime ||");
            source.AppendLine("                    elapsedTime > segment.startTime + segment.duration) continue;");
            source.AppendLine("            }");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        /// <summary>停止轨道播放并清理临时状态。</summary>");
            source.AppendLine("        public void Stop()");
            source.AppendLine("        {");
            source.AppendLine("            // 在这里清理本轨道创建或缓存的运行时状态。");
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        /// <summary>
        /// 构建 Timeline 轨道编译器脚本。
        /// </summary>
        /// <param name="trackName">轨道类型名称。</param>
        /// <returns>轨道编译器源码。</returns>
        public static string BuildTrackCompiler(string trackName)
        {
            StringBuilder source = CreateHeader();
            source.AppendLine("using UnityEngine;");
            source.AppendLine("using UnityEngine.Timeline;");
            source.AppendLine();
            source.AppendLine("namespace BehaviorEditor");
            source.AppendLine("{");
            source.AppendLine("    /// <summary>");
            source.AppendLine($"    /// 将 {trackName} Timeline 轨道导出为运行时数据。");
            source.AppendLine("    /// </summary>");
            source.AppendLine($"    [BehaviorTrackCompiler(typeof(BehaviorTimeline{trackName}Track))]");
            source.AppendLine($"    internal sealed class {trackName}TimelineTrackCompiler : IBehaviorTimelineTrackCompiler");
            source.AppendLine("    {");
            source.AppendLine("        /// <summary>获取当前编译器支持的 Timeline 轨道类型。</summary>");
            source.AppendLine($"        public System.Type TrackType => typeof(BehaviorTimeline{trackName}Track);");
            source.AppendLine();
            source.AppendLine("        /// <summary>导出当前轨道的全部有效片段。</summary>");
            source.AppendLine("        /// <param name=\"track\">待导出的 Timeline 轨道。</param>");
            source.AppendLine("        /// <param name=\"context\">当前 Timeline 导出上下文。</param>");
            source.AppendLine("        public void Export(TrackAsset track, BehaviorExportContext context)");
            source.AppendLine("        {");
            source.AppendLine($"            if (track is not BehaviorTimeline{trackName}Track sourceTrack || context == null) return;");
            source.AppendLine();
            source.AppendLine($"            {trackName}TrackData data = context.GetOrCreateTrackData<{trackName}TrackData>();");
            source.AppendLine("            foreach (TimelineClip clip in sourceTrack.GetClips())");
            source.AppendLine("            {");
            source.AppendLine($"                if (clip?.asset is not BehaviorTimeline{trackName}ClipAsset asset) continue;");
            source.AppendLine();
            source.AppendLine("                // Timeline 时间信息由 Clip 提供，运行时数据只保存导出结果。");
            source.AppendLine("                context.ConsiderEndTime(clip.end);");
            source.AppendLine($"                data.segments.Add(new {trackName}TrackSegment");
            source.AppendLine("                {");
            source.AppendLine("                    startTime = Mathf.Max(0f, (float)clip.start),");
            source.AppendLine("                    duration = Mathf.Max(0f, (float)clip.duration),");
            source.AppendLine("                    value = asset.value");
            source.AppendLine("                });");
            source.AppendLine("            }");
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        /// <summary>
        /// 创建带有统一生成说明的源码构建器。
        /// </summary>
        /// <returns>已写入生成说明的源码构建器。</returns>
        private static StringBuilder CreateHeader()
        {
            StringBuilder source = new StringBuilder();
            source.AppendLine("// 此文件由 BehaviorEditor 新轨道脚本工具生成，可按轨道需求修改。");
            source.AppendLine();
            return source;
        }
    }
}
#endif
