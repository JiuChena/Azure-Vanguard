using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>战斗倒地状态：Once 分段，倒地动画播完前不放行转移（复活边同时 gate 在完成上）；
    /// 进入由 Downed 立即触发（豁免其他状态的完成度 gate），离开经 SegmentFinished && !Downed 回 Idle。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatDeathState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；与 UnitConfig 行为列表中的 clip 资产名对齐。</summary>
        public const string BehaviorKey = "Death";

        /// <inheritdoc/>
        protected override string Key => BehaviorKey;

        /// <summary>创建倒地段（Once）。</summary>
        public CombatDeathState() : base(false) { }
    }
}
