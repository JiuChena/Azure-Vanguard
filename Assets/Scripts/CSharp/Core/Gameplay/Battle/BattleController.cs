using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗控制器（③心脏）：Step(float dt) 统一推进——
    /// 点数供给 → 波次生成 → 索敌策略回调（意图归角色侧）→ 攻击节拍结算 → 死亡/倒地扫描 → 胜负评估 → 快照刷新。
    /// 纯 C# 模拟核（零 UnityEngine.Object 依赖，Vector3 数据除外）；验证脚本 new 全套手工注入即可推演。
    /// 指令流水（Deploy/Move/Revive）是 ⑤④→③ 唯一通道，被拒指令同样记账（seq 不断号）。
    /// 事件为普通 C# event，⑥流程层订阅后桥接 EventCenter 广播给 ②/UI。
    /// </summary>
    public sealed class BattleController
    {
        // 复活费用占位公式：base × mult^已复活次数（📐 挂账 #29，数值待拍板）
        private const int ReviveBaseCost = 20;
        private const int ReviveCostMultiplier = 2;

        private readonly List<BattleUnitState> units = new List<BattleUnitState>();
        private readonly BattleDataPack data;
        private readonly SkillEffectCatalog effects;
        private readonly BattleOutcomeEvaluator evaluator;
        private readonly IUnitIntentStrategy intentStrategy;
        private readonly System.Random rng;
        private readonly Vector3 spawnPosition;

        private readonly BattleIntentContext intentContext = new BattleIntentContext();
        private readonly BattleEvaluationContext evalContext = new BattleEvaluationContext();
        private readonly BattleSnapshot snapshot = new BattleSnapshot();

        // 事件（⑥桥接 EventCenter；载荷语义见注释）
        public event Action<BattleUnitState> UnitSpawned;    // 敌人生成/角色部署
        public event Action<BattleUnitState> UnitDowned;     // 角色倒地
        public event Action<BattleUnitState> UnitRevived;    // 角色复活
        public event Action<BattleUnitState> EnemyRemoved;   // 敌人死亡出表（②演出后回池）
        public event Action<int> PointsChanged;              // 点数变化（当前值）
        public event Action<int> WaveChanged;                // 波次推进（1 基波号，对齐 WaveRow.WaveIndex）
        public event Action<BattleOutcome> OutcomeDecided;   // 胜负判定（只发一次）

        public BattleController(GuardPointState guardPoint, Vector3 guardPosition, Vector3 spawnPosition,
            PointLedger ledger, WaveDirector waves, BattleDataPack data, SkillEffectCatalog effects,
            BattleOutcomeEvaluator evaluator, IUnitIntentStrategy intentStrategy, int seed)
        {
            GuardPoint = guardPoint ?? throw new ArgumentNullException(nameof(guardPoint));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Waves = waves ?? throw new ArgumentNullException(nameof(waves));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            this.intentStrategy = intentStrategy;
            this.spawnPosition = spawnPosition;
            rng = new System.Random(seed);
            GuardPointPosition = guardPosition;

            intentContext.Units = units;
            intentContext.GuardPoint = guardPoint;
            intentContext.GuardPointPosition = guardPosition;
            evalContext.Units = units;
            evalContext.GuardPoint = guardPoint;

            // 快照基线（Points/Wave/守点先行落位，后续刷新只报真实差值）
            RefreshSnapshot();
        }

        public GuardPointState GuardPoint { get; }
        public PointLedger Ledger { get; }
        public WaveDirector Waves { get; }
        public Vector3 GuardPointPosition { get; }
        public BattleCommandLog CommandLog { get; } = new BattleCommandLog();
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public BattleSnapshot Snapshot => snapshot;
        public IReadOnlyList<BattleUnitState> Units => units;

        // 上场名额 = 编队白名单大小（"八个角色上齐之后无新角色可以再上"，倒地不空名额）
        public int MaxDeployed => data.DeployCosts.Count;

        // 已上场角色数（含倒地）
        public int DeployedCharacterCount { get; private set; }

        // 当前波号（1 基；全部生成完停在最后一波）
        public int CurrentWaveNumber => Waves.AllSpawned ? Waves.WaveIndex : Waves.WaveIndex + 1;

        #region 指令通道（⑤④→③ 唯一入口，全部记账）

        /// <summary>部署角色：点数 + 编队白名单 + 名额 + 重复上场四重校验，通过则扣费注册。</summary>
        public bool SubmitDeploy(string unitId, Vector3 position)
        {
            if (!data.DeployCosts.TryGetValue(unitId, out int cost))
                return RecordReject(BattleCommandType.Deploy, unitId, position, "未在编队白名单");

            if (DeployedCharacterCount >= MaxDeployed)
                return RecordReject(BattleCommandType.Deploy, unitId, position, "上场名额已满");

            if (FindCharacter(unitId) != null)
                return RecordReject(BattleCommandType.Deploy, unitId, position, "该角色已上场");

            if (!Ledger.TrySpend(cost))
                return RecordReject(BattleCommandType.Deploy, unitId, position, "点数不足");

            var state = new BattleUnitState(unitId, BattleCamp.Character, data.Stats[unitId], position);
            units.Add(state);
            DeployedCharacterCount++;
            CommandLog.Append(BattleCommandType.Deploy, unitId, position, BattleCommandResult.Applied, null, cost);
            UnitSpawned?.Invoke(state);
            RefreshSnapshot();
            return true;
        }

        /// <summary>点选移动：只写目的地意图（免费，寻路归 ②执行）。</summary>
        public bool SubmitMove(string unitId, Vector3 position)
        {
            BattleUnitState unit = FindCharacter(unitId);
            if (unit == null || unit.IsDowned)
                return RecordReject(BattleCommandType.Move, unitId, position, "单位不存在或已倒地");

            unit.MoveDestination = position;
            CommandLog.Append(BattleCommandType.Move, unitId, position, BattleCommandResult.Applied, null, 0);
            RefreshSnapshot();
            return true;
        }

        /// <summary>复活倒地角色：按递增公式扣费，满血复活（复活血量 📐 占位 = 满血，挂账 #29）。</summary>
        public bool SubmitRevive(string unitId)
        {
            BattleUnitState unit = FindCharacter(unitId);
            if (unit == null || !unit.IsDowned)
                return RecordReject(BattleCommandType.Revive, unitId, Vector3.zero, "单位不在倒地状态");

            int cost = GetReviveCost(unit);
            if (!Ledger.TrySpend(cost))
                return RecordReject(BattleCommandType.Revive, unitId, Vector3.zero, "点数不足");

            unit.Hp = unit.Stats.MaxHp;
            unit.Vital = UnitVitalState.Alive;
            unit.ReviveCount++;
            CommandLog.Append(BattleCommandType.Revive, unitId, Vector3.zero, BattleCommandResult.Applied, null, cost);
            UnitRevived?.Invoke(unit);
            RefreshSnapshot();
            return true;
        }

        // 复活费用：base × mult^已复活次数（递增 📐 挂账 #29）
        public int GetReviveCost(BattleUnitState unit)
        {
            return ReviveBaseCost * (int)Math.Pow(ReviveCostMultiplier, unit.ReviveCount);
        }

        // 记一条被拒指令（seq 占号不断号）并返回 false，调用方直接 return
        private bool RecordReject(BattleCommandType type, string unitId, Vector3 position, string reason)
        {
            CommandLog.Append(type, unitId, position, BattleCommandResult.Rejected, reason, 0);
            RefreshSnapshot();
            return false;
        }

        private BattleUnitState FindCharacter(string unitId)
        {
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState unit = units[i];
                if (unit.Camp == BattleCamp.Character && unit.UnitId == unitId) return unit;
            }
            return null;
        }

        #endregion

        #region 模拟推进

        /// <summary>统一推进一拍；胜负已判后冻结（暂停 = 不调 Step，倍速 = dt×scale）。</summary>
        public void Step(float dt)
        {
            if (Outcome != BattleOutcome.Ongoing) return;

            // 点数供给（余额变化由 RefreshSnapshot 统一检测外发）
            Ledger.Tick(dt);

            // 波次生成（新波触发表经 WaveChanged 外发）
            int waveNumberBefore = CurrentWaveNumber;
            Waves.Update(dt, SpawnEnemyInternal);
            if (CurrentWaveNumber != waveNumberBefore) WaveChanged?.Invoke(CurrentWaveNumber);

            // 索敌意图回调（目标选择归角色侧策略；敌人 BHSM 批 3 接入同一入口）
            if (intentStrategy != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i].IsAlive) intentStrategy.UpdateIntent(units[i], intentContext);
                }
            }

            // 攻击节拍：冷却统一衰减，有有效目标且冷却归零才结算
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState unit = units[i];
                if (unit.AttackCooldown > 0f)
                    unit.AttackCooldown = Math.Max(0f, unit.AttackCooldown - dt);
                if (!unit.IsAlive || !unit.HasAttackIntent || unit.AttackCooldown > 0f) continue;

                if (ResolveTarget(unit, out BattleUnitState targetUnit, out bool targetIsGuard))
                {
                    if (TryAttack(unit, targetUnit, targetIsGuard))
                        unit.AttackCooldown = unit.Stats.AtkInterval;
                }
            }

            // 死亡/倒地扫描（倒序，敌人移除安全）
            for (int i = units.Count - 1; i >= 0; i--)
            {
                BattleUnitState unit = units[i];
                if (!unit.IsAlive || unit.Hp > 0) continue;

                if (unit.Camp == BattleCamp.Character)
                {
                    unit.Vital = UnitVitalState.Downed;
                    unit.TargetUnitId = null;
                    unit.TargetIsGuardPoint = false;
                    UnitDowned?.Invoke(unit);
                }
                else
                {
                    units.RemoveAt(i);
                    EnemyRemoved?.Invoke(unit);
                }
            }

            // 胜负评估（每拍；判出即冻结）
            evalContext.AllWavesSpawned = Waves.AllSpawned;
            BattleOutcome outcome = evaluator.Evaluate(evalContext);
            if (outcome != BattleOutcome.Ongoing)
            {
                Outcome = outcome;
                OutcomeDecided?.Invoke(outcome);
            }

            RefreshSnapshot();
        }

        // 敌人生成（WaveDirector 回调；位置 = 关卡出生点）
        private void SpawnEnemyInternal(WaveRow row)
        {
            if (!data.Stats.TryGetValue(row.EnemyId, out BattleUnitStats stats))
            {
                Debug.LogError($"[Battle] 敌人数值缺失，跳过生成：{row.EnemyId}");
                return;
            }
            var state = new BattleUnitState(row.EnemyId, BattleCamp.Enemy, stats, spawnPosition);
            units.Add(state);
            UnitSpawned?.Invoke(state);
        }

        // 目标解析与射程重验（意图是上一拍的，本拍目标可能已走出射程/倒地）
        private bool ResolveTarget(BattleUnitState unit, out BattleUnitState targetUnit, out bool targetIsGuard)
        {
            targetUnit = null;
            targetIsGuard = false;
            float rangeSq = unit.Stats.Range * unit.Stats.Range;

            if (unit.TargetIsGuardPoint)
            {
                if (GuardPoint.IsBroken) return false;
                targetIsGuard = true;
                return (GuardPointPosition - unit.Position).sqrMagnitude <= rangeSq;
            }

            if (unit.TargetUnitId == null) return false;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState candidate = units[i];
                if (candidate.UnitId != unit.TargetUnitId || !candidate.IsAlive
                    || candidate.Camp == unit.Camp) continue;
                if ((candidate.Position - unit.Position).sqrMagnitude > rangeSq) return false;
                targetUnit = candidate;
                return true;
            }
            return false;
        }

        // 一次普攻结算：经 SkillEffectCatalog 分发（effectType 未注册返回 false，冷却不重置下拍重试）
        private bool TryAttack(BattleUnitState unit, BattleUnitState targetUnit, bool targetIsGuard)
        {
            if (!data.NormalAttacks.TryGetValue(unit.UnitId,
                    out BattleDataModule.NormalAttackData attack))
            {
                Debug.LogError($"[Battle] 普攻数据缺失：{unit.UnitId}");
                return false;
            }

            var ctx = new SkillEffectContext
            {
                Caster = unit,
                TargetUnit = targetUnit,
                TargetGuard = GuardPoint,
                TargetIsGuardPoint = targetIsGuard,
                Effect = attack.Effect,
                Attack = attack,
                Rng = rng,
            };
            return effects.TryExecute(attack.Effect, in ctx);
        }

        #endregion

        // 快照刷新（Step 末与每条指令后；PointsChanged 在此统一检测外发）
        private void RefreshSnapshot()
        {
            int pointsBefore = snapshot.Points;
            snapshot.Points = Ledger.Current;
            snapshot.WaveIndex = CurrentWaveNumber;
            snapshot.AllWavesSpawned = Waves.AllSpawned;
            snapshot.GuardPointHp = GuardPoint.Hp;
            snapshot.GuardPointMaxHp = GuardPoint.MaxHp;
            snapshot.Outcome = Outcome;

            snapshot.Units.Clear();
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

            if (snapshot.Points != pointsBefore) PointsChanged?.Invoke(Ledger.Current);
        }
    }
}
