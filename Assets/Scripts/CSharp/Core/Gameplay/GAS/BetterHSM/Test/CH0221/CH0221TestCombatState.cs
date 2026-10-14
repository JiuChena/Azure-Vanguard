using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>
    /// 分层演示的战斗分支复合根状态：仅参与激活链的 OnEnter/OnExit（结构节点），
    /// 不接收 OnUpdate，不重写 Interrupt。本状态不启用门控，演示"未启用门控每帧照常评估"。
    /// </summary>
    public sealed class CH0221TestCombatState : StateBase<CH0221TestHierarchicalStateContext>
    {
        /// <summary>
        /// 输出复合状态进入信息。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log("[BetterHSM Test][Hier] Enter Combat", Context.OwnerContext.Owner);
        }

        /// <summary>
        /// 输出复合状态离开信息。
        /// </summary>
        public override void OnExit()
        {
            Debug.Log("[BetterHSM Test][Hier] Exit Combat", Context.OwnerContext.Owner);
        }
    }
}
