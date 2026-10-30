using Core.Gear;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>技能状态（Once 段，2026-10-27 用户口径——按行为表现有 clip 分配状态）：
    /// 行为表现有技能 clip 即 <c>Skill</c>（E 小技能）与 <c>EX</c>（Q 大招）单段资产，
    /// 本状态按感知层待放槽位播对应 clip；播完回行动态。进入时施放结算（重设 CD / Q 消耗满能量）。</summary>
    /// <typeparam name="TContext">所属单位的战斗状态上下文类型。</typeparam>
    public class CombatSkillState<TContext> : CombatSegmentState<TContext> where TContext : CombatStateContext
    {
        /// <summary>E 小技能 BE 行为键（与 UnitConfig 行为列表中的 clip 资产名对齐：BehaviorClips/Skill.asset）。</summary>
        public const string BehaviorKeyE = "Skill";

        /// <summary>Q 大招（EX）BE 行为键（BehaviorClips/EX.asset）。</summary>
        public const string BehaviorKeyQ = "EX";

        /// <inheritdoc/>
        protected override string Key =>
            Context.PendingSkillSlot == SkillSlot.Q ? BehaviorKeyQ : BehaviorKeyE;

        /// <summary>进入技能态：播放对应技能 clip + 施放结算（重设 CD / Q 消耗满能量 / 锁定 CastingSkillId）。</summary>
        public override void OnEnter()
        {
            base.OnEnter();
            Context.StartSkillCast(Context.PendingSkillSlot);
        }

        /// <summary>离开技能态：清除释放锁定（效果执行器只在该技能播放窗口读 CastingSkillId）。</summary>
        public override void OnExit()
        {
            base.OnExit();
            Context.StopCastingSkill();
        }

        /// <summary>创建技能态（Once 单段）。</summary>
        public CombatSkillState() : base(false) { }
    }
}
