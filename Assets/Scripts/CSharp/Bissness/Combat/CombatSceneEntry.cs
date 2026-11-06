using System;
using System.Collections.Generic;
using Core.Gear;
using Framework.Gameplay.Battle;
using UnityEngine;

/// <summary>
/// 战斗场景入口（MonoBehaviour）：只承载场景拖拽引用，构建 CombatBattleConfig 后交给全局
/// CombatService；进入场景 Awake Reset、离开场景 OnDestroy EndBattle，保证数据干净。
/// 波次/视图/部署/选中/事件桥均由 CombatService 的子服务承担。
/// </summary>
public sealed class CombatSceneEntry : MonoBehaviour
{
    // 当前场景活动入口（防同场景重复实例双 Tick 推动同一单例）。
    private static CombatSceneEntry activeEntry;

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

    [Tooltip("敌人行进路线表（WaveTable.routeId → 有序路点；Inspector 手摆坐标，Scene 视图 Gizmos 预览）")]
    [SerializeField] private List<EnemyRoute> routes = new List<EnemyRoute>();

    [Tooltip("战斗视图父节点；可空（不挂父级）")]
    [SerializeField] private Transform viewRoot;

    // 本次装配的会话号（BeginBattleAsync 返回；0 = 未装配成功）。
    private int battleSession;

    // 入口是否已成功接管本场景。
    private bool started;

    private void Awake()
    {
        // 同场景重复入口：保留首个，其余禁用，避免双 Tick。
        if (activeEntry != null && activeEntry != this)
        {
            Debug.LogWarning("[CombatSceneEntry] 场景存在重复战斗入口，禁用后加入的实例。", this);
            enabled = false;
            return;
        }
        activeEntry = this;

        // 进入战斗场景先清一次，保证不继承上一场残留。
        CombatService.Instance.Reset();
    }

    private async void Start()
    {
        try
        {
            battleSession = await CombatService.Instance.BeginBattleAsync(BuildBattleConfig());
            if (battleSession == 0)
            {
                enabled = false;
                return;
            }
            started = true;

            // HUD 随装配完成打开（仅运行时；EditMode 验证链下 PanelManager 单例链会抛异常）
            if (Application.isPlaying)
            {
                PanelManager.Instance.OpenPanel<CombatPanel>(UIPath.COMBATPANEL, UILayer.Mid);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CombatSceneEntry] 战斗装配异常：{exception}", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) ToggleHsmDebugPanel();
        CombatService.Instance.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        // 只结束自己装配的会话；旧场景迟到拆除不会 reset 新战斗。
        CombatService.Instance.EndBattle(started ? battleSession : 0);
        if (activeEntry == this) activeEntry = null;
    }

    // 从序列化字段构建场景配置（字段名/结构与场景已保存数据一致）。
    private CombatBattleConfig BuildBattleConfig()
    {
        return new CombatBattleConfig
        {
            LevelConfig = levelConfig,
            GuardPoint = guardPoint,
            ViewRoot = viewRoot,
            DeployAreaPointA = deployAreaPointA,
            DeployAreaPointB = deployAreaPointB,
            DeployCheckRadius = deployCheckRadius,
            UnputableLayers = unputableLayers,
            PlaceableGroundLayers = placeableGroundLayers,
            GhostAlpha = ghostAlpha,
            GhostInvalidTint = ghostInvalidTint,
            SpawnInterval = spawnInterval,
            SpawnPoints = spawnPoints,
            Routes = routes,
        };
    }

    /// <summary>F5 切换 HSM 状态调试面板（GetPanel 探测 → 开/关；打开层 Top）。</summary>
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

    // Scene 视图可视化：部署区域盒 + 拖动中判定半径球（绿=可放/红=拒绝）+ 路线
    private void OnDrawGizmos()
    {
        DrawDeployAreaGizmos();
        DrawRouteGizmos();
    }

    // 部署区域：两点对角线框盒 + 角点小球 + 拖动中判定线/半径球
    private void DrawDeployAreaGizmos()
    {
        if (deployAreaPointA == null || deployAreaPointB == null) return;

        Vector3 min = Vector3.Min(deployAreaPointA.position, deployAreaPointB.position);
        Vector3 max = Vector3.Max(deployAreaPointA.position, deployAreaPointB.position);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
        Gizmos.color = new Color(0.4f, 1f, 1f);
        Gizmos.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z));
        Gizmos.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z));
        Gizmos.DrawLine(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z));
        Gizmos.DrawLine(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z));
        Gizmos.DrawSphere(deployAreaPointA.position, 0.15f);
        Gizmos.DrawSphere(deployAreaPointB.position, 0.15f);

        if (!Application.isPlaying) return;
        CombatDeployService deploy = CombatService.Instance.Deploy;
        if (deploy == null || string.IsNullOrEmpty(deploy.DraggingUnitId)) return;

        Vector3 checkPoint = deploy.DebugCheckPoint;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 origin = cam.transform.position;
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(origin, 0.12f);
            Gizmos.DrawLine(origin, checkPoint);
            Gizmos.color = Color.gray;
            Vector3 dir = (checkPoint - origin).normalized;
            Gizmos.DrawLine(checkPoint, checkPoint + dir * 2f);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(checkPoint, checkPoint + Vector3.up * 3f);
        Gizmos.color = deploy.DebugCheckDeployable ? Color.green : Color.red;
        Gizmos.DrawWireSphere(checkPoint, deployCheckRadius);
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
}
