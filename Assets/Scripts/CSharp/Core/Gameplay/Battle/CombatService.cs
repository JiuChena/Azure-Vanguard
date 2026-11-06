using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 战斗服务（③心脏 + 全局单例）：上层管理器，持有并驱动单位档案/资源/胜负/指令/Effect 子服务。
    /// Step(float dt) 统一推进点数供给 → 波次生成 → 死亡/倒地扫描 → 胜负评估 → 快照刷新。
    /// 伤害不在模拟层自动结算：由 BE 事件链触发写 Hp，本层死亡/倒地扫描只轮询感知。
    /// 全局单例常驻，进入/离开战斗场景都调用 Reset 清空战斗数据。
    /// </summary>
    public sealed class CombatService
    {
        // 敌人进守点判定半径（米；守点交互 2026-10-27——敌人走进守点直接回收 + 扣守点消耗值血量，📐 占位可配）
        private const float GuardPointReachRadius = 1f;

        // 全局单例：常驻，不随场景销毁。
        private static CombatService instance;

        /// <summary>战斗服务全局单例（无战斗时各门面返回安全空值）。</summary>
        public static CombatService Instance => instance ??= new CombatService();

        // 编辑器关闭 Domain Reload 时，静态单例/静态表会跨 Play 会话存活；进 Play 前统一清一次。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            instance?.Reset();
            instance = null;
            BattleSelection.Current?.Dispose();
            FieldUnitRegistry.Clear();
            BattleDataModule.Clear();
            if (Application.isPlaying) Time.timeScale = 1f;
        }

        /// <summary>在场单位档案子服务。</summary>
        public CombatRosterService Roster { get; private set; }

        /// <summary>点数 / 技能 CD / 能量子服务。</summary>
        public CombatResourceService Resources { get; private set; }

        /// <summary>守点 / 胜负 / 快照子服务。</summary>
        public CombatOutcomeService OutcomeService { get; private set; }

        /// <summary>指令通道子服务。</summary>
        public CombatCommandService Commands { get; private set; }

        /// <summary>全局 Effect 子服务（角色/敌人分区 + 光环）。</summary>
        public CombatEffectService Effects { get; private set; }

        /// <summary>视图桥子服务。</summary>
        public CombatViewService Views { get; private set; }

        /// <summary>世界侧部署子服务。</summary>
        public CombatDeployService Deploy { get; private set; }

        /// <summary>选中态子服务。</summary>
        public CombatSelectionService Selection { get; private set; }

        /// <summary>事件桥子服务。</summary>
        public CombatEventService Events { get; private set; }

        /// <summary>装配子服务（无状态工厂）。</summary>
        public CombatAssemblyService Assembly { get; } = new CombatAssemblyService();

        /// <summary>部署名单（编队顺序；无战斗为 null）。</summary>
        public IReadOnlyList<string> DeployRoster { get; private set; }

        /// <summary>模拟是否暂停（暂停时 Step 不推进 + timeScale 冻结表现层）。</summary>
        public bool SimulationPaused { get; private set; }

        /// <summary>战斗数据包（装配产物；命令/单位生成共用只读数据源）。</summary>
        public BattleDataPack DataPack { get; private set; }

        /// <summary>战斗随机源（伤害暴击/天赋概率；验证脚本可注入固定种子/序列）。</summary>
        public System.Random Rng { get; private set; }

        /// <summary>波次驱动（生产 = CombatWaveService；验证可注入桩）。</summary>
        public IBattleWaveDriver Waves { get; private set; }

        /// <summary>生产波次子服务（测试注入桩时为 null）。</summary>
        public CombatWaveService WaveService { get; private set; }

        /// <summary>当前是否有一场装配完成的战斗。</summary>
        public bool IsBattleActive { get; private set; }

        /// <summary>当前战斗会话号（成功装配后非 0；供 HUD 判定新战斗首帧）。</summary>
        public int BattleSessionId { get; private set; }

        // 异步/会话代际：Reset 自增以作废所有在途 await。
        private int battleEpoch;

        // 单次在途装配守卫（防止两个 preload 并发写静态缓存）。
        private bool beginInFlight;

        // 脱离全局单例的测试实例（Reset 不碰全局静态缓存/时间缩放）。
        private bool isolated;

        // 本服务是否拥有 timeScale（暂停时记录暂停前值，恢复时只还原自己改过的）。
        private bool ownsTimeScale;

        // 暂停前的 timeScale（暂停时记录）。
        private float timeScaleBeforePause = 1f;

        // 缓存的出怪回调（避免每帧方法组转换分配）。
        private Action<string, int, Vector3, IReadOnlyList<Vector3>> spawnEnemyCallback;

        // 事件（⑥桥接 EventCenter；载荷语义见注释）
        public event Action<BattleUnitState> UnitSpawned;    // 敌人生成/角色部署
        public event Action<BattleUnitState> UnitDowned;     // 角色倒地
        public event Action<BattleUnitState> UnitRevived;    // 角色复活
        public event Action<BattleUnitState> EnemyRemoved;   // 敌人死亡出表（②演出后回池）
        public event Action<int> PointsChanged;              // 点数变化（当前值）
        public event Action<int> WaveChanged;                // 波次推进（1 基波号）
        public event Action<BattleOutcome> OutcomeDecided;   // 胜负判定（只发一次）
        public event Action<BattleDamageEvent> DamageDealt;  // 伤害结算（天赋/表现/HUD）

        // 单例构造：只初始化无副作用的子服务；战斗依赖由 BeginBattleAsync 注入。
        private CombatService()
        {
            Rng = new System.Random();
            Roster = new CombatRosterService();
            Resources = new CombatResourceService();
            OutcomeService = new CombatOutcomeService();
            Commands = new CombatCommandService();
            Effects = new CombatEffectService();
            Views = new CombatViewService();
            Deploy = new CombatDeployService();
            Selection = new CombatSelectionService();
            Events = new CombatEventService();
            spawnEnemyCallback = Roster.SpawnEnemy;
        }

        /// <summary>按一套战斗依赖创建实例（验证脚本/临时装配用；正式入口走 Instance）。</summary>
        /// <param name="guardPoint">守点模拟状态。</param>
        /// <param name="guardPosition">守点世界坐标。</param>
        /// <param name="ledger">点数台账。</param>
        /// <param name="waves">波次驱动（生产=CombatWaveService，验证=桩）。</param>
        /// <param name="data">战斗数据包。</param>
        /// <param name="evaluator">胜负评估器。</param>
        /// <param name="rng">随机源；null = 新建。</param>
        public CombatService(GuardPointState guardPoint, Vector3 guardPosition,
            PointLedger ledger, IBattleWaveDriver waves, BattleDataPack data,
            BattleOutcomeEvaluator evaluator, System.Random rng = null)
        {
            Rng = rng ?? new System.Random();
            Waves = waves ?? throw new ArgumentNullException(nameof(waves));
            DataPack = data ?? throw new ArgumentNullException(nameof(data));
            if (guardPoint == null) throw new ArgumentNullException(nameof(guardPoint));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));

            Roster = new CombatRosterService();
            Resources = new CombatResourceService();
            OutcomeService = new CombatOutcomeService();
            Commands = new CombatCommandService();
            Effects = new CombatEffectService();
            Views = new CombatViewService();
            Deploy = new CombatDeployService();
            Selection = new CombatSelectionService();
            Events = new CombatEventService();
            spawnEnemyCallback = Roster.SpawnEnemy;

            Roster.Begin(this, DataPack);
            Resources.Begin(this, ledger);
            OutcomeService.Begin(this, guardPoint, guardPosition, evaluator, Roster.Units);
            Commands.Begin(this);
            Effects.Begin(this);

            IsBattleActive = true;
            BattleSessionId = 1;

            // 快照基线（Points/Wave/守点先行落位，后续刷新只报真实差值）
            RefreshSnapshot();
        }

        /// <summary>
        /// 创建脱离全局单例的模拟实例（验证脚本专用）：不碰 Addressables /
        /// EventCenter / 全局静态缓存，Reset 只清自身状态。
        /// </summary>
        /// <returns>可直接 Step/Submit 的独立战斗实例。</returns>
        public static CombatService CreateForSimulation(GuardPointState guardPoint, Vector3 guardPosition,
            PointLedger ledger, IBattleWaveDriver waves, BattleDataPack data,
            BattleOutcomeEvaluator evaluator, System.Random rng = null)
        {
            var service = new CombatService(guardPoint, guardPosition, ledger, waves, data, evaluator, rng);
            service.isolated = true;
            return service;
        }

        #region 门面（保留原 BattleController / CombatManager 访问名）

        /// <summary>技能 CD 管理器（感知层只读就绪判定）。</summary>
        public SkillCooldownManager Cooldowns => Resources.Cooldowns;

        /// <summary>点数台账。</summary>
        public PointLedger Ledger => Resources.Ledger;

        /// <summary>当前点数。</summary>
        public int Points => Resources.Points;

        /// <summary>距下次点数补给的秒数（到顶恒 0）。</summary>
        public float SecondsToNextSupply => Resources.SecondsToNextSupply;

        /// <summary>守点模拟状态。</summary>
        public GuardPointState GuardPoint => OutcomeService.GuardPoint;

        /// <summary>守点世界坐标。</summary>
        public Vector3 GuardPointPosition => OutcomeService.GuardPointPosition;

        /// <summary>当前胜负状态。</summary>
        public BattleOutcome Outcome => OutcomeService.Outcome;

        /// <summary>战场快照（③→② 状态出口）。</summary>
        public BattleSnapshot Snapshot => OutcomeService.Snapshot;

        /// <summary>指令流水账本。</summary>
        public BattleCommandLog CommandLog => Commands.CommandLog;

        /// <summary>全部在场单位。</summary>
        public IReadOnlyList<BattleUnitState> Units => Roster.Units;

        /// <summary>上场名额 = 编队白名单大小。</summary>
        public int MaxDeployed => Commands.MaxDeployed;

        /// <summary>已上场角色数（含倒地）。</summary>
        public int DeployedCharacterCount => Roster.DeployedCharacterCount;

        /// <summary>波次总数（HUD 波次 X/Y 的分母）。</summary>
        public int TotalWaves => Waves != null ? Waves.TotalWaves : 0;

        /// <summary>当前波号（1 基；无战斗为 0）。</summary>
        public int CurrentWaveNumber => Waves != null ? Waves.CurrentWaveNumber : 0;

        /// <summary>全部波次是否已生成完。</summary>
        public bool AllSpawned => Waves != null && Waves.AllSpawned;

        #endregion

        /// <summary>广播一次伤害结算事件（BattleDamage 唯一调用）。</summary>
        /// <param name="e">伤害事件。</param>
        internal void RaiseDamageDealt(BattleDamageEvent e)
        {
            DamageDealt?.Invoke(e);
        }

        /// <summary>外发单位出生事件。</summary>
        /// <param name="unit">出生单位。</param>
        internal void RaiseUnitSpawned(BattleUnitState unit) => UnitSpawned?.Invoke(unit);

        /// <summary>外发角色倒地事件。</summary>
        /// <param name="unit">倒地角色。</param>
        internal void RaiseUnitDowned(BattleUnitState unit) => UnitDowned?.Invoke(unit);

        /// <summary>外发角色复活事件。</summary>
        /// <param name="unit">复活角色。</param>
        internal void RaiseUnitRevived(BattleUnitState unit) => UnitRevived?.Invoke(unit);

        /// <summary>外发敌人出表事件。</summary>
        /// <param name="unit">出表敌人。</param>
        internal void RaiseEnemyRemoved(BattleUnitState unit) => EnemyRemoved?.Invoke(unit);

        /// <summary>外发点数变化事件。</summary>
        /// <param name="points">当前点数。</param>
        internal void RaisePointsChanged(int points) => PointsChanged?.Invoke(points);

        /// <summary>外发波次推进事件。</summary>
        /// <param name="wave">当前波号（1 基）。</param>
        internal void RaiseWaveChanged(int wave) => WaveChanged?.Invoke(wave);

        /// <summary>外发胜负判定事件。</summary>
        /// <param name="outcome">判定结果。</param>
        internal void RaiseOutcomeDecided(BattleOutcome outcome) => OutcomeDecided?.Invoke(outcome);

        /// <summary>战斗结束清理：释放全部子服务状态并复位会话。</summary>
        public void Reset()
        {
            battleEpoch++;
            IsBattleActive = false;
            BattleSessionId = 0;

            // 只恢复本服务改过的 timeScale，避免踩掉其他系统的全局暂停
            if (ownsTimeScale)
            {
                if (Application.isPlaying) Time.timeScale = timeScaleBeforePause;
                ownsTimeScale = false;
            }
            SimulationPaused = false;

            Views?.Reset();
            Deploy?.Reset();
            Selection?.Reset();
            Events?.Reset();
            Effects?.Reset();
            Commands?.Reset();
            Roster?.Reset();
            Resources?.Reset();
            OutcomeService?.Reset();

            DataPack = null;
            Rng = null;
            Waves = null;
            WaveService = null;
            DeployRoster = null;

            // 测试实例不碰全局静态缓存
            if (!isolated)
            {
                BattleDataModule.Clear();
                FieldUnitRegistry.Clear();
            }
        }

        /// <summary>战斗结束清理（保留旧 Dispose 名称给验证脚本；等价 Reset）。</summary>
        public void Dispose()
        {
            Reset();
        }

        /// <summary>按会话号结束战斗：只有当前会话才 Reset，防止旧场景迟到拆除误伤新战斗。</summary>
        /// <param name="session">BeginBattleAsync 返回的会话号；0 表示尚未装配成功。</param>
        public void EndBattle(int session)
        {
            if (session == BattleSessionId || (session == 0 && !IsBattleActive)) Reset();
        }

        /// <summary>装配并开始一场战斗：Reset → 预热/打包 → 初始化全部子服务 → 打开事件桥。</summary>
        /// <param name="config">场景配置。</param>
        /// <returns>会话号；0 = 失败（已 Reset）。</returns>
        public async Task<int> BeginBattleAsync(CombatBattleConfig config)
        {
            if (beginInFlight)
            {
                Debug.LogWarning("[CombatService] 上一场装配仍在进行，拒绝新的装配请求。");
                return 0;
            }

            beginInFlight = true;
            try
            {
                Reset();
                int token = ++battleEpoch;
                CombatSetupResult result = await Assembly.BuildAsync(config);
                if (result == null || token != battleEpoch)
                {
                    if (result != null) Reset();
                    return 0;
                }

                DataPack = result.DataPack;
                DeployRoster = result.DeployRoster;
                Rng = new System.Random();

                WaveService = new CombatWaveService();
                WaveService.Begin(result.WaveGroups, config);
                Waves = WaveService;

                Roster.Begin(this, DataPack);
                Resources.Begin(this, result.Ledger);
                OutcomeService.Begin(this, result.GuardPoint, result.GuardPosition,
                    result.Evaluator, Roster.Units);
                Commands.Begin(this);
                Effects.Begin(this);
                spawnEnemyCallback = Roster.SpawnEnemy;

                Selection.Begin();
                Views.Begin(this, config.ViewRoot);
                Deploy.Begin(this, Views, config);
                Events.Bind(this);

                FieldUnitRegistry.Clear();
                if (config.GuardPoint != null) FieldUnitRegistry.RegisterGuardPoint(config.GuardPoint);

                IsBattleActive = true;
                BattleSessionId = token;
                RefreshSnapshot();
                return token;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CombatService] 战斗装配异常：{exception}");
                Reset();
                return 0;
            }
            finally
            {
                beginInFlight = false;
            }
        }

        /// <summary>每帧驱动：视图桥每帧同步；暂停/胜负已定时不推进模拟。</summary>
        /// <param name="dt">本帧时长（秒）。</param>
        public void Tick(float dt)
        {
            if (!IsBattleActive) return;

            Views?.Tick();

            if (SimulationPaused || Outcome != BattleOutcome.Ongoing) return;
            Step(dt);
        }

        /// <summary>切换模拟暂停：Step 停止 + timeScale 冻结表现层（只恢复自己改过的缩放）。</summary>
        public void TogglePause()
        {
            if (!IsBattleActive) return;

            SimulationPaused = !SimulationPaused;
            if (SimulationPaused)
            {
                timeScaleBeforePause = Time.timeScale;
                ownsTimeScale = true;
                if (Application.isPlaying) Time.timeScale = 0f;
            }
            else if (ownsTimeScale)
            {
                if (Application.isPlaying) Time.timeScale = timeScaleBeforePause;
                ownsTimeScale = false;
            }
            Debug.Log($"[CombatService] 模拟暂停：{SimulationPaused}");
        }

        #region 视图/部署门面（委托子服务）

        /// <summary>视图实例反查单位（调试面板点击选中用）。</summary>
        /// <param name="view">点击命中的视图对象。</param>
        /// <param name="unit">反查到的单位；未命中为 null。</param>
        /// <returns>命中返回 true。</returns>
        public bool TryGetUnitByView(GameObject view, out BattleUnitState unit)
        {
            if (Views == null)
            {
                unit = null;
                return false;
            }
            return Views.TryGetUnitByView(view, out unit);
        }

        /// <summary>该角色是否已放置过。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <returns>已放置返回 true。</returns>
        public bool IsUnitDeployed(string unitId)
        {
            return Deploy != null && Deploy.IsUnitDeployed(unitId);
        }

        /// <summary>开始拖动编队卡。</summary>
        /// <param name="unitId">角色 id。</param>
        public void BeginDeployDrag(string unitId) => Deploy?.BeginDeployDrag(unitId);

        /// <summary>拖动中更新幽灵预览位置。</summary>
        /// <param name="worldPoint">落点世界坐标。</param>
        public void UpdateDeployDrag(Vector3 worldPoint) => Deploy?.UpdateDeployDrag(worldPoint);

        /// <summary>结束拖动并尝试放置。</summary>
        /// <param name="worldPoint">落点世界坐标。</param>
        /// <returns>部署成功返回 true。</returns>
        public bool EndDeployDrag(Vector3 worldPoint) => Deploy != null && Deploy.EndDeployDrag(worldPoint);

        /// <summary>取消拖动。</summary>
        public void CancelDeployDrag() => Deploy?.CancelDeployDrag();

        /// <summary>执行一次放置校验。</summary>
        /// <param name="point">待判定世界点。</param>
        /// <returns>可放置返回 true。</returns>
        public bool IsDeployable(Vector3 point) => Deploy != null && Deploy.IsDeployable(point);

        /// <summary>守点当前血量。</summary>
        public int GuardHp => OutcomeService?.GuardPoint != null ? OutcomeService.GuardPoint.Hp : 0;

        /// <summary>守点最大血量。</summary>
        public int GuardMaxHp => OutcomeService?.GuardPoint != null ? OutcomeService.GuardPoint.MaxHp : 0;

        #endregion

        #region 指令通道（委托 CombatCommandService）

        /// <summary>部署角色：点数 + 白名单 + 名额 + 重复上场校验，通过则扣费注册。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <param name="position">部署点。</param>
        /// <returns>部署成功返回 true。</returns>
        public bool SubmitDeploy(string unitId, Vector3 position) => Commands.SubmitDeploy(unitId, position);

        /// <summary>点选移动：只写目的地意图。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <param name="position">目标点。</param>
        /// <returns>接受返回 true。</returns>
        public bool SubmitMove(string unitId, Vector3 position) => Commands.SubmitMove(unitId, position);

        /// <summary>复活倒地角色：按递增公式扣费，满血复活。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <returns>复活成功返回 true。</returns>
        public bool SubmitRevive(string unitId) => Commands.SubmitRevive(unitId);

        /// <summary>查询部署费用（HUD 槽位展示用）。</summary>
        /// <param name="unitId">角色 id。</param>
        /// <param name="cost">部署费用。</param>
        /// <returns>在编队白名单内返回 true。</returns>
        public bool TryGetDeployCost(string unitId, out int cost) => Commands.TryGetDeployCost(unitId, out cost);

        /// <summary>复活费用：base × mult^已复活次数。</summary>
        /// <param name="unit">倒地角色。</param>
        /// <returns>费用。</returns>
        public int GetReviveCost(BattleUnitState unit) => Commands.GetReviveCost(unit);

        #endregion

        #region 模拟推进

        /// <summary>统一推进一拍；胜负已判后冻结（暂停 = 不调 Step，倍速 = dt×scale）。</summary>
        /// <param name="dt">本拍时长（秒）。</param>
        public void Step(float dt)
        {
            if (!IsBattleActive || Waves == null || Outcome != BattleOutcome.Ongoing) return;

            // 点数供给 + 技能 CD 节拍
            Resources.Tick(dt);

            // 减速效果推进
            Roster.TickStatuses(dt);

            // 波次生成（新波触发表经 WaveChanged 外发）
            int waveNumberBefore = CurrentWaveNumber;
            Waves.Tick(dt, spawnEnemyCallback);
            if (CurrentWaveNumber != waveNumberBefore) RaiseWaveChanged(CurrentWaveNumber);

            // 敌人进守点（走进守点直接回收 + 按类型守点消耗值扣血）
            GuardPointState guardPoint = OutcomeService.GuardPoint;
            if (guardPoint != null && !guardPoint.IsBroken)
            {
                float reachSq = GuardPointReachRadius * GuardPointReachRadius;
                for (int i = Roster.Units.Count - 1; i >= 0; i--)
                {
                    BattleUnitState unit = Roster.Units[i];
                    if (unit.Camp != BattleCamp.Enemy || !unit.IsAlive) continue;
                    if ((unit.Position - OutcomeService.GuardPointPosition).sqrMagnitude <= reachSq)
                    {
                        guardPoint.TakeDamage(unit.Stats.GuardPointDamage);
                        Resources.Cooldowns.Unregister(unit);
                        Roster.RemoveEnemyAt(i);
                    }
                }
            }

            // 死亡/倒地扫描（倒序，敌人移除安全）——伤害由 BE 事件链写 Hp，本扫描只感知变化
            for (int i = Roster.Units.Count - 1; i >= 0; i--)
            {
                BattleUnitState unit = Roster.Units[i];
                if (!unit.IsAlive || unit.Hp > 0) continue;

                if (unit.Camp == BattleCamp.Character)
                {
                    Roster.MarkDownedAt(i);
                }
                else
                {
                    Resources.Cooldowns.Unregister(unit);
                    // 击杀回能（表驱动：敌人行 energyOnKill；归属 = 最后命中者）
                    if (!string.IsNullOrEmpty(unit.LastHitBy))
                    {
                        int killGain = BattleDataModule.TryGetEnemyEnergyOnKill(unit.UnitId, out int k) ? k : 0;
                        if (killGain > 0) Resources.AddEnergy(Roster.FindUnit(unit.LastHitBy), killGain);
                    }
                    Roster.RemoveEnemyAt(i);
                }
            }

            // 胜负评估（每拍；判出即冻结）
            OutcomeService.Evaluate(Waves.AllSpawned);

            RefreshSnapshot();
        }

        #endregion

        /// <summary>刷新快照（Step 末与每条指令后；点数变化在此统一检测外发）。</summary>
        internal void RefreshSnapshot()
        {
            OutcomeService?.RefreshSnapshot(Resources.Points, CurrentWaveNumber, AllSpawned, Roster.Units);
        }
    }
}
