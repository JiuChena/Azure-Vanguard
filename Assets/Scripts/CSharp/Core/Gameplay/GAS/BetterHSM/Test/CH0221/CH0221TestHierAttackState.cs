using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>
    /// 分层演示的攻击叶子状态（combat 分支）：进入即消费战斗请求，避免返回 locomotion
    /// 分支后攻击边立刻再次命中；按输入经返回边回到 idle/move。
    /// </summary>
    public sealed class CH0221TestHierAttackState : StateBase<CH0221TestHierarchicalStateContext>
    {
        // 本状态对应的 BE 行为资产键。
        public const string BehaviorKey = "Attack";

        /// <summary>
        /// 输出一次进入信息、消费战斗请求并播放对应 BE 行为。
        /// </summary>
        public override void OnEnter()
        {
            // 先消费请求再播表现：返回边读取的是输入条件，与请求标志无关，但保持数据整洁。
            Context.ClearCombatRequest();
            Debug.Log("[BetterHSM Test][Hier] Enter Attack", Context.OwnerContext.Owner);
            Context.PlayBehavior(BehaviorKey);
        }

        /// <summary>
        /// 离开状态时停止 BE 行为播放。
        /// </summary>
        public override void OnExit()
        {
            Context.StopBehavior();
        }
    }
}
