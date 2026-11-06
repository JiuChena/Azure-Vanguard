using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗胜负/守点/快照子服务：守点血量、胜负评估、BattleSnapshot 刷新与点数变化事件检测。
    /// </summary>
    public sealed class CombatOutcomeService : ICombatSubservice
    {
        // 所属战斗服务（事件外发）。
        private CombatService service;

        // 胜负评估器（关卡级注入）。
        private BattleOutcomeEvaluator evaluator;

        // 评估输入（复用实例，不每拍分配）。
        private readonly BattleEvaluationContext evalContext = new BattleEvaluationContext();

        // 战场快照（③→② 状态出口）。
        private readonly BattleSnapshot snapshot = new BattleSnapshot();

        /// <summary>守点模拟状态（无战斗时为 null）。</summary>
        public GuardPointState GuardPoint { get; private set; }

        /// <summary>守点世界坐标。</summary>
        public Vector3 GuardPointPosition { get; private set; }

        /// <summary>当前胜负状态。</summary>
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;

        /// <summary>战场快照。</summary>
        public BattleSnapshot Snapshot => snapshot;

        /// <summary>绑定守点/评估器/单位列表（重复调用先清空）。</summary>
        /// <param name="combatService">所属战斗服务；不允许为 null。</param>
        /// <param name="guardPoint">守点状态；不允许为 null。</param>
        /// <param name="guardPosition">守点世界坐标。</param>
        /// <param name="outcomeEvaluator">胜负评估器；不允许为 null。</param>
        /// <param name="units">在场单位列表（与 Roster 共用同一实例）。</param>
        public void Begin(CombatService combatService, GuardPointState guardPoint, Vector3 guardPosition,
            BattleOutcomeEvaluator outcomeEvaluator, IReadOnlyList<BattleUnitState> units)
        {
            Reset();
            service = combatService;
            GuardPoint = guardPoint;
            GuardPointPosition = guardPosition;
            evaluator = outcomeEvaluator;
            evalContext.Units = units;
            evalContext.GuardPoint = guardPoint;
        }

        /// <summary>清空守点/评估器/快照/胜负状态。</summary>
        public void Reset()
        {
            GuardPoint = null;
            evaluator = null;
            GuardPointPosition = Vector3.zero;
            Outcome = BattleOutcome.Ongoing;
            evalContext.Units = null;
            evalContext.GuardPoint = null;
            evalContext.AllWavesSpawned = false;
            snapshot.Points = 0;
            snapshot.WaveIndex = 0;
            snapshot.AllWavesSpawned = false;
            snapshot.GuardPointHp = 0;
            snapshot.GuardPointMaxHp = 0;
            snapshot.Outcome = BattleOutcome.Ongoing;
            snapshot.Units.Clear();
            service = null;
        }

        /// <summary>执行一次胜负评估；判出结果时外发 OutcomeDecided（只发一次）。</summary>
        /// <param name="allWavesSpawned">全部波次是否已生成完。</param>
        public void Evaluate(bool allWavesSpawned)
        {
            if (evaluator == null || Outcome != BattleOutcome.Ongoing) return;
            evalContext.AllWavesSpawned = allWavesSpawned;
            BattleOutcome outcome = evaluator.Evaluate(evalContext);
            if (outcome == BattleOutcome.Ongoing) return;

            Outcome = outcome;
            service?.RaiseOutcomeDecided(outcome);
        }

        /// <summary>刷新快照；点数变化时外发 PointsChanged。</summary>
        /// <param name="points">当前点数。</param>
        /// <param name="wave">当前波号（1 基）。</param>
        /// <param name="allWavesSpawned">全部波次是否已生成完。</param>
        /// <param name="units">在场单位列表。</param>
        public void RefreshSnapshot(int points, int wave, bool allWavesSpawned, IReadOnlyList<BattleUnitState> units)
        {
            int pointsBefore = snapshot.Points;
            snapshot.Points = points;
            snapshot.WaveIndex = wave;
            snapshot.AllWavesSpawned = allWavesSpawned;
            snapshot.GuardPointHp = GuardPoint != null ? GuardPoint.Hp : 0;
            snapshot.GuardPointMaxHp = GuardPoint != null ? GuardPoint.MaxHp : 0;
            snapshot.Outcome = Outcome;

            snapshot.Units.Clear();
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    BattleUnitState unit = units[i];
                    snapshot.Units.Add(new BattleUnitSnapshot
                    {
                        UnitId = unit.UnitId,
                        Camp = unit.Camp,
                        Vital = unit.Vital,
                        Hp = unit.Hp,
                        Position = unit.Position,
                        ReviveCount = unit.ReviveCount,
                    });
                }
            }

            if (snapshot.Points != pointsBefore) service?.RaisePointsChanged(points);
        }
    }
}
