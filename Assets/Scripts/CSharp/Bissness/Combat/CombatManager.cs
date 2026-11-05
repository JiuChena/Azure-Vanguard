using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Abilities.Buff;
using Framework.Gameplay.Battle;
using Framework.Gameplay.Player;
using Framework.Gameplay.Unit;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 战斗场景管理器（场景侧唯一管理件，四合一）：
/// ① 装配编排：部署名单解析（编队管理器当前编队，唯一来源）→ 数据预热（StageTable 关卡行 + 全表域）
///    → ①→③ 打包 → 控制器组装 → 门面绑定 → 事件桥 → HUD 打开；战斗缓存生命周期 = 本组件生命周期；
/// ② 波次驱动（IBattleWaveDriver 场景实现）：出怪点绑定（pointId → Transform）+ 路线表（routeId → 路点
///    序列，Gizmos 可视化）+ 敌人创建间隔 + FIFO 节奏；时钟跟随模拟步进（本组件 Update →
///    BattleController.Step → Tick），暂停/倍速天然生效；
/// ③ 战斗上下文门面：控制器/守点/点数/补给倒计时/编队名单——CombatPanel 等 HUD 快速访问口；
/// ④ 视图桥：按单位 id 动态加载战斗视图 prefab（id 即 Addressables 地址，Combat 标签组交集解析，
///    角色敌人同通道，无 Inspector 强制引用），出生注册能力数据入口（GAS 上下文），敌人出表销毁视图。
/// ⑤ 部署拖放（世界侧）：编队卡拖放的放置校验（区域盒/可放置层白名单/不可放置层球检测/角色过近）与 SubmitDeploy
///    提交、幽灵预览（纯视觉外壳：去 NavMeshAgent/脚本/Rigidbody，transform 直改）+ 幽灵板块与红调、
///    出生视图落地序列（agent 网面预置：出生点在网面上才启用代理，未连通高台保持禁用防拉回）；屏幕侧（卡片升降/射线映射）在 CombatPanel。
/// 关卡静态参数（守点血量/点数经济/胜负策略）走 LevelConfig SO（场景拖拽）；
/// 波次序列唯一走 StageTable（levelConfig.StageId 查表 waveIds，有序）。
/// </summary>
public sealed class CombatManager : MonoBehaviour, IBattleWaveDriver
{
    /// <summary>战斗场景唯一实例（HUD 等快速访问口；场景卸载后为 null）。</summary>
    public static CombatManager Current { get; private set; }

    /// <summary>出怪点绑定：WaveTable.enemyCreatePointId → 场景位置。</summary>
    [Serializable]
    public sealed class SpawnPointBinding
    {
        [Tooltip("出怪点 id（WaveTable.enemyCreatePointId）")]
        public string pointId;

        [Tooltip("出怪点位置（敌人出生 Transform）")]
        public Transform point;
    }

    [Header("Config")]
    [Tooltip("关卡配置（SO 拖拽：守点血量/点数经济/胜负策略；波次序列经 StageId 查 StageTable）")]
    [SerializeField] private LevelConfig levelConfig;

    [Header("Deploy Drag（编队卡拖放放置）")]
    [Tooltip("可放置区域角点 A（与 B 对角组成三维包围盒；Transform 拖拽，任一未配置则全域不可放置）")]
    [SerializeField] private Transform deployAreaPointA;

    [Tooltip("可放置区域角点 B（与 A 对角组成三维包围盒）")]
    [SerializeField] private Transform deployAreaPointB;

    [Tooltip("放置球检测半径（不可放置层障碍命中 / 已部署角色过近共用）")]
    [SerializeField] private float deployCheckRadius = 1f;

    [Tooltip("不可放置层（场景障碍物放此层，球检测命中即拒绝放置；LayerMask 可多选）")]
    [SerializeField] private LayerMask unputableLayers;

    [Tooltip("可放置地面层白名单（判定点半径内必须命中这些层才算有效放置面，如地形/高台；空 = 跳过本关）")]
    [SerializeField] private LayerMask placeableGroundLayers;

    [Tooltip("幽灵预览透明度系数（GeneralToonyShader 幽灵板块 _GhostAlpha）")]
    [SerializeField] private float ghostAlpha = 0.5f;

    [Tooltip("不可放置时幽灵主色调（乘到材质 _Color 上的红色调；恢复可放置即还原基色）")]
    [SerializeField] private Color ghostInvalidTint = new Color(1f, 0.35f, 0.35f);

    [Header("Scene Refs")]
    [Tooltip("守点（索敌锚点：敌人意图目标 / 守点判定；Transform 拖拽，与出怪点同形式）")]
    [SerializeField] private Transform guardPoint;

    [Tooltip("敌人创建间隔（秒）：同批到点敌人的依次创建节奏")]
    [SerializeField] private float spawnInterval = 1.5f;

    [Tooltip("出怪点绑定表（WaveTable.enemyCreatePointId → 场景位置）")]
    [SerializeField] private List<SpawnPointBinding> spawnPoints = new List<SpawnPointBinding>();

    /// <summary>
    /// 敌人行进路线：波次行经 routeId 引用；路点为世界坐标有序序列，敌人沿折线依次走
    /// （严格走点、无视障碍，聚堆为塔防设计预期），走完最后路点后直奔守点。
    /// Scene 视图 Gizmos 可视化（路点小球+连线），每条路线独立开关。
    /// </summary>
    [Serializable]
    public sealed class EnemyRoute
    {
        [Tooltip("路线 id（WaveTable.routeId 引用）")]
        public string routeId;

        [Tooltip("是否在 Scene 视图绘制本路线（路点小球+相邻连线）")]
        public bool showGizmos = true;

        [Tooltip("路点序列（世界坐标，有序；走完转守点）")]
        public List<Vector3> points = new List<Vector3>();
    }

    [Tooltip("敌人行进路线表（WaveTable.routeId → 有序路点；Inspector 手摆坐标，Scene 视图 Gizmos 预览）")]
    [SerializeField] private List<EnemyRoute> routes = new List<EnemyRoute>();

    [Tooltip("战斗视图父节点；可空（不挂父级）")]
    [SerializeField] private Transform viewRoot;

    // 单位视图 prefab 的 Addressables 标签通道（id 即地址；角色 Prefab-Unit-Character-Combat 组 / 敌人 Prefab-Unit-Enemy 组）
    private static readonly string[] UnitViewLabels = { "Combat" };

    // 时间比较容差（浮点累计时钟防边缘漏拍）
    private const float TimeEpsilon = 0.0001f;

    // 出生点网面判定半径（NavMesh.SamplePosition 水平找网面；大于此距离视为网面外，代理保持禁用）
    private const float SpawnNavMeshSampleRadius = 1f;

    private readonly struct SpawnRequest
    {
        public readonly string EnemyId;
        public readonly int Level;
        public readonly Vector3 Position;
        public readonly IReadOnlyList<Vector3> Route;
        public SpawnRequest(string enemyId, int level, Vector3 position, IReadOnlyList<Vector3> route)
        {
            EnemyId = enemyId; Level = level; Position = position; Route = route;
        }
    }

    // —— 波次驱动状态（Init 注入/推进）——
    private IReadOnlyList<WaveGroup> groups;
    private int groupIndex;
    private float waveClock;
    private int[] cursors = Array.Empty<int>();
    private readonly Queue<SpawnRequest> pending = new Queue<SpawnRequest>();
    private float spawnTimer;
    private readonly HashSet<string> missingPoints = new HashSet<string>();
    private readonly HashSet<string> missingRoutes = new HashSet<string>();

    // —— 装配产物/上下文（guardPoint 场景序列化引用；控制器/名单 Start 装配注入）——
    private BattleController controller;
    private IReadOnlyList<string> roster = Array.Empty<string>();
    private BattleWorldRuntimeData worldData;

    // —— 视图桥状态 ——
    // 单位 → 视图实例（敌人出表销毁；角色倒地留场）
    private readonly Dictionary<BattleUnitState, GameObject> views
        = new Dictionary<BattleUnitState, GameObject>();
    // 单位 id → prefab 租约（战斗生命周期共享，同 id 多实例共用一份；OnDestroy 统一释放）
    private readonly Dictionary<string, ResourceLease<GameObject>> viewPrefabLeases
        = new Dictionary<string, ResourceLease<GameObject>>();
    // 已出表但视图仍在首载的单位（视图就位后立即销毁，防幽灵尸体）
    private readonly HashSet<BattleUnitState> removedWithoutView = new HashSet<BattleUnitState>();

    /// <summary>装配完成的控制器（HUD / 结算等后续接入口；装配未完成时 null）。</summary>
    public BattleController Controller => controller;

    /// <summary>
    /// 视图实例反查单位（调试面板点击选中用；views 线性反查——部署单位个位数，无需索引）。
    /// </summary>
    public bool TryGetUnitByView(GameObject view, out BattleUnitState unit)
    {
        unit = null;
        // 命中碰撞体随 prefab 挂在骨盆等子骨骼上（非根）：沿父链向上找注册的根视图。
        // 点击是单帧事件非热路径，链长 = 骨骼深度，可接受。
        for (GameObject node = view; node != null; node = node.transform.parent != null ? node.transform.parent.gameObject : null)
        {
            foreach (KeyValuePair<BattleUnitState, GameObject> pair in views)
            {
                if (pair.Value == node)
                {
                    unit = pair.Key;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>装配用的部署名单（编队解析后序；装配完成前为 null）。</summary>
    public IReadOnlyList<string> DeployRoster => roster;

    /// <summary>守点位置（索敌锚点；Transform 拖拽实时取值）。</summary>
    public Vector3 GuardPosition => guardPoint != null ? guardPoint.position : Vector3.zero;

    /// <summary>守点当前血量。</summary>
    public int GuardHp => controller != null ? controller.Snapshot.GuardPointHp : 0;

    /// <summary>守点最大血量。</summary>
    public int GuardMaxHp => controller != null ? controller.Snapshot.GuardPointMaxHp : 0;

    /// <summary>当前点数。</summary>
    public int Points => controller != null ? controller.Ledger.Current : 0;

    /// <summary>距下次点数补给的秒数（到顶恒 0）。</summary>
    public float SecondsToNextSupply => controller != null ? controller.Ledger.SecondsToNextSupply : 0f;

    /// <summary>模拟是否暂停（暂停时 ③ 不推进 + timeScale 冻结表现层）。</summary>
    public bool SimulationPaused { get; private set; }

    public int TotalWaves => groups?.Count ?? 0;

    // 当前波号（1 基；全部生成完停在最后一波）
    public int CurrentWaveNumber => groups == null ? 0 : Math.Min(groupIndex + 1, groups.Count);

    public bool AllSpawned => groups != null && groupIndex >= groups.Count;

    private void Awake()
    {
        Current = this;
    }

    // ---------- 装配（Start 触发，⑥ 编排） ----------

    private async void Start()
    {
        // 选中态（BattleSelection）一场战斗一份：Start 创建 / OnDestroy 释放。
        // 点击选中单位 + UnitStatPanel 副面板共用（选中比面板长寿，面板关闭后可重开）。
        if (BattleSelection.Current == null) new BattleSelection();
        try
        {
            string[] deployIds = ResolveDeployUnitIds();
            roster = deployIds;
            controller = await AssembleAsync(deployIds);
            if (controller == null)
            {
                enabled = false;
            }
            // 不做自动部署：角色全部由玩家在 HUD 底栏拖卡片手动放置（EndDeployDrag → SubmitDeploy）
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CombatManager] 战斗装配异常：{ex}", this);
            enabled = false;
        }
    }

    // 部署名单解析：唯一来源 = 编队管理器当前编队（战备面板保存；UI Finish 已对空编队守门，
    // 此处报错为纵深防御）。
    private string[] ResolveDeployUnitIds()
    {
        FormationTeamManager formation = FormationTeamManager.Instance;
        List<string> teamUnitIds = formation.GetTeamUnitIds(formation.CurrentTeamIndex);
        if (teamUnitIds.Count == 0)
        {
            Debug.LogError("[CombatManager] 编队为空（战备面板未编入角色）。", this);
            return Array.Empty<string>();
        }
        return teamUnitIds.ToArray();
    }

    // 装配链：数据预热 → 打包 → 控制器组装 → 门面绑定 → 事件桥 → HUD。失败返回 null（错误已定位）。
    // 波次序列查 StageTable（PreloadAsync 已随表域加载，此处命中 DataTableManager 缓存）。
    private async Task<BattleController> AssembleAsync(string[] deployIds)
    {
        if (levelConfig == null)
        {
            Debug.LogError("[CombatManager] levelConfig 未绑定（SO 拖拽）。", this);
            return null;
        }
        string stageId = levelConfig.StageId;
        if (string.IsNullOrEmpty(stageId))
        {
            Debug.LogError("[CombatManager] LevelConfig.stageId 未配置。", levelConfig);
            return null;
        }
        if (guardPoint == null)
        {
            Debug.LogError("[CombatManager] 守点 guardPoint 未绑定（Transform 拖拽）。", this);
            return null;
        }
        if (deployIds == null || deployIds.Length == 0)
        {
            Debug.LogError("[CombatManager] 部署名单为空。", this);
            return null;
        }

        if (!await BattleDataModule.PreloadAsync(deployIds, stageId))
        {
            Debug.LogError("[CombatManager] 数据预热失败（缺失项见上方 BattleData 报错）。", this);
            return null;
        }

        // 波次序列：levelConfig.StageId 查 StageTable（GetWaveGroups 内部做关卡行缺失/波次行引用报错）
        IReadOnlyList<WaveGroup> waveGroups = BattleDataModule.GetWaveGroups(stageId);
        BattleDataPack pack = BuildPack(deployIds, waveGroups);
        // 胜负策略：SO 关卡级注入（普通关=守点默认实现资产）；未配置时兜底 new GuardPointEvaluator
        BattleOutcomeEvaluator evaluator = levelConfig.EvaluatorAsset != null
            ? levelConfig.EvaluatorAsset.CreateEvaluator()
            : new GuardPointEvaluator();

        Init(waveGroups);
        var ledger = new PointLedger(levelConfig.InitialPoints, levelConfig.PointSupplyInterval,
            levelConfig.PointSupplyAmount, levelConfig.PointCap);

        var assembled = new BattleController(
            new GuardPointState(levelConfig.GuardPointMaxHp), guardPoint.position,
            ledger, this, pack, evaluator);

        // 场内注册表：清上一场残留 + 登记守点（子弹锁头/攻击转向打守点取 head 世界位）
        FieldUnitRegistry.Clear();
        FieldUnitRegistry.RegisterGuardPoint(guardPoint);

        Bind(assembled, deployIds);
        assembled.OutcomeDecided += outcome => Debug.Log($"[CombatManager] 胜负判定：{outcome}");

        BridgeBattleEvents(assembled);

        // HUD 随装配完成打开（面板自带 Addressables 加载；装配失败不会走到这里）。
        // 仅运行时打开：EditMode 验证链下 PanelManager 单例链会触发 DontDestroyOnLoad 直接抛异常。
        if (Application.isPlaying)
        {
            PanelManager.Instance.OpenPanel<CombatPanel>(UIPath.COMBATPANEL, UILayer.Mid);
        }

        return assembled;
    }

    // 绑定战斗上下文并接管视图桥（控制器创建后调用一次）：此后暴露快速访问门面，
    // 出生/出表事件驱动视图生命周期。重复 Bind 先解绑旧控制器。
    private void Bind(BattleController battleController, IReadOnlyList<string> deployRoster)
    {
        if (controller != null)
        {
            controller.UnitSpawned -= HandleUnitSpawned;
            controller.EnemyRemoved -= HandleEnemyRemoved;
        }
        controller = battleController ?? throw new ArgumentNullException(nameof(battleController));
        roster = deployRoster ?? Array.Empty<string>();
        worldData = new BattleWorldRuntimeData(controller);
        controller.UnitSpawned += HandleUnitSpawned;
        controller.EnemyRemoved += HandleEnemyRemoved;
    }

    // ⑥ 打包：预热结果 → ①→③ 数据包。角色来自部署名单（带部署费用，CharacterTable.Cost 直查不改值）；
    // 波次时间点条目敌人按 (id, 等级) 去重补入 Stats/NormalAttacks（键 = id@等级；
    // pack 是 ③ 唯一数据源，漏打敌人则波次无法出兵）。
    private BattleDataPack BuildPack(string[] unitIds, IReadOnlyList<WaveGroup> waveGroups)
    {
        var pack = new BattleDataPack();
        var characters = DataTableManager.Instance
            .LoadTableAsync<CharacterRow, string>("CharacterTable").GetAwaiter().GetResult();

        for (int i = 0; i < unitIds.Length; i++)
        {
            string unitId = unitIds[i];
            if (BattleDataModule.TryGetUnitStats(unitId, out BattleUnitStats stats))
            {
                pack.Stats[unitId] = stats;
            }
            if (BattleDataModule.TryGetNormalAttack(unitId, out BattleDataModule.NormalAttackData normalAttack))
            {
                pack.NormalAttacks[unitId] = normalAttack;
            }
            if (characters != null && characters.TryGet(unitId, out CharacterRow row))
            {
                pack.DeployCosts[unitId] = row.Cost;
            }
        }

        var packed = new HashSet<string>(unitIds);
        for (int g = 0; g < waveGroups.Count; g++)
        {
            IReadOnlyList<WaveRow> rows = waveGroups[g].Rows;
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<WaveSpawnEntry> points = rows[r].TimePoints;
                for (int p = 0; p < points.Count; p++)
                {
                    for (int u = 0; u < points[p].Units.Count; u++)
                    {
                        string enemyId = points[p].Units[u].EnemyId;
                        // 敌人按 (id, 等级) 去重打包，键 = id@等级（同 id 多等级各一份）
                        string statsKey = string.IsNullOrEmpty(enemyId)
                            ? null
                            : BattleDataModule.EnemyCacheKey(enemyId, points[p].Units[u].Level);
                        if (statsKey == null || !packed.Add(statsKey)) continue;
                        if (BattleDataModule.TryGetUnitStats(statsKey, out BattleUnitStats stats))
                        {
                            pack.Stats[statsKey] = stats;
                        }
                        if (BattleDataModule.TryGetNormalAttack(statsKey, out BattleDataModule.NormalAttackData normalAttack))
                        {
                            pack.NormalAttacks[statsKey] = normalAttack;
                        }
                    }
                }
            }
        }
        return pack;
    }

    // ③ 模拟域事件 → EventCenter 广播（载荷：点数 int / 1 基波号 int / BattleOutcome）；初值先行落一次。
    private static void BridgeBattleEvents(BattleController assembled)
    {
        assembled.PointsChanged += points =>
            EventCenter.Instance.SetEventTrigger(EventNames.BattlePointChanged, points);
        assembled.WaveChanged += wave =>
            EventCenter.Instance.SetEventTrigger(EventNames.BattleWaveChanged, wave);
        assembled.OutcomeDecided += outcome =>
            EventCenter.Instance.SetEventTrigger(EventNames.BattleOutcomeDecided, outcome);

        EventCenter.Instance.SetEventTrigger(EventNames.BattlePointChanged, assembled.Ledger.Current);
        EventCenter.Instance.SetEventTrigger(EventNames.BattleWaveChanged, assembled.CurrentWaveNumber);
    }

    /// <summary>切换模拟暂停：③ 停止 Step + timeScale 冻结表现层（动画/寻路随帧冻结）。</summary>
    public void TogglePause()
    {
        SimulationPaused = !SimulationPaused;
        Time.timeScale = SimulationPaused ? 0f : 1f;
        Debug.Log($"[CombatManager] 模拟暂停：{SimulationPaused}");
    }

    // 天赋视图桥：等待 GAS 的 Buff/Behavior 能力就绪后挂展示 buff / 播天赋 clip（异步就绪，逐帧重试）
    private readonly Dictionary<BattleUnitState, GameplayAbilitySystem> pendingTalentViews =
        new Dictionary<BattleUnitState, GameplayAbilitySystem>();
    private readonly Dictionary<BattleUnitState, GameplayAbilitySystem> pendingTalentClips =
        new Dictionary<BattleUnitState, GameplayAbilitySystem>();
    private readonly HashSet<BattleUnitState> talentClipPlayed = new HashSet<BattleUnitState>();

    // 每帧推进模拟（暂停或胜负已定不调 Step）。
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) ToggleHsmDebugPanel();

        TrySyncTalentViews();

        if (!SimulationPaused && controller != null && controller.Outcome == BattleOutcome.Ongoing)
        {
            controller.Step(Time.deltaTime);
        }
    }

    // 天赋视图就绪同步：Buff 键就绪 → 挂展示 buff；Behavior 键就绪且是 CH0221 → 播一次天赋 clip
    private void TrySyncTalentViews()
    {
        if (controller == null) return;

        List<BattleUnitState> done = null;
        foreach (KeyValuePair<BattleUnitState, GameplayAbilitySystem> pair in pendingTalentViews)
        {
            BattleUnitState unit = pair.Key;
            GameplayAbilitySystem gas = pair.Value;
            if (unit == null || gas == null || gas.OwnerContext == null)
            {
                (done ??= new List<BattleUnitState>()).Add(unit);
                continue;
            }

            if (gas.OwnerContext.TryGet(AbilityRuntimeDataType.Buff, out BuffRuntimeData buffData))
            {
                controller.Talents?.AttachView(unit, buffData);
                (done ??= new List<BattleUnitState>()).Add(unit);
            }
        }
        if (done != null)
        {
            for (int i = 0; i < done.Count; i++) pendingTalentViews.Remove(done[i]);
        }

        done = null;
        foreach (KeyValuePair<BattleUnitState, GameplayAbilitySystem> pair in pendingTalentClips)
        {
            BattleUnitState unit = pair.Key;
            GameplayAbilitySystem gas = pair.Value;
            if (unit == null || gas == null || gas.OwnerContext == null || talentClipPlayed.Contains(unit))
            {
                (done ??= new List<BattleUnitState>()).Add(unit);
                continue;
            }
            if (unit.UnitId != "CH0221") continue;
            // Behavior 就绪后置一次性请求，由 HSM Talent 态播放并消费（不在表现层直接 Play，避免绕过完成度门控）
            if (!gas.OwnerContext.TryGet(AbilityRuntimeDataType.Behavior, out BehaviorAbilityRuntimeData _)) continue;

            talentClipPlayed.Add(unit);
            unit.WantTalent = true;
            (done ??= new List<BattleUnitState>()).Add(unit);
        }
        if (done == null) return;
        for (int i = 0; i < done.Count; i++) pendingTalentClips.Remove(done[i]);
    }

    /// <summary>F1 切换 HSM 状态调试面板（GetPanel 探测 → 开/关；打开层 Top）。</summary>
    private void ToggleHsmDebugPanel()
    {
        if (PanelManager.Instance.GetPanel<HsmDebugPanel>(UIPath.HSMDEBUGPANEL) != null)
        {
            PanelManager.Instance.ClosePanel(UIPath.HSMDEBUGPANEL);
        }
        else
        {
            PanelManager.Instance.OpenPanel<HsmDebugPanel>(UIPath.HSMDEBUGPANEL, UILayer.Top);
        }
    }

    private void OnDestroy()
    {
        BattleSelection.Current?.Dispose();
        if (Current == this) Current = null;
        if (SimulationPaused) Time.timeScale = 1f;
        CleanupDrag(); // 拖动中途组件销毁：清幽灵预览与挂起状态
        if (controller != null)
        {
            controller.UnitSpawned -= HandleUnitSpawned;
            controller.EnemyRemoved -= HandleEnemyRemoved;
            controller.Dispose();
        }
        pendingTalentViews.Clear();
        pendingTalentClips.Clear();
        talentClipPlayed.Clear();
        foreach (ResourceLease<GameObject> lease in viewPrefabLeases.Values)
        {
            lease?.Dispose();
        }
        viewPrefabLeases.Clear();
        views.Clear();
        FieldUnitRegistry.Clear();
        removedWithoutView.Clear();
        // 战斗缓存生命周期 = 一场战斗（场景卸载/组件销毁时失效）。
        BattleDataModule.Clear();
    }

    // ---------- 波次驱动（IBattleWaveDriver） ----------

    /// <summary>装配波次数据（重复调用重置时钟与队列）。</summary>
    public void Init(IReadOnlyList<WaveGroup> waveGroups)
    {
        groups = waveGroups ?? throw new ArgumentNullException(nameof(waveGroups));
        groupIndex = 0;
        waveClock = 0f;
        spawnTimer = 0f;
        pending.Clear();
        missingPoints.Clear();
        cursors = groups.Count > 0 ? new int[groups[0].Rows.Count] : Array.Empty<int>();
    }

    /// <summary>推进一拍：波内时钟 + 到点条目入队 + FIFO 逐只出 + 波完成切波。</summary>
    public void Tick(float dt, Action<string, int, Vector3, IReadOnlyList<Vector3>> spawnOne)
    {
        if (groups == null || groupIndex >= groups.Count) return;

        WaveGroup group = groups[groupIndex];
        bool queueWasEmpty = pending.Count == 0;

        // 1) 波内时钟推进 + 到点条目整队入队（表内书写顺序）
        waveClock += dt;
        bool allTriggered = true;
        for (int r = 0; r < group.Rows.Count; r++)
        {
            WaveRow row = group.Rows[r];
            IReadOnlyList<WaveSpawnEntry> points = row.TimePoints;
            int cursor = cursors[r];
            Vector3 position = ResolvePoint(row.EnemyCreatePointId);
            IReadOnlyList<Vector3> route = ResolveRoute(row.RouteId);

            while (cursor < points.Count && points[cursor].Time <= waveClock + TimeEpsilon)
            {
                WaveSpawnEntry entry = points[cursor];
                for (int u = 0; u < entry.Units.Count; u++)
                {
                    WaveSpawnEntry.WaveSpawnUnit unit = entry.Units[u];
                    for (int n = 0; n < unit.Count; n++)
                        pending.Enqueue(new SpawnRequest(unit.EnemyId, unit.Level, position, route));
                }
                cursor++;
            }

            cursors[r] = cursor;
            if (cursor < points.Count) allTriggered = false;
        }

        // 2) FIFO 逐只出：本轮新入队（队列原为空）时首只立即出，其后按 spawnInterval 节奏
        if (pending.Count > 0)
        {
            if (queueWasEmpty) spawnTimer = 0f;
            spawnTimer -= dt;
            while (pending.Count > 0 && spawnTimer <= 0f)
            {
                SpawnRequest request = pending.Dequeue();
                spawnOne?.Invoke(request.EnemyId, request.Level, request.Position, request.Route);
                spawnTimer += spawnInterval;
            }
        }

        // 3) 波完成判定：全部条目已触发且创建队列已排空 → 切下波（时钟归零）
        if (allTriggered && pending.Count == 0)
        {
            groupIndex++;
            waveClock = 0f;
            cursors = groupIndex < groups.Count ? new int[groups[groupIndex].Rows.Count] : Array.Empty<int>();
        }
    }

    // 出怪点解析（实时取 Transform 位置）；未命中报错一次并落 Vector3.zero
    private Vector3 ResolvePoint(string pointId)
    {
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            SpawnPointBinding binding = spawnPoints[i];
            if (binding != null && binding.point != null && binding.pointId == pointId)
                return binding.point.position;
        }
        if (missingPoints.Add(pointId))
            Debug.LogError($"[CombatManager] 出怪点未绑定：pointId={pointId}（请检查 CombatManager.spawnPoints）", this);
        return Vector3.zero;
    }

    // 路线解析（routeId → 路点序列，出兵时随 SpawnRequest 透传）；空 routeId = 无路线（直奔守点），
    // 非空未绑定报错一次并落 null（不拒出兵——拒了波次会卡死，兜底语义与 ResolvePoint 一致）
    private IReadOnlyList<Vector3> ResolveRoute(string routeId)
    {
        if (string.IsNullOrEmpty(routeId)) return null;
        for (int i = 0; i < routes.Count; i++)
        {
            EnemyRoute route = routes[i];
            if (route != null && route.routeId == routeId) return route.points;
        }
        if (missingRoutes.Add(routeId))
            Debug.LogError($"[CombatManager] 路线未绑定：routeId={routeId}（请检查 CombatManager.routes）", this);
        return null;
    }

    // Scene 视图可视化：部署区域盒 + 拖动中判定半径球（绿=可放/红=拒绝）+ 路线
    private void OnDrawGizmos()
    {
        DrawDeployAreaGizmos();
        DrawRouteGizmos();
    }

    // 部署区域：两点对角线框盒（与放置校验①同几何）+ 角点小球；
    // 拖动中叠加判定线（主相机 → 判定点，即落点映射射线的实际走向）+ 判定点竖线与半径球着色
    private void DrawDeployAreaGizmos()
    {
        if (deployAreaPointA == null || deployAreaPointB == null) return;

        Vector3 min = Vector3.Min(deployAreaPointA.position, deployAreaPointB.position);
        Vector3 max = Vector3.Max(deployAreaPointA.position, deployAreaPointB.position);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
        // 底面矩形加亮：放置判定实际看的地面足迹（透视下顶面投影会朝相机方向外扩，别按顶面轮廓目测）
        Gizmos.color = new Color(0.4f, 1f, 1f);
        Gizmos.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z));
        Gizmos.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z));
        Gizmos.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z));
        Gizmos.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z));
        Gizmos.DrawSphere(deployAreaPointA.position, 0.15f);
        Gizmos.DrawSphere(deployAreaPointB.position, 0.15f);

        if (Application.isPlaying && draggingUnitId != null)
        {
            // 判定线（白）：主相机射线原点 → 判定点——落点就是这条线与地面层几何的交点；
            // 末端延伸一截灰线示意射线继续前进的方向
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 origin = cam.transform.position;
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(origin, 0.12f);
                Gizmos.DrawLine(origin, debugCheckPoint);
                Gizmos.color = Color.gray;
                Vector3 dir = (debugCheckPoint - origin).normalized;
                Gizmos.DrawLine(debugCheckPoint, debugCheckPoint + dir * 2f);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(debugCheckPoint, debugCheckPoint + Vector3.up * 3f);
            Gizmos.color = debugCheckDeployable ? Color.green : Color.red;
            Gizmos.DrawWireSphere(debugCheckPoint, deployCheckRadius);
        }
    }

    // 路线可视化：路点小球+相邻连线，每条路线独立开关，颜色按索引错开
    private void DrawRouteGizmos()
    {
        if (routes == null) return;
        for (int i = 0; i < routes.Count; i++)
        {
            EnemyRoute route = routes[i];
            if (route == null || !route.showGizmos || route.points == null) continue;
            Gizmos.color = Color.HSVToRGB((i * 0.35f) % 1f, 0.75f, 1f);
            for (int p = 0; p < route.points.Count; p++)
            {
                Gizmos.DrawWireSphere(route.points[p], 0.2f);
                if (p > 0) Gizmos.DrawLine(route.points[p - 1], route.points[p]);
            }
        }
    }

    // ---------- 编队卡拖放（HUD 底栏 → 世界放置；HUD 管屏幕侧，这里管世界侧） ----------

    // 已放置角色（不可重放；生命周期 = 一场战斗，重装配即新开）
    private readonly HashSet<string> deployedUnitIds = new HashSet<string>();
    // 拖动状态：当前拖动角色 id（null = 未拖动）、幽灵预览实例、拖动序号（作废挂起的异步首载）
    private string draggingUnitId;
    private GameObject dragPreview;
    private int dragSequence;
    // 障碍球检测复用缓冲（避免拖动逐帧分配）
    private static readonly Collider[] deployOverlapBuffer = new Collider[32];

    // 放置判定调试态（拖动中逐帧刷新）：最近判定点/三关是否全过/未通过原因——Gizmos 着色与失败日志用
    private Vector3 debugCheckPoint;
    private bool debugCheckDeployable;
    private string debugCheckReason = "未拖动";

    /// <summary>该角色是否已放置过（放置过的卡片不可再次拖出）。</summary>
    public bool IsUnitDeployed(string unitId) => deployedUnitIds.Contains(unitId);

    /// <summary>
    /// 开始拖动编队卡：按 id 拉起战斗视图同款幽灵预览（异步首载，失败仅无预览不挡放置）。
    /// 重复调用（已在拖动中）忽略；拖动已结束则丢弃迟到的预览。
    /// </summary>
    public async void BeginDeployDrag(string unitId)
    {
        if (controller == null || string.IsNullOrEmpty(unitId) || draggingUnitId != null) return;
        draggingUnitId = unitId;
        debugCheckReason = "等待地面映射";
        int seq = ++dragSequence;

        GameObject prefab = await LoadViewPrefabAsync(unitId);
        if (seq != dragSequence || prefab == null || draggingUnitId == null) return;

        dragPreview = Instantiate(prefab, prefab.transform.position, Quaternion.identity, viewRoot);
        dragPreview.name = $"DeployPreview_{unitId}";
        StripGhostBehaviours(dragPreview);
        ApplyGhostMaterials(dragPreview);
    }

    /// <summary>拖动中更新幽灵预览位置（HUD 射线落到战场落点后的世界点）；
    /// 同帧刷新放置判定调试态（Gizmos 半径球着色跟随），判定结果变化时落一条 Debug（逐帧去重防刷屏）。</summary>
    public void UpdateDeployDrag(Vector3 worldPoint)
    {
        if (dragPreview != null) dragPreview.transform.position = worldPoint;

        string previousReason = debugCheckReason;
        EvaluateDeploy(worldPoint, out debugCheckReason);
        if (debugCheckReason != previousReason)
        {
            Debug.Log($"[CombatManager] 放置判定：{debugCheckReason}（point={worldPoint}）", this);
        }
        if (dragPreview != null) SetGhostInvalid(!debugCheckDeployable);
    }

    /// <summary>
    /// 结束拖动并尝试放置：通过放置校验才 SubmitDeploy（消耗点数、发 UnitSpawned 走视图桥）。
    /// 返回 false = 校验失败或被拒（点数不足/名额/白名单），卡片保留可再拖；校验未通过原因落日志。
    /// </summary>
    public bool EndDeployDrag(Vector3 worldPoint)
    {
        string unitId = draggingUnitId;
        bool deployable = EvaluateDeploy(worldPoint, out string reason);
        CleanupDrag();
        if (unitId == null) return false;

        if (!deployable)
        {
            Debug.Log($"[CombatManager] '{unitId}' 放置校验未通过：{reason}（point={worldPoint}）", this);
            return false;
        }
        if (!controller.SubmitDeploy(unitId, worldPoint))
        {
            Debug.LogWarning($"[CombatManager] '{unitId}' 部署被拒（点数不足/名额/白名单），卡片保留。", this);
            return false;
        }
        deployedUnitIds.Add(unitId);
        return true;
    }

    /// <summary>取消拖动（玩家把卡片放回）：只清理预览，不提交放置。</summary>
    public void CancelDeployDrag() => CleanupDrag();

    // 拖动状态清理：作废挂起的首载（dragSequence 自增）+ 销毁幽灵预览
    private void CleanupDrag()
    {
        draggingUnitId = null;
        dragSequence++;
        if (dragPreview != null)
        {
            DestroyGhostMaterials(dragPreview);
            Destroy(dragPreview);
            dragPreview = null;
        }
    }

    /// <summary>
    /// 放置校验（全部通过才可放）：① 两点对角三维包围盒内；② 判定点半径内命中可放置地面层（白名单，
    /// mask 空跳过）；③ 球检测半径内无不可放置层障碍；④ 离已部署角色过近拒绝（模拟域位置为真值，
    /// 不依赖视图 collider）。允许放进敌人堆。
    /// </summary>
    public bool IsDeployable(Vector3 point) => EvaluateDeploy(point, out _);

    // 放置校验全量执行：三关逐关短路，原因与判定点落调试态（Gizmos 着色 + 失败日志用）。
    private bool EvaluateDeploy(Vector3 point, out string reason)
    {
        debugCheckPoint = point;
        debugCheckDeployable = false;

        if (controller == null || deployAreaPointA == null || deployAreaPointB == null)
        {
            reason = controller == null
                ? "战斗未就绪（控制器未装配）"
                : "部署区域角点未配置（deployAreaPointA/B）";
            return false;
        }

        // ① 区域盒（分量取两角点最小/最大，含边界）
        Vector3 min = Vector3.Min(deployAreaPointA.position, deployAreaPointB.position);
        Vector3 max = Vector3.Max(deployAreaPointA.position, deployAreaPointB.position);
        if (point.x < min.x || point.x > max.x
            || point.y < min.y || point.y > max.y
            || point.z < min.z || point.z > max.z)
        {
            reason = $"区域外（盒 {min} ~ {max}，点 {point}）";
            return false;
        }

        // ② 可放置地面层白名单：判定点半径内必须命中可放置层（点站在有效放置面上才可放；
        // mask 空 = 跳过本关，行为与未加白名单前一致）
        if (placeableGroundLayers.value != 0
            && Physics.OverlapSphereNonAlloc(
                point, deployCheckRadius, deployOverlapBuffer, placeableGroundLayers.value) == 0)
        {
            reason = $"半径 {deployCheckRadius} 内未命中可放置地面层（点不在有效放置面上）";
            return false;
        }

        // ③ 不可放置层障碍球检测：半径内命中即拒（LayerMask 过滤，可多选层）
        int hitCount = Physics.OverlapSphereNonAlloc(
            point, deployCheckRadius, deployOverlapBuffer, unputableLayers.value);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = deployOverlapBuffer[i];
            if (hit != null)
            {
                reason = $"半径 {deployCheckRadius} 内命中不可放置层物体：{hit.name}";
                return false;
            }
        }

        // ④ 与已部署角色过近（同半径）；敌人不查（设计允许放进敌人堆）
        float radiusSq = deployCheckRadius * deployCheckRadius;
        IReadOnlyList<BattleUnitState> units = controller.Units;
        for (int i = 0; i < units.Count; i++)
        {
            BattleUnitState unit = units[i];
            if (unit.Camp != BattleCamp.Character || !unit.IsAlive) continue;
            if ((unit.Position - point).sqrMagnitude <= radiusSq)
            {
                reason = $"离角色 {unit.UnitId} 过近（≤{deployCheckRadius}）";
                return false;
            }
        }

        reason = "可放置";
        debugCheckDeployable = true;
        return true;
    }

    // 幽灵材质实例缓存（Apply 填充 / Destroy 清空）：不可放置着色按「基色×色调」回写，非累乘不漂移
    private readonly List<Material> ghostMaterials = new List<Material>();
    private readonly List<Color> ghostBaseColors = new List<Color>();

    // 幽灵预览去逻辑化（纯视觉外壳）：销毁全部用户脚本 + NavMeshAgent + NavMeshObstacle + Rigidbody。
    // 角色 prefab 保留 NavMeshAgent——agent 的内部同步会把 transform 拉回 NavMesh 网面（地面→高台
    // 未连通即「预览卡在地面」），逻辑脚本也可能驱动移动；判定与放置只认 transform，预览不参与模拟。
    private void StripGhostBehaviours(GameObject root)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false; // 立即停 Update 入口（Destroy 帧末才生效）
            Destroy(behaviour);
        }
        foreach (NavMeshAgent agent in root.GetComponentsInChildren<NavMeshAgent>(true))
        {
            agent.enabled = false;
            Destroy(agent);
        }
        foreach (NavMeshObstacle obstacle in root.GetComponentsInChildren<NavMeshObstacle>(true))
        {
            obstacle.enabled = false; // 防预览在拖动中雕刻 NavMesh（敌人绕着幽灵走）
            Destroy(obstacle);
        }
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(body);
        }
    }

    // 幽灵化预览材质：实例化每个 Renderer 的材质（getter 即实例化），开 _GHOST_ON + 透明混合 + Transparent 队列，
    // 关投影（幽灵不落影）。GeneralToonyShader 的幽灵板块（默认关，仅预览实例打开）。
    // 顺带禁用全部 Collider：预览不参与物理（拖动落点射线不被自身命中、放置球检测不被误伤）。
    private void ApplyGhostMaterials(GameObject root)
    {
        ghostMaterials.Clear();
        ghostBaseColors.Clear();
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.materials;
            bool ghosted = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;
                // 无 _Color 的材质（如 Mobile/Particles/Additive 粒子）不可主色着色，跳过缓存防 get_color 报错
                if (mat.HasProperty("_Color"))
                {
                    ghostMaterials.Add(mat);
                    ghostBaseColors.Add(mat.color);
                }
                mat.EnableKeyword("_GHOST_ON");
                mat.SetFloat("_GhostAlpha", ghostAlpha);
                // SrcAlpha(5)/OneMinusSrcAlpha(10)：材质被改过混合时也强制拉回透明混合
                mat.SetFloat("_BlendSrc", 5f);
                mat.SetFloat("_BlendDst", 10f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                ghosted = true;
            }
            if (ghosted) renderer.materials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // 不可放置着色：幽灵材质主色 × 红色调（恢复可放置写回基色）；实例与基色缓存见 ApplyGhostMaterials
    private void SetGhostInvalid(bool invalid)
    {
        for (int i = 0; i < ghostMaterials.Count; i++)
        {
            Material mat = ghostMaterials[i];
            if (mat == null) continue;
            mat.color = invalid ? ghostBaseColors[i] * ghostInvalidTint : ghostBaseColors[i];
        }
    }

    // 销毁幽灵预览：.materials getter 产生的材质实例是孤本，须显式 Destroy 防泄漏
    private void DestroyGhostMaterials(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material mat in renderer.materials)
            {
                if (mat != null) Destroy(mat);
            }
        }
        ghostMaterials.Clear();
        ghostBaseColors.Clear();
    }

    // ---------- 视图桥 ----------

    // 单位出生：按 id 加载战斗视图（同 id 首载后缓存，命中同步），实例化并注册 ④ 能力数据入口。
    // 落地序列（2026-10-20 用户拍板「彻底落地前禁用 NavMesh/GAS，放置后再激活」）：激活实例化后
    // 立即冻结 → 摆回部署点 → 代理网面预置 → 再激活，能力首帧 Update 晚于全部预置。
    private async void HandleUnitSpawned(BattleUnitState unit)
    {
        if (controller == null) return;

        GameObject prefab = await LoadViewPrefabAsync(unit.UnitId);
        if (prefab == null) return; // 报错已定位（编辑态不拉 Addressables / Combat 组缺失）

        // 首载等待期已出表：直接丢弃，不落幽灵尸体
        if (removedWithoutView.Remove(unit)) return;

        // 落地序列：agent 随 prefab 激活注册会在 Instantiate 栈内即时 snap 到最近网面（未连通高台的
        // 出生点被拖到地面网面——「放置那一刻拽下高台」根因）。先激活实例化、随即冻结纠正：
        // SetActive(false) 令代理退出 NavMesh → 摆回部署点 → 网面判定预置代理 → 再激活。
        // 再激活时禁用代理不 snap；启用代理从部署点注册（就地落在网面上）。
        GameObject view = Instantiate(prefab, unit.Position, Quaternion.identity, viewRoot);
        view.name = $"{prefab.name}_{unit.UnitId}";
        view.SetActive(false);
        view.transform.position = unit.Position;
        PrefitSpawnNavAgent(view, unit.Position);
        // 命中碰撞体随 prefab 自带（骨盆骨骼手动挂），代码不补挂——CombatSelectionPicker 选中与子弹射线命中共用同一碰撞体
        view.SetActive(true);

        // 场内注册表：登记 head 节点（headNode 列 → 解析 Transform），供子弹锁头/攻击转向取目标 head 世界位。
        // 在 GAS 判定前登记——无能力数据的单位同样是被攻击目标，锁头不受影响。
        FieldUnitRegistry.Register(unit.UnitId, unit, ResolveHeadNode(view, unit.Stats.HeadNode));

        GameplayAbilitySystem gas = view.GetComponent<GameplayAbilitySystem>();
        if (gas == null || gas.OwnerContext == null)
        {
            Debug.LogError(
                $"[CombatManager] '{view.name}' 缺 GameplayAbilitySystem，④能力数据不注册（感知/寻路停摆）。", view);
            views[unit] = view;
            return;
        }

        gas.OwnerContext.Register(AbilityRuntimeDataType.BattleUnit, new BattleUnitRuntimeData(unit));
        gas.OwnerContext.Register(AbilityRuntimeDataType.BattleWorld, worldData);
        // 天赋展示/表现桥：Buff/Behavior 能力异步就绪后由 Update 逐帧补挂/补播
        pendingTalentViews[unit] = gas;
        pendingTalentClips[unit] = gas;
        talentClipPlayed.Remove(unit);
        // stat 数据由 prefab 能力列表上的 StatAbility 提供（能力就绪时自建自注册），②桥不代管——
        // 此处再注册会与能力注册撞重复键，勿补。
        views[unit] = view;
    }

    // 解析头部骨骼节点：headNode 列配置骨骼名或相对路径——Find 路径优先，未命中按名全扫（兼容两种配法）。
    // 解析失败返回 null（子弹锁头/攻击转向回退根位置/事件正向）。
    private Transform ResolveHeadNode(GameObject view, string headNode)
    {
        if (string.IsNullOrWhiteSpace(headNode)) return null;
        Transform found = view.transform.Find(headNode);
        if (found != null) return found;
        foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == headNode) return child;
        }
        return null;
    }

    // NavMeshAgent 网面预置（冻结窗口内调用，先于能力首帧）：出生点在 NavMesh 网面上才启用代理；
    // 不在（地面→高台未连通等）保持禁用——启用即注册 snap 到地面网面，视图被拽离部署点
    // （角色抽搐/卡地面根因）。烘焙侧加坡道/台阶或调可上高度后新部署自动启用。无代理（敌人路线制
    // 手动位移）直接跳过；角色缺代理由移动能力自行报错。
    private void PrefitSpawnNavAgent(GameObject view, Vector3 spawnPoint)
    {
        NavMeshAgent agent = view.GetComponent<NavMeshAgent>();
        if (agent == null) return;

        if (NavMesh.SamplePosition(spawnPoint, out NavMeshHit hit, SpawnNavMeshSampleRadius, NavMesh.AllAreas))
        {
            agent.enabled = true;
            return;
        }
        agent.enabled = false;
        Debug.LogWarning($"[CombatManager] '{view.name}' 出生点不在 NavMesh 网面上（高台未连通？），" +
            "NavMeshAgent 保持禁用、单位原地站稳；烘焙侧加坡道/台阶或调可上高度后新部署自动启用。", view);
    }

    // 按 id 加载战斗视图 prefab：id 即 Addressables 地址（Combat 标签交集，命中多条即报错）。
    // 仅运行时加载：EditMode 验证链下 Addressables 初始化依赖引擎帧泵（报错为预期噪音，不影响模拟域）。
    private async Task<GameObject> LoadViewPrefabAsync(string unitId)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError($"[CombatManager] '{unitId}' 视图仅在运行时按 id 加载（EditMode 不拉 Addressables），无表现。", this);
            return null;
        }
        if (viewPrefabLeases.TryGetValue(unitId, out ResourceLease<GameObject> cached))
        {
            return cached.Asset;
        }
        try
        {
            ResourceLease<GameObject> lease = await AddressableManager.Instance
                .AcquireAssetWithLabelsAsync<GameObject>(unitId, UnitViewLabels);
            viewPrefabLeases[unitId] = lease;
            return lease.Asset;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CombatManager] 战斗视图 prefab 加载失败：'{unitId}'" +
                $"（Combat 组未登记该 id 或标签缺失）。{ex.Message}", this);
            return null;
        }
    }

    // 敌人死亡出表：销毁对应视图 + 注销场内注册表（角色 Downed 不走此事件，留场）。
    private void HandleEnemyRemoved(BattleUnitState unit)
    {
        FieldUnitRegistry.Unregister(unit);
        if (!views.TryGetValue(unit, out GameObject view))
        {
            // 视图仍在首载等待期：登记后视图就位即销毁
            removedWithoutView.Add(unit);
            return;
        }
        views.Remove(unit);
        if (view != null) Destroy(view);
    }
}
