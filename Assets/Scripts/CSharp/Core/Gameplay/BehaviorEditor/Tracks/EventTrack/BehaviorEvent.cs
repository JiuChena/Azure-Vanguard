using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// 行为时间轴上的单个运行时事件。
    /// </summary>
    [Serializable]
    [MovedFrom("BehaviorCore")]
    public class BehaviorEvent
    {
        [Tooltip("作者期来源的 Timeline 轨道名。用于保持同一时间点事件的稳定导出顺序。")]
        public string authoringTrackName;

        [Tooltip("事件触发时间，单位为秒，基于行为起点计算")]
        public float time;

        [Tooltip("参照骨骼的层级路径；留空时位置和旋转偏移使用世界空间。")]
        public string referenceBone;

        [Tooltip("相对参照骨骼的局部位置偏移，单位为米")]
        public Vector3 positionOffset;

        [Tooltip("相对参照骨骼的局部旋转偏移，单位为度")]
        public Vector3 rotationOffset;

        [Tooltip("传递给执行配置的局部缩放倍率。")]
        public Vector3 scaleOffset = Vector3.one;
        
        [Space(5), Header("Execute"), Tooltip("事件触发时调用的项目侧执行配置。具体业务逻辑由其子类实现。")]
        public BehaviorEventExecuteSO execute;
    }
}
