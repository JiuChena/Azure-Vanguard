using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>
    /// 分层演示的移动叶子状态：累计移动满 3 秒写入战斗请求并 MarkDirty 父层，
    /// 演示"父层中断直接切到跨分支子状态"（locomotion 层攻击边以高优先级跨树命中 combat/attack）。
    /// </summary>
    public sealed class CH0221TestHierMoveState : StateBase<CH0221TestHierarchicalStateContext>
    {
        // 本状态对应的 BE 行为资产键。
        public const string BehaviorKey = "Move";

        // 移动累计满该时长后发起战斗请求（秒）：模拟"条件在移动中才成立"的业务数据变更。
        private const float CombatRequestSeconds = 3f;

        // 移动累计时长。
        private float _moveSeconds;

        // 父复合状态引用：数据变更处 MarkDirty 需要标到对应层（注册工厂注入）。
        private CH0221TestLocomotionState _locomotionState;

        /// <summary>
        /// 注入父复合状态引用（注册工厂在构建前调用一次）。
        /// </summary>
        /// <param name="locomotionState">本状态的父复合状态实例。</param>
        internal void SetLocomotionState(CH0221TestLocomotionState locomotionState)
        {
            _locomotionState = locomotionState;
        }

        /// <summary>
        /// 判断当前实体是否应进入移动状态。
        /// </summary>
        /// <returns>当前存在有效移动输入时返回 true。</returns>
        public override bool Interrupt()
        {
            return Context.HasMoveInput();
        }

        /// <summary>
        /// 输出一次进入信息并播放对应 BE 行为，重置计时。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log("[BetterHSM Test][Hier] Enter Move", Context.OwnerContext.Owner);
            Context.PlayBehavior(BehaviorKey);
            _moveSeconds = 0f;
        }

        /// <summary>
        /// 累计移动时长；满阈值处发生业务数据变更（战斗请求）并同时 MarkDirty 父层——
        /// 事件式门控的业务接线示范：数据变更与标记同处发生，下帧父层评估命中攻击边。
        /// </summary>
        public override void OnUpdate()
        {
            _moveSeconds += Time.deltaTime;
            if (_moveSeconds < CombatRequestSeconds) return;

            _moveSeconds = 0f;
            Debug.Log("[BetterHSM Test][Hier] 移动满 3 秒：RequestCombat + MarkDirty（预期下帧跨树进入 Attack）", Context.OwnerContext.Owner);
            Context.RequestCombat();
            if (_locomotionState != null) _locomotionState.MarkDirty();
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
