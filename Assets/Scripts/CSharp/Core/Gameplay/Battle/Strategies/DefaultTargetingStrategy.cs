namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 角色侧默认索敌策略（④ 感知能力按阵营调用）：射程内最近敌人。
    /// 差异化索敌=角色能力/策略配置参数化（切片 2 挂账口径）。
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
