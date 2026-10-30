using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>攻击起手状态：Once 段，起手动画播完前不放行转移（Downed 立即倒地豁免由图边表达）；
    /// 播完按攻击欲望分流——仍想打进循环段，欲望消失直接收招。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatAttackStartState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>本状态对应的 BE 行为键；与 UnitConfig 行为列表中的 clip 资产名对齐（Normal 前缀=站姿）。</summary>
        public const string BehaviorKey = "NormalAttackStart";

        /// <inheritdoc/>
        protected override string Key => BehaviorKey;

        /// <summary>创建起手段（Once）。</summary>
        public CombatAttackStartState() : base(false) { }
    }
}
