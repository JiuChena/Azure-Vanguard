using System.Collections.Generic;

namespace Framework.Gameplay.Battle
{
    /// <summary>战斗结局三态。</summary>
    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat,
    }

    /// <summary>胜负评估输入：评估器所需的战场只读面（由 BattleController 每拍装配）。</summary>
    public sealed class BattleEvaluationContext
    {
        public GuardPointState GuardPoint { get; set; }

        // 全部波次是否已生成完（清波判定的前提）
        public bool AllWavesSpawned { get; set; }

        public IReadOnlyList<BattleUnitState> Units { get; set; }

        // 存活敌人数
        public int AliveEnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Units.Count; i++)
                {
                    if (Units[i].Camp == BattleCamp.Enemy && Units[i].IsAlive) count++;
                }
                return count;
            }
        }
    }

    /// <summary>
    /// 胜负判定策略基类（继承式可重写判定，2026-10-14 拍板）：普通关默认实现 = GuardPointEvaluator，
    /// 挑战关（限时全歼/守点健康度等）继承重写；StageTable.outcome 键经 CombatAssemblyService 选实现。
    /// </summary>
    public abstract class BattleOutcomeEvaluator
    {
        public abstract BattleOutcome Evaluate(BattleEvaluationContext context);
    }

    /// <summary>普通关默认判定：守点血量归零 = 败；全部波生成完且无存活敌人 = 胜。</summary>
    public sealed class GuardPointEvaluator : BattleOutcomeEvaluator
    {
        public override BattleOutcome Evaluate(BattleEvaluationContext context)
        {
            if (context.GuardPoint.IsBroken) return BattleOutcome.Defeat;
            if (context.AllWavesSpawned && context.AliveEnemyCount == 0) return BattleOutcome.Victory;
            return BattleOutcome.Ongoing;
        }
    }
}
