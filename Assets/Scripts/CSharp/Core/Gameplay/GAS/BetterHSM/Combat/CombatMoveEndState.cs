using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>移动收势状态（Move→待机过渡）：Once 段，播完前不放行转移——收势完整再起步/入待机，
    /// 收势期间的新移动意图等收势完成后经 SegmentFinished gate 命中出边生效。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatMoveEndState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；与 UnitConfig 行为列表中的 clip 资产名对齐。</summary>
        public const string BehaviorKey = "MoveEndToNormal";

        /// <inheritdoc/>
        protected override string Key => BehaviorKey;

        /// <summary>创建收势段（Once）。</summary>
        public CombatMoveEndState() : base(false) { }
    }
}
