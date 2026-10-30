using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>
    /// 攻击转向纯函数：yaw-delta 本地增量法——每帧把面向节点绕自身 Y 轴转一步，
    /// 步长按固定转速钳制（度/秒 × 帧时），收敛到目标水平朝向（LookAt 语义的水平分量）。
    /// 本地增量（face.localRotation 累乘）而非设绝对角：子节点（炮塔/头部）可独立于根转向、
    /// 保留预制体上的挂载基角，root 与子节点同法适用。仰角不参与（战斗单位竖直站桩）。
    /// </summary>
    public static class BattleFacing
    {
        /// <summary>
        /// 把转向节点向目标方向步进一步（本帧增量）。
        /// </summary>
        /// <param name="face">转向节点（root 或子节点）；调用方保证非空。</param>
        /// <param name="target">目标世界坐标。</param>
        /// <param name="speed">固定转速，单位：度/秒。</param>
        /// <param name="deltaTime">当前帧时长，单位：秒。</param>
        public static void StepYawDelta(Transform face, Vector3 target, float speed, float deltaTime)
        {
            Vector3 forward = face.forward;
            Vector3 to = target - face.position;
            to.y = 0f;

            // 目标与自身水平同向（或高度齐平）时无需转向。
            if (to.sqrMagnitude < 0.0001f) return;

            // heading/desired 均取 atan2(x, z) 标准：0 = +Z，逆时针为正。
            float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(heading, desired);
            // 固定转速钳制：步长不超过 speed×dt，到达前即逐帧逼近、不越过（收敛无抖动）。
            float step = Mathf.Clamp(delta, -speed * deltaTime, speed * deltaTime);

            // 局部 Y 增量（DeltaAngle 带符号决定左右转方向）；同轴旋转与父级其他旋转可交换，世界 yaw 增量 = 本地 yaw 增量。
            face.localRotation = face.localRotation * Quaternion.Euler(0f, step, 0f);
        }
    }
}
