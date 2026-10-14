namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 角色侧默认索敌策略（首切片占位，角色未挂 GAS 时的兜底）：射程内最近敌人。
    /// 切片 2 接 GAS 后由角色能力/策略配置参数化替代（差异化索敌=角色能力，挂账口径）。
    /// 只写攻击目标意图；角色移动由玩家指令驱动，策略不碰 MoveDestination。
    /// </summary>
    public sealed class DefaultTargetingStrategy : IUnitIntentStrategy
    {
        public void UpdateIntent(BattleUnitState unit, BattleIntentContext context)
        {
            if (unit.Camp != BattleCamp.Character || !unit.IsAlive) return;

            float rangeSq = unit.Stats.Range * unit.Stats.Range;
            float bestDistSq = float.MaxValue;
            BattleUnitState best = null;

            foreach (BattleUnitState enemy in context.EnumerateEnemies(unit.Camp))
            {
                float distSq = (enemy.Position - unit.Position).sqrMagnitude;
                if (distSq <= rangeSq && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = enemy;
                }
            }

            unit.TargetUnitId = best?.UnitId;
            unit.TargetIsGuardPoint = false;
        }
    }
}
