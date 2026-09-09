using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 自动发现并按 Timeline 轨道类型分发编译器的编辑器目录。
    /// </summary>
    internal static class BehaviorTrackCompilerCatalog
    {
        // 轨道类型 → 自动发现的唯一编译器实例。
        private static readonly Dictionary<Type, IBehaviorTimelineTrackCompiler> compilersByTrackType =
            new Dictionary<Type, IBehaviorTimelineTrackCompiler>();

        // 当前目录是否已经完成 TypeCache 扫描。
        private static bool initialized;

        /// <summary>
        /// 尝试将一个 Timeline 轨道交给其匹配的编译器导出。
        /// </summary>
        /// <param name="track">需要导出的 Timeline 轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        /// <returns>找到并执行编译器时返回 true。</returns>
        public static bool TryExport(TrackAsset track, BehaviorExportContext context)
        {
            if (track == null) return false;

            EnsureInitialized();
            if (!compilersByTrackType.TryGetValue(track.GetType(), out IBehaviorTimelineTrackCompiler compiler))
                return false;

            if (compiler == null || context == null)
                return false;

            compiler.Export(track, context);
            return true;
        }

        /// <summary>
        /// 使用 Unity TypeCache 自动发现全部带声明特性的轨道编译器。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (initialized) return;

            // 仅在编辑器域重载后扫描一次，运行时和导出热路径不使用反射。
            compilersByTrackType.Clear();
            foreach (Type compilerType in TypeCache.GetTypesDerivedFrom<IBehaviorTimelineTrackCompiler>())
            {
                if (compilerType.IsAbstract || compilerType.IsInterface)
                    continue;

                BehaviorTrackCompilerAttribute attribute =
                    (BehaviorTrackCompilerAttribute)Attribute.GetCustomAttribute(compilerType,
                        typeof(BehaviorTrackCompilerAttribute));
                if (attribute == null || attribute.TrackType == null ||
                    !typeof(TrackAsset).IsAssignableFrom(attribute.TrackType))
                {
                    continue;
                }

                IBehaviorTimelineTrackCompiler compiler = (IBehaviorTimelineTrackCompiler)Activator.CreateInstance(compilerType);
                if (compiler.TrackType != attribute.TrackType)
                    throw new InvalidOperationException($"轨道编译器 {compilerType.Name} 的声明类型与实现类型不一致。");
                if (!compilersByTrackType.TryAdd(attribute.TrackType, compiler))
                    throw new InvalidOperationException($"轨道类型 {attribute.TrackType.Name} 注册了多个 Behavior 编译器。");
            }

            initialized = true;
        }
    }
}
