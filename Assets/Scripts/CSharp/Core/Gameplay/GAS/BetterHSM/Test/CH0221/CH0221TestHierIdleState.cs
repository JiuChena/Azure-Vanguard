using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Test
{
    /// <summary>
    /// 分层演示的待机叶子状态：每 2 秒对父层 MarkDirty 一次，演示"父层边命中已激活叶为无操作"
    /// （预期无 Enter/Exit 日志、待机表现不重启）。
    /// </summary>
    public sealed class CH0221TestHierIdleState : StateBase<CH0221TestHierarchicalStateContext>
    {
        // 本状态对应的 BE 行为资产键。
        public const string BehaviorKey = "Idle";

        // 父层 MarkDirty 的间隔（秒）：模拟业务数据定期变更（如增益节拍）。
        private const float MarkDirtyIntervalSeconds = 2f;

        // 待机累计时长。
        private float _idleSeconds;

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
        /// 判断当前实体是否应进入待机状态（同时被父层边与叶子边复用为进入条件）。
        /// </summary>
        /// <returns>当前没有有效移动输入时返回 true。</returns>
        public override bool Interrupt()
        {
            return !Context.HasMoveInput();
        }

        /// <summary>
        /// 输出一次进入信息并播放对应 BE 行为，重置计时。
        /// </summary>
        public override void OnEnter()
        {
            // 仅在状态发生切换时输出；父层命中已激活叶（无操作）不应出现本日志。
            Debug.Log("[BetterHSM Test][Hier] Enter Idle", Context.OwnerContext.Owner);
            Context.PlayBehavior(BehaviorKey);
            _idleSeconds = 0f;
        }

        /// <summary>
        /// 累计待机时长并周期性 MarkDirty 父层（预期父层评估后命中已激活叶：无任何生命周期日志）。
        /// </summary>
        public override void OnUpdate()
        {
            _idleSeconds += Time.deltaTime;
            if (_idleSeconds < MarkDirtyIntervalSeconds) return;

            _idleSeconds = 0f;
            Debug.Log("[BetterHSM Test][Hier] 待机节拍 MarkDirty（预期无 Enter/Exit 日志）", Context.OwnerContext.Owner);
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
