using Framework.Gameplay.Abilities;

namespace Framework.Gameplay.Battle
{
    /// <summary>技能效果/条件执行上下文：施放者 + 战场 + 施放者 GAS（条件判断/效果执行共享，实时读战场不缓存快照）。</summary>
    public sealed class SkillEffectContext
    {
        /// <summary>施放者单位（效果目标/数值来源）。</summary>
        public BattleUnitState Caster;

        /// <summary>战场控制器（目标解析/单位查找）。</summary>
        public BattleController Controller;

        /// <summary>施放者 GAS（buff 应用等能力访问；ExecuteContext.OwnerContext）。</summary>
        public AbilityOwnerContext OwnerContext;
    }
}
