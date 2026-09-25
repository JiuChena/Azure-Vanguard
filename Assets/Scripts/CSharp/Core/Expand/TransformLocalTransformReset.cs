using UnityEngine;

namespace Core.Expand
{
    /// <summary>
    /// Transform 扩展注入。
    /// Core.Expand 约定：针对 Unity 现有无法修改的类做外部函数注入，每个扩展方法单开一个脚本，
    /// 统一聚合在 <c>{类型名}Expand</c> 的 static partial 类中（本文件 = Transform 的一个扩展方法）。
    /// </summary>
    public static partial class TransformExpand
    {
        /// <summary>
        /// 本地变换归零归一：localPosition 归零、localRotation 归位（identity）、localScale 归一（1,1,1）。
        /// </summary>
        /// <param name="transform">目标 Transform。</param>
        public static void LocalTransformReset(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
