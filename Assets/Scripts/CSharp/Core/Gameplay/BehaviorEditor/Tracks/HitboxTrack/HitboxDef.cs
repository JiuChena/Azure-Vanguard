using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为内单个命中判定区域的配置。
    /// </summary>
    [Serializable]
    [MovedFrom("BehaviorCore")]
    public class HitboxDef
    {
        [Tooltip("作者期来源的 Timeline 轨道名。用于保持导出数据的稳定排序。")]
        public string authoringTrackName;

        [Tooltip("用于调试和日志定位的 Hitbox 名称")]
        public string name;

        [Tooltip("Hitbox 开始生效的时间点，单位为秒")]
        [Min(0f)]
        public float startTime;

        [Tooltip("Hitbox 持续生效的时长，单位为秒")]
        [Min(0f)]
        public float duration = 0.1f;

        [Tooltip("Hitbox 的几何形状")]
        public HitboxShape shape = HitboxShape.Box;

        [Tooltip("参照骨骼的层级路径，留空时使用世界空间，不挂到任何宿主骨骼下")]
        public string referenceBone;

        [Tooltip("相对参照骨骼的局部位置偏移，单位为米")]
        public Vector3 positionOffset;

        [Tooltip("相对参照骨骼的局部旋转偏移，单位为度")]
        public Vector3 rotationOffset;

        [Tooltip("Hitbox 的局部缩放倍率")]
        public Vector3 scaleOffset = Vector3.one;

        [Tooltip("Hitbox 尺寸。Box 为长宽高，Sphere 使用 X 作为半径，Capsule 使用 X 为半径、Y 为高度")]
        public Vector3 size = Vector3.one;

        [Tooltip("命中检测完成后执行的配置资产；为空时仅执行范围检测，不产生玩法结果")]
        public HitExecuteSO execute;
    }
}
