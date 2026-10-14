using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 索敌/意图查询面（③提供的候选服务，不做目标选择——索敌归角色，2026-10-14 边界定案）：
    /// 策略实现遍历候选自行决定打谁/去哪，把结论写进 BattleUnitState 意图字段。
    /// </summary>
    public sealed class BattleIntentContext
    {
        public IReadOnlyList<BattleUnitState> Units { get; set; }

        // 守点模拟状态（null = 关卡无守点）
        public GuardPointState GuardPoint { get; set; }

        // 守点位置（射程判定用；场景数据，⑥装配传入）
        public Vector3 GuardPointPosition { get; set; }

        /// <summary>枚举指定阵营的存活敌方候选（Downed 不进候选）。</summary>
        public IEnumerable<BattleUnitState> EnumerateEnemies(BattleCamp ofCamp)
        {
            BattleCamp enemyCamp = ofCamp == BattleCamp.Character ? BattleCamp.Enemy : BattleCamp.Character;
            for (int i = 0; i < Units.Count; i++)
            {
                BattleUnitState unit = Units[i];
                if (unit.Camp == enemyCamp && unit.IsAlive) yield return unit;
            }
        }

        /// <summary>守点是否在射程内（守点已破不视为有效目标）。</summary>
        public bool IsGuardPointInRange(BattleUnitState unit)
        {
            if (GuardPoint == null || GuardPoint.IsBroken) return false;
            float rangeSq = unit.Stats.Range * unit.Stats.Range;
            return (GuardPointPosition - unit.Position).sqrMagnitude <= rangeSq;
        }
    }

    /// <summary>
    /// 单位意图策略接口：每拍由 BattleController 逐存活单位回调，写意图字段（移动目的地/攻击目标）。
    /// 角色侧实现 = 默认索敌策略（首切片，切片 2 由 GAS 能力/策略配置替代）；敌人侧实现 = BHSM（批 3）。
    /// </summary>
    public interface IUnitIntentStrategy
    {
        void UpdateIntent(BattleUnitState unit, BattleIntentContext context);
    }
}
