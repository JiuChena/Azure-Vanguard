using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>战斗攻击状态：零逻辑，仅进入时播放 UnitConfig 行为表中名为 <see cref="BehaviorKey"/> 的 BE 行为、离开时停止。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatAttackState<TContext> : StateBase<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；与 UnitConfig 行为列表中的 clip 资产名对齐。</summary>
        public const string BehaviorKey = "Attack";

        /// <summary>进入攻击：播放同名 BE 行为。</summary>
        public override void OnEnter() => Context.PlayBehavior(BehaviorKey);

        /// <summary>离开攻击：停止 BE 行为播放。</summary>
        public override void OnExit() => Context.StopBehavior();
    }
}
