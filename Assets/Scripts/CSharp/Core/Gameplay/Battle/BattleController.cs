using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗控制器（③心脏）：Step(float dt) 统一推进——
    /// 点数供给 → 波次生成 → 死亡/倒地扫描 → 胜负评估 → 快照刷新。
    /// 伤害不在此层结算：模拟层自动伤害链（攻击节拍→DamageExecutor）已于 2026-10-22 收口退役，
    /// 伤害改由 BE 事件链触发（命中 → 动态取目标 GAS 状态能力 → 写 Hp），本层死亡/倒地扫描只轮询感知。
    /// 意图字段（MoveDestination/Target*）由 ④ 感知能力经 ② 注册的运行时数据写入，本层每拍只消费。
    /// 纯 C# 模拟核（零 UnityEngine.Object 依赖，Vector3 数据除外）；验证脚本 new 全套手工注入即可推演。
    /// 指令流水（Deploy/Move/Revive）是 ⑤④→③ 唯一通道，被拒指令同样记账（seq 不断号）。
    /// 事件为普通 C# event，⑥流程层订阅后桥接 EventCenter 广播给 ②/UI。
    /// </summary>
    public sealed class BattleController
    {
        // 复活费用占位公式：base × mult^已复活次数（📐 挂账 #29，数值待拍板）
        private const int ReviveBaseCost = 20;
        private const int ReviveCostMultiplier = 2;

        // 停走射程随机偏移幅度（±米；同种敌人停在略有差异的半径、聚堆点错开，📐 占位数值待数值设计定稿）
        private const float StopRangeJitter = 0.3f;

        // 敌人进守点判定半径（米；守点交互重做 2026-10-27——敌人走进守点直接回收 + 扣守点消耗值血量，📐 占位可配）
        private const float GuardPointReachRadius = 1f;

        // 技能 CD 管理器（③ 模拟层：入场注册 + Step 节拍推进，感知层只读就绪）；
        // 能量账户 = BattleUnitState.Energy（静态 EnergyManager 统一入账，无实例状态）。
        public SkillCooldownManager Cooldowns { get; } = new SkillCooldownManager();

        private readonly List<BattleUnitState> units = new List<BattleUnitState>();
        private readonly BattleDataPack data;
        private readonly BattleOutcomeEvaluator evaluator;

        private readonly BattleEvaluationContext evalContext = new BattleEvaluationContext();
        private readonly BattleSnapshot snapshot = new BattleSnapshot();

        // 事件（⑥桥接 EventCenter；载荷语义见注释）
        public event Action<BattleUnitState> UnitSpawned;    // 敌人生成/角色部署
        public event Action<BattleUnitState> UnitDowned;     // 角色倒地
        public event Action<BattleUnitState> UnitRevived;    // 角色复活
        public event Action<BattleUnitState> EnemyRemoved;   // 敌人死亡出表（②演出后回池）
        public event Action<int> PointsChanged;              // 点数变化（当前值）
        public event Action<int> WaveChanged;                // 波次推进（1 基波号）
        public event Action<BattleOutcome> OutcomeDecided;   // 胜负判定（只发一次）

        public BattleController(GuardPointState guardPoint, Vector3 guardPosition,
            PointLedger ledger, IBattleWaveDriver waves, BattleDataPack data,
            BattleOutcomeEvaluator evaluator)
        {
            GuardPoint = guardPoint ?? throw new ArgumentNullException(nameof(guardPoint));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Waves = waves ?? throw new ArgumentNullException(nameof(waves));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            GuardPointPosition = guardPosition;

            evalContext.Units = units;
            evalContext.GuardPoint = guardPoint;

            // 快照基线（Points/Wave/守点先行落位，后续刷新只报真实差值）
            RefreshSnapshot();
        }

        public GuardPointState GuardPoint { get; }
        public PointLedger Ledger { get; }
        public IBattleWaveDriver Waves { get; }
        public Vector3 GuardPointPosition { get; }
        public BattleCommandLog CommandLog { get; } = new BattleCommandLog();
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public BattleSnapshot Snapshot => snapshot;
        public IReadOnlyList<BattleUnitState> Units => units;

        // 上场名额 = 编队白名单大小（"八个角色上齐之后无新角色可以再上"，倒地不空名额）
        public int MaxDeployed => data.DeployCosts.Count;

        // 已上场角色数（含倒地）
        public int DeployedCharacterCount { get; private set; }

        // 当前波号（1 基；全部生成完停在最后一波；驱动内部钳制）
        public int CurrentWaveNumber => Waves.CurrentWaveNumber;

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
            // 技能 CD 入场注册：E 初始满 CD（部署即开始冷却，首次放等冷却完——避免部署瞬间自动放；
            // 2026-10-28 用户口径"刚放置 E 冷却重新转而不是 0"）；Q 无 CD 靠能量门槛；敌人无技能不注册
            if (state.Stats.SkillEId > 0
                && BattleDataModule.TryGetSkillCooldown(state.Stats.SkillEId, out float eCooldown))
            {
                Cooldowns.StartCooldown(state, state.Stats.SkillEId, eCooldown);
            }
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

        /// <summary>查询部署费用（HUD 槽位费用展示用；不在编队白名单返回 false）。</summary>
        public bool TryGetDeployCost(string unitId, out int cost)
            => data.DeployCosts.TryGetValue(unitId, out cost);

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

        // 任意阵营按 id 查找（击杀归属回能/事件桥用；未命中返回 null，调用方容错）
        private BattleUnitState FindUnit(string unitId)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].UnitId == unitId) return units[i];
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

            // 技能 CD 统一节拍推进（暂停 = 不调 Step 天然生效；缩减由管理器统一调配）
            Cooldowns.Tick(dt);

            // 减速效果推进（slow 效果行 duration 到点恢复移速乘数）
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnitState slowed = units[i];
                if (slowed.SlowRemaining > 0f)
                {
                    slowed.SlowRemaining = Mathf.Max(0f, slowed.SlowRemaining - dt);
                    if (slowed.SlowRemaining <= 0f) slowed.MoveSlowFactor = 1f;
                }
            }

            // 波次生成（新波触发表经 WaveChanged 外发）
            int waveNumberBefore = CurrentWaveNumber;
            Waves.Tick(dt, SpawnEnemyInternal);
            if (CurrentWaveNumber != waveNumberBefore) WaveChanged?.Invoke(CurrentWaveNumber);

            // 敌人进守点（守点交互重做 2026-10-27：不攻击守点，走进守点直接回收 + 按类型守点消耗值扣血）
            if (GuardPoint != null && !GuardPoint.IsBroken)
            {
                float reachSq = GuardPointReachRadius * GuardPointReachRadius;
                for (int i = units.Count - 1; i >= 0; i--)
                {
                    BattleUnitState unit = units[i];
                    if (unit.Camp != BattleCamp.Enemy || !unit.IsAlive) continue;
                    if ((unit.Position - GuardPointPosition).sqrMagnitude <= reachSq)
                    {
                        GuardPoint.TakeDamage(unit.Stats.GuardPointDamage);
                        Cooldowns.Unregister(unit);
                        units.RemoveAt(i);
                        EnemyRemoved?.Invoke(unit);
                    }
                }
            }

            // 死亡/倒地扫描（倒序，敌人移除安全）——伤害由 BE 事件链写 Hp，本扫描只感知变化
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
                    Cooldowns.Unregister(unit);
                    // 击杀回能（表驱动 2026-10-28：敌人行 energyOnKill，boss 可配多；归属 = 最后命中者，击杀者已离场忽略）
                    if (!string.IsNullOrEmpty(unit.LastHitBy))
                    {
                        int killGain = BattleDataModule.TryGetEnemyEnergyOnKill(unit.UnitId, out int k) ? k : 0;
                        if (killGain > 0) EnergyManager.Add(FindUnit(unit.LastHitBy), killGain);
                    }
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

        // 敌人生成（IBattleWaveDriver 回调；位置 = 波次表出怪点，路线 = 波次行配给路点序列）。
        // UnitId 保持纯 id（身份/索敌/视图地址），等级进 StatsKey（数据缓存键 id@等级）
        private void SpawnEnemyInternal(string enemyId, int level, Vector3 position, IReadOnlyList<Vector3> route)
        {
            string statsKey = BattleDataModule.EnemyCacheKey(enemyId, level);
            if (!data.Stats.TryGetValue(statsKey, out BattleUnitStats stats))
            {
                Debug.LogError($"[Battle] 敌人数值缺失，跳过生成：{statsKey}");
                return;
            }
            var state = new BattleUnitState(enemyId, BattleCamp.Enemy, stats, position, route, statsKey);
            // 停走射程每单位随机偏移（无种子，每只独立抽样）：同种敌人停在不同半径、聚堆点错开，替代软分离推挤
            state.StopRangeOffset = UnityEngine.Random.Range(-StopRangeJitter, StopRangeJitter);
            units.Add(state);
            UnitSpawned?.Invoke(state);
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
