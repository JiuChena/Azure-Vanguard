using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 角色侧索敌策略（2026-11-06：仅 AttackStart/Attacking 经 CombatStateContext.AcquireAttackTarget 调用；
    /// 感知层只用 HasCandidate 判断有没有可打候选，不写目标）：按角色表 targetingPolicy 选择目标。
    /// Nearest = 射程内最近；LowestHP = 射程内血量最低（同血量取更近）；
    /// Middle = 最近与最远敌人连线的中点，取离中点最近的候选（“打中间”）。
    /// 只写攻击目标意图；角色移动由玩家指令驱动，策略不碰 MoveDestination。
    /// </summary>
    public sealed class DefaultTargetingStrategy : IUnitIntentStrategy
    {
        /// <summary>射程内是否存在可打候选（感知用；不选具体目标）。</summary>
        public static bool HasCandidate(BattleUnitState unit, BattleIntentContext context)
        {
            if (unit == null || context == null) return false;
            if (unit.Camp != BattleCamp.Character || !unit.IsAlive) return false;
            float rangeSq = unit.Stats.Range * unit.Stats.Range;
            foreach (BattleUnitState enemy in context.EnumerateEnemies(unit.Camp))
            {
                if (enemy == null || enemy.Hp <= 0) continue;
                if ((enemy.Position - unit.Position).sqrMagnitude <= rangeSq) return true;
            }
            return false;
        }

        public void UpdateIntent(BattleUnitState unit, BattleIntentContext context)
        {
            if (unit == null || context == null) return;
            if (unit.Camp != BattleCamp.Character || !unit.IsAlive) return;

            float rangeSq = unit.Stats.Range * unit.Stats.Range;
            var candidates = new List<BattleUnitState>();
            foreach (BattleUnitState enemy in context.EnumerateEnemies(unit.Camp))
            {
                if (enemy == null || enemy.Hp <= 0) continue;
                if ((enemy.Position - unit.Position).sqrMagnitude <= rangeSq) candidates.Add(enemy);
            }

            BattleUnitState best = Select(unit, candidates);
            unit.TargetUnit = best;
            unit.TargetUnitId = best?.UnitId;
            unit.TargetIsGuardPoint = false;
        }

        private static BattleUnitState Select(BattleUnitState unit, List<BattleUnitState> candidates)
        {
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            string policy = unit.Stats.TargetingPolicy;
            if (string.Equals(policy, "LowestHP", StringComparison.OrdinalIgnoreCase))
            {
                return SelectLowestHp(unit, candidates);
            }
            if (string.Equals(policy, "Middle", StringComparison.OrdinalIgnoreCase))
            {
                return SelectMiddle(unit, candidates);
            }
            return SelectNearest(unit, candidates);
        }

        private static BattleUnitState SelectNearest(BattleUnitState unit, List<BattleUnitState> candidates)
        {
            BattleUnitState best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float dist = (candidates[i].Position - unit.Position).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidates[i];
                }
            }
            return best;
        }

        private static BattleUnitState SelectLowestHp(BattleUnitState unit, List<BattleUnitState> candidates)
        {
            BattleUnitState best = null;
            int bestHp = int.MaxValue;
            float bestDist = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                BattleUnitState candidate = candidates[i];
                float dist = (candidate.Position - unit.Position).sqrMagnitude;
                if (candidate.Hp < bestHp || (candidate.Hp == bestHp && dist < bestDist))
                {
                    bestHp = candidate.Hp;
                    bestDist = dist;
                    best = candidate;
                }
            }
            return best;
        }

        private static BattleUnitState SelectMiddle(BattleUnitState unit, List<BattleUnitState> candidates)
        {
            // 先找最近与最远，取两者位置中点；再选离中点最近的候选（同距取更近者）
            BattleUnitState nearest = null;
            BattleUnitState farthest = null;
            float nearestDist = float.MaxValue;
            float farthestDist = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float dist = (candidates[i].Position - unit.Position).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidates[i];
                }
                if (dist > farthestDist)
                {
                    farthestDist = dist;
                    farthest = candidates[i];
                }
            }
            if (nearest == null || farthest == null) return null;

            Vector3 midpoint = (nearest.Position + farthest.Position) * 0.5f;
            BattleUnitState best = null;
            float bestMidDist = float.MaxValue;
            float bestUnitDist = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                BattleUnitState candidate = candidates[i];
                float midDist = (candidate.Position - midpoint).sqrMagnitude;
                float unitDist = (candidate.Position - unit.Position).sqrMagnitude;
                if (midDist < bestMidDist || (Mathf.Approximately(midDist, bestMidDist) && unitDist < bestUnitDist))
                {
                    bestMidDist = midDist;
                    bestUnitDist = unitDist;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
