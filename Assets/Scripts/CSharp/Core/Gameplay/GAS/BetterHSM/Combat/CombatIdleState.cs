using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>战斗待机状态：可随时中断（无完成度 gate），进入播 UnitConfig 行为表同名 BE 行为、离开时停止；
    /// 键用站姿细分（Normal 前缀=站姿，Crouch=蹲下），与 clip 资产名对齐。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatIdleState<TContext> : StateBase<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；站姿待机。</summary>
        public const string BehaviorKey = "NormalIdle";

        /// <summary>进入待机：播放同名 BE 行为。</summary>
        public override void OnEnter() => Context.PlayBehavior(BehaviorKey);

        /// <summary>离开待机：停止 BE 行为播放。</summary>
        public override void OnExit() => Context.StopBehavior();
    }
}
