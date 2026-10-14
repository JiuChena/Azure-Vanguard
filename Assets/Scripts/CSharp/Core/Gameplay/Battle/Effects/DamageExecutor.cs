namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 伤害执行器（首切片唯一注册的效果类型）：
    /// 单体伤害，经 BattleMath 双通道公式（atk×rate+atkadd，暴击乘区，减 def，最低 1）。
    /// 目标为守点时按 def=0 结算（守点无防御值）。只扣血，死亡/倒地判定归 BattleController 扫描。
    /// </summary>
    public sealed class DamageExecutor : ISkillEffectExecutor
    {
        public void Execute(in SkillEffectContext context)
        {
            BattleUnitState caster = context.Caster;
            BattleDataModule.NormalAttackData attack = context.Attack;

            if (context.TargetIsGuardPoint)
            {
                if (context.TargetGuard == null) return;
                int guardDamage = BattleMath.CalcDamage(caster.Stats.Atk, attack.Rate, attack.AtkAdd,
                    0, caster.Stats.CritRate, caster.Stats.CritDmg, context.Rng);
                context.TargetGuard.TakeDamage(guardDamage);
                return;
            }

            BattleUnitState target = context.TargetUnit;
            if (target == null || !target.IsAlive) return; // 倒地/已死不结算
            int damage = BattleMath.CalcDamage(caster.Stats.Atk, attack.Rate, attack.AtkAdd,
                target.Stats.Def, caster.Stats.CritRate, caster.Stats.CritDmg, context.Rng);
            target.Hp -= damage;
        }
    }
}
