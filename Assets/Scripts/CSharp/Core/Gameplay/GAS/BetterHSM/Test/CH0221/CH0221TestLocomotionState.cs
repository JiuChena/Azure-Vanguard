using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>
    /// 分层演示的移动分支复合根状态：仅参与激活链的 OnEnter/OnExit（结构节点），
    /// 不接收 OnUpdate；保持默认 Interrupt 实现（复合状态永不成为转换边目标）。
    /// 本状态在注册工厂中启用脏标记门控，演示"父层中断先于叶子评估"。
    /// </summary>
    public sealed class CH0221TestLocomotionState : StateBase<CH0221TestHierarchicalStateContext>
    {
        /// <summary>
        /// 输出复合状态进入信息（激活链自顶向下经过时执行，切换分支不经过本状态）。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log("[BetterHSM Test][Hier] Enter Locomotion", Context.OwnerContext.Owner);
        }

        /// <summary>
        /// 输出复合状态离开信息（激活链自下而上经过时执行，切换分支不经过本状态）。
        /// </summary>
        public override void OnExit()
        {
            Debug.Log("[BetterHSM Test][Hier] Exit Locomotion", Context.OwnerContext.Owner);
        }
    }
}
