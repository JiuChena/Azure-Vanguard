using Framework.Gameplay.Abilities;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 为单次行为播放中的轨道执行器提供共享宿主信息。
    /// </summary>
    public sealed class BehaviorExecutionContext
    {
        /// <summary>宿主 GAS 能力上下文（技能方案第 0 步：BHSM 能力创建 data 时注入，效果段直接访问能力）。</summary>
        public AbilityOwnerContext OwnerContext { get; }

        /// <summary>行为宿主对象。</summary>
        public GameObject OwnerGameObject { get; }
        /// <summary>行为宿主变换。</summary>
        public Transform OwnerTransform { get; }
        /// <summary>轨道共享的骨骼路径解析服务。</summary>
        public BehaviorTransformResolver TransformResolver { get; }
        /// <summary>当前行为的全局播放速度倍率。</summary>
        public float PlaybackSpeed { get; }

        /// <summary>
        /// 创建一次播放专用的轨道执行上下文。
        /// </summary>
        /// <param name="ownerGameObject">执行行为的宿主对象；允许为 null。</param>
        /// <param name="playbackSpeed">当前行为全局播放速度倍率；小于等于零时钳制为最小正值。</param>
        /// <param name="ownerContext">宿主 GAS 能力上下文；允许为 null（非 GAS 宿主播放时缺省）。</param>
        public BehaviorExecutionContext(GameObject ownerGameObject, float playbackSpeed,
            AbilityOwnerContext ownerContext = null)
        {
            // 缓存所有轨道都可共享的宿主信息。
            OwnerGameObject = ownerGameObject;
            OwnerTransform = ownerGameObject != null ? ownerGameObject.transform : null;
            PlaybackSpeed = Mathf.Max(0.01f, playbackSpeed);
            TransformResolver = new BehaviorTransformResolver(OwnerTransform, OwnerGameObject);
            OwnerContext = ownerContext;
        }
    }
}
