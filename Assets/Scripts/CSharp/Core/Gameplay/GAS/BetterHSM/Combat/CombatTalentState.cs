namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>
    /// 天赋态（Once 段，2026-11-05）：CH0221 进场播一次 Talent BE clip（clip 上的 EffectSO 注册全局效果）。
    /// 与 Skill/Attack 同属分段状态：播完一轮才放行出边；Talent 播放期间 Attack/Skill/Move 都不能打断。
    /// </summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatTalentState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>天赋 BE 行为键（UnitConfig 行为表中的 clip 资产名）。</summary>
        public const string BehaviorKey = "Talent";

        /// <inheritdoc/>
        protected override string Key => BehaviorKey;

        /// <summary>进入天赋态：播放 Talent clip 并消耗一次性天赋请求。</summary>
        public override void OnEnter()
        {
            base.OnEnter();
            Context.ConsumeTalentRequest();
        }

        /// <summary>创建天赋态（Once 单段）。</summary>
        public CombatTalentState() : base(false) { }
    }
}
