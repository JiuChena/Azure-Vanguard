using System.Collections.Generic;
using Framework.Gameplay.Battle;
using UnityEngine;

namespace Framework.Gameplay.Abilities.BetterHSM.Combat
{
    /// <summary>
    /// 敌人意图策略（无状态，可全体共享，④ 感知能力按阵营调用）：路径阻挡语义——向目标点机动途中，
    /// 仅当有角色挡在去路上（位于「当前位置→移动目的地」线段上、垂距 ≤ 阻挡半径 📐0.8 占位）才停下
    /// 攻击最近阻挡者；不在去路上的角色无视、继续走（索敌范围（range 列）外的远处角色不算阻挡）；守点进索敌范围
    /// 则改打守点；都无则路线制机动——沿配给路点（unit.Route）依次推进，严格走点、无视障碍、聚堆为
    /// 设计预期；停走距离 = 索敌范围（range 列）+ 每单位随机偏移（unit.StopRange），同种敌人停在略有差异的半径、聚堆点错开；
    /// 路点进度由 unit.RouteIndex 持有（抵达即前移、单调不减），途中停走/目标死后从当前
    /// 路点继续、走完转守点。
    /// 进度放单位而非策略：策略本体保持无状态可共享，且"抵达即前移"避免走出路点
    /// 0.2 判定圈后被无状态重扫判回"未抵达"而开倒车。
    /// 只写 BattleUnitState 意图字段，决策标志镜像由感知能力完成。
    /// </summary>
    public sealed class EnemyMeleeIntentStrategy : IUnitIntentStrategy
    {
        // 路点抵达判定（平方距离；与 NavMeshAgent.stoppingDistance / 感知 MoveArriveDistance 同口径 0.2）
        private const float WaypointArriveDistSq = 0.2f * 0.2f;

        // 阻挡判定半径默认值（米）：EnemyTable.blockRadius 列 ≤0 或未配时的兜底
        private const float DefaultBlockRadius = 0.8f;

        /// <summary>
        /// 生产敌人意图：目标优先级为去路上最近阻挡角色 → 无目标（路线制机动；走完/无路线直奔守点，
        /// 进守点由 ③ Step 回收扣血——2026-10-27 守点交互重做，不再攻击守点）。
        /// </summary>
        /// <param name="unit">当前敌人单位。</param>
        /// <param name="context">意图查询面（索敌候选/守点判定）。</param>
        public void UpdateIntent(BattleUnitState unit, BattleIntentContext context)
        {
            if (unit.Camp != BattleCamp.Enemy || !unit.IsAlive) return;

            // 路径阻挡优先：目的地≈自身（出生首拍/原地驻守，线段零长）时跳过阻挡判定，避免同点叠单位误触发
            Vector3 destination = unit.MoveDestination;
            if ((destination - unit.Position).sqrMagnitude > WaypointArriveDistSq)
            {
                BattleUnitState blocker = FindPathBlocker(unit, destination, context);
                if (blocker != null)
                {
                    unit.TargetUnit = blocker;
                    unit.TargetUnitId = blocker.UnitId;
                    unit.TargetIsGuardPoint = false;
                    return;
                }
            }

            // 无阻挡：不再攻击守点（2026-10-27 守点交互重做——走进守点由 ③ Step 回收扣血），直接走无目标分支

            // 无任何攻击目标：清攻击意图，路线制机动——从 RouteIndex 起找第一个未抵达路点为目的地
            // （②按直线段逐帧逼近），抵达即前移索引：进度单调不减，途中遇敌停走/目标死亡后从当前路点
            // 继续、永不开倒车；路线走完转守点；无路线（波次行未配 routeId）兜底直奔守点；关卡无守点时原地驻守
            unit.TargetUnit = null;
            unit.TargetUnitId = null;
            unit.TargetIsGuardPoint = false;
            IReadOnlyList<Vector3> route = unit.Route;
            if (route != null)
            {
                int i = unit.RouteIndex;
                while (i < route.Count && (route[i] - unit.Position).sqrMagnitude <= WaypointArriveDistSq) i++;
                unit.RouteIndex = i;
                if (i < route.Count)
                {
                    unit.MoveDestination = route[i];
                    return;
                }
            }
            unit.MoveDestination = context.GuardPoint != null ? context.GuardPointPosition : unit.Position;
        }

        // 找去路上最近的阻挡角色：索敌候选（③ 已滤倒地/死亡）位置到「unit.Position→destination」线段
        // 的最近点垂距 ≤ 阻挡判定半径（EnemyTable.blockRadius 列，≤0 兜底 0.8）、投影落在线段内、
        // 且已进入自身索敌范围（range 列）（三者齐备才算「撞上挡路人」）。
        // 索敌距离条件不可省——垂距只界定「挡路」，不保证够得着：远处挡路者若照锁，敌人会停走进不了的
        // 攻击态永久钉死；无视继续走、逼近进索敌范围自然命中本分支。命中者取离自身最近（多个阻挡先打贴脸的）。
        private static BattleUnitState FindPathBlocker(BattleUnitState unit, Vector3 destination, BattleIntentContext context)
        {
            Vector3 origin = unit.Position;
            Vector3 segment = destination - origin;
            float segmentLengthSq = segment.sqrMagnitude;
            float blockRadius = unit.Stats.BlockRadius > 0f ? unit.Stats.BlockRadius : DefaultBlockRadius;
            float radiusSq = blockRadius * blockRadius;
            float rangeSq = unit.StopRange * unit.StopRange;

            BattleUnitState best = null;
            float bestDistSq = float.MaxValue;
            foreach (BattleUnitState candidate in context.EnumerateEnemies(unit.Camp))
            {
                Vector3 offset = candidate.Position - origin;
                // 投影参数 0~1 才算「挡在去路上」：身后或过了目的地的单位不拦路
                float t = Vector3.Dot(offset, segment) / segmentLengthSq;
                if (t < 0f || t > 1f) continue;
                Vector3 closest = origin + segment * t;
                if ((candidate.Position - closest).sqrMagnitude > radiusSq) continue;

                // 垂距达标还不够——离得远不算「撞上」：进入自身索敌范围（range 列）才停下攻击（否则停走进不了的攻击态）
                float distSq = offset.sqrMagnitude;
                if (distSq > rangeSq) continue;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
