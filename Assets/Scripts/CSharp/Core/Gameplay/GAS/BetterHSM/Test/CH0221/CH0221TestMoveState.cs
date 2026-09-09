using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>演示 CH0221 在存在移动输入时进入移动状态的测试状态。</summary>
    public sealed class CH0221TestMoveState : StateBase<CH0221TestStateContext>
    {
        /// <summary>
        /// 判断当前实体是否应进入移动状态。
        /// </summary>
        /// <returns>当前存在有效移动输入时返回 true。</returns>
        public override bool Interrupt()
        {
            return Context.HasMoveInput();
        }

        /// <summary>
        /// 输出一次测试状态进入信息。
        /// </summary>
        public override void OnEnter()
        {
            // 仅在状态发生切换时输出，便于确认 HSM 仲裁和转换关系。
            Debug.Log("[BetterHSM Test][CH0221] Enter Move", Context.OwnerContext.Owner);
        }
    }
}
