using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>攻击收招状态：Once 段，收招动画播完前不放行转移（严格重走 Start——本状态无直达循环段的边）；
    /// 播完按是否在移动分流回 Move/Idle。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatAttackEndState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；与 UnitConfig 行为列表中的 clip 资产名对齐（Normal 前缀=站姿）。</summary>
        public const string BehaviorKey = "NormalAttackEnd";

        /// <inheritdoc/>
        protected override string Key => BehaviorKey;

        /// <summary>创建收招段（Once）。</summary>
        public CombatAttackEndState() : base(false) { }
    }
}
