using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为事件触发时提供给项目侧执行配置的通用场景上下文。
    /// </summary>
    public sealed class BehaviorEventContext
    {
        /// <summary>执行行为的宿主对象。</summary>
        public GameObject OwnerGameObject { get; internal set; }
        /// <summary>执行行为的宿主变换。</summary>
        public Transform OwnerTransform { get; internal set; }
        /// <summary>事件解析出的骨骼变换；世界空间事件时为 null。</summary>
        public Transform ReferenceTransform { get; internal set; }
        /// <summary>作者配置的骨骼路径；空字符串表示世界空间事件。</summary>
        public string ReferenceBonePath { get; internal set; }
        /// <summary>事件解析后的世界位置。</summary>
        public Vector3 Position { get; internal set; }
        /// <summary>事件解析后的世界旋转。</summary>
        public Quaternion Rotation { get; internal set; }
        /// <summary>事件配置的局部缩放倍率。</summary>
        public Vector3 Scale { get; internal set; }
        /// <summary>事件在行为时间轴中的触发时间，单位为秒。</summary>
        public float TriggerTime { get; internal set; }
        /// <summary>事件实际执行时行为已播放的时间，单位为秒。</summary>
        public float ElapsedTime { get; internal set; }
    }
}
