namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 技能目标策略（2026-11-05）：直接复用 SkillEffectTable.target 判断本技能是否需要单目标索敌。
    /// target = single_target → 需要；self / team_all / team_in_range / enemies_in_range → 不需要。
    /// </summary>
    public static class SkillTargetPolicy
    {
        /// <summary>
        /// 三态判定：表已就绪时给出 requires；表/行未就绪返回 false（调用方按“需要目标”保守处理）。
        /// </summary>
        public static bool TryRequiresTarget(int skillId, out bool requires)
        {
            requires = false;
            if (skillId <= 0) return false;
            if (!BattleDataModule.TryGetSkillEffects(skillId, out var effectIds) || effectIds == null) return false;

            bool anyRow = false;
            for (int i = 0; i < effectIds.Count; i++)
            {
                if (!BattleDataModule.TryGetSkillEffect(effectIds[i], out var row) || row == null) continue;
                anyRow = true;
                if (string.Equals(row.Target?.Trim(), "single_target", System.StringComparison.OrdinalIgnoreCase))
                {
                    requires = true;
                    return true;
                }
            }
            return anyRow;
        }

        /// <summary>该技能是否需要预先锁定一个单目标。</summary>
        public static bool RequiresTarget(int skillId)
        {
            return TryRequiresTarget(skillId, out bool requires) && requires;
        }

        /// <summary>施放者当前招式（技能态取 CastingSkillId，否则普攻）是否需要单目标。</summary>
        public static bool RequiresTargetFor(BattleUnitState caster)
        {
            if (caster == null) return false;
            int skillId = caster.CastingSkillId > 0 ? caster.CastingSkillId : caster.Stats.NormalAtkId;
            return RequiresTarget(skillId);
        }
    }
}
