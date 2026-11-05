using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Battle;
using Framework.Gameplay.Item;
using Framework.Gameplay.Player;
using Framework.Gameplay.Progression;
using Framework.Gameplay.Unit;
using UnityEngine;

public class Preloader : MonoBehaviour
{
    /// <summary>池预热条目：一个常用池化预制体 + 开局备用的实例数量。</summary>
    [System.Serializable]
    public class PoolPrewarmEntry
    {
        //预缓存的池化预制体（与业务面板拖的是同一个资产，引用注入）
        public GameObject prefab;

        //备用实例数量（池内不足时补足，已够则跳过）
        public int count = 8;
    }

    [Header("新账号发放")]
    //初始基础货币物品 id（ItemTable 主键，Inspector 可调；当前为信用点）
    [SerializeField] private string defaultBaseCurrencyItemId = "Currency_CreditPoint";

    [Header("池预热")]
    //常用池化控件预缓存清单（列表项/背包格等小件；开局实例化备用入池常驻，消除首次开板实例化尖峰）
    [SerializeField] private List<PoolPrewarmEntry> poolPrewarms = new List<PoolPrewarmEntry>();

    // 总进度阶段权重：场景加载为大头，配置预热次之（和不必为 1，按相对比例分摊）
    private const string StageScene = "scene";
    private const string StageConfig = "config";

    void Start()
    {
        //网络传输信息注册初始化
        MessageTool.Init();

        //养成事务中断恢复：上次养成操作未提交时按快照回滚（材料回来、养成不生效），
        //须在默认发放与任何读档 UI 之前；无遗留日志时零开销
        CharacterProgressionManager.RecoverInterruptedOp();

        //池预热：小件同步实例化（总量几十个、毫秒级），赶在任何面板开板前备好
        WarmObjectPools();

        //常驻通知面板（系统层）：开局即开、永不关闭，任何系统经 NoticeRequested 事件
        //或 GetPanel<NoticePanel>(UIPath.NOTICEPANEL)?.Submit() 提交提示
        PanelManager.Instance.OpenPanel<NoticePanel>(UIPath.NOTICEPANEL, UILayer.System);

        //新账号默认数据发放（无存档发默认角色与初始货币；老账号幂等跳过），须在任何 UI 读档前完成
        PlayerDataCenter.EnsureNewAccountDefaults(defaultBaseCurrencyItemId);

        //总进度汇聚（LoadProgressHub）：场景加载 + 配置预热统一进一根进度条，
        //经 EventNames.PreloadProgressChanged 广播，PreloadPanel 监听刷新
        LoadProgressHub.Instance.Reset();
        LoadProgressHub.Instance.Register(StageScene, 0.8f);
        LoadProgressHub.Instance.Register(StageConfig, 0.2f);
        EventCenter.Instance.Register<float>(EventNames.LoadSceneProgress, OnSceneLoadProgress);

        //配置缓存预热：后台把全部业务表解码为 DataTable（内部即字典，O(1) 查询），
        //玩家操作路径零首载等待；未预热成功时业务侧首次查询仍会自动加载，预热只是提前
        _ = WarmConfigCacheAsync();

        //打开加载面板；开场动画播放完毕后由面板回调，才开始异步加载主城场景
        PanelManager.Instance.OpenPanel<PreloadPanel>(UIPath.PRELOADERPANEL, UILayer.Top, panel =>
        {
            //开场动画（溶解入场）播完后再开始加载，避免动画未播完就切场景
            panel.AddDisplayFinishedListener(() =>
            {
                LoadSceneManager.Instance.LoadSceneAsync(SceneNames.Home, () =>
                {
                    //场景激活即卸载引导场景，本组件的进度中继（OnSceneLoadProgress）随之注销，
                    //框架在 isDone 后补发的最终 1f 广播无人中继，Hub 会卡在 0.8×0.9+0.2×1=92%——
                    //在存活的加载回调里补标场景阶段完成，总进度归一，PreloadPanel 收到 ≥1 关面板
                    LoadProgressHub.Instance.Complete(StageScene);
                    PanelManager.Instance.OpenPanel<HomePanel>(UIPath.HOMEPANEL, UILayer.Bot);
                });
            });
        });
    }

    /// <summary>
    /// 场景加载进度中继：LoadSceneManager 的逐帧广播汇入总进度（场景阶段）。
    /// </summary>
    private void OnSceneLoadProgress(float progress)
    {
        LoadProgressHub.Instance.Report(StageScene, progress);
    }

    /// <summary>
    /// 池预热：常用小控件（列表项/背包格等）开局实例化备用件入池常驻（ObjectsPool.Prewarm 幂等补足）。
    /// 预制体引用走 Inspector 注入；条目缺失或引用为空跳过，逐条告警不阻塞启动。
    /// </summary>
    private void WarmObjectPools()
    {
        for (int i = 0; i < poolPrewarms.Count; i++)
        {
            PoolPrewarmEntry entry = poolPrewarms[i];
            if (entry == null || entry.prefab == null || entry.count <= 0) continue;
            ObjectsPool.Instance.Prewarm(entry.prefab, entry.count);
        }
    }

    private void OnDestroy()
    {
        //事件中心为静态单例，不反注册会持有已销毁 Preloader 的回调
        EventCenter.Instance.Unregister<float>(EventNames.LoadSceneProgress, OnSceneLoadProgress);
    }

    /// <summary>
    /// 配置缓存预热：全部业务表在加载阶段就经 DataTableManager 解码缓存
    ///（Excel 导出的 MessagePack 表 → 内存字典，非 json 文本遍历），玩家操作路径零首载等待。
    /// 进度逐表汇入总进度（配置阶段，权重按表数均分）；失败只记日志并标记阶段完成，
    /// 业务侧会自动重试。
    /// </summary>
    private static async Task WarmConfigCacheAsync()
    {
        try
        {
            //预热清单：逐项惰性起载、串行 await，进度条随表数均分平滑推进
            (string table, Func<Task> load)[] warmups =
            {
                ("ItemTable", () => DataTableManager.Instance.LoadTableAsync<ItemRow, string>("ItemTable")),
                ("CharacterLevelTable", () => DataTableManager.Instance.LoadTableAsync<CharacterLevelRow, int>("CharacterLevelTable")),
                ("CharacterTable", () => DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable")),
                ("WeaponTable", () => DataTableManager.Instance.LoadTableAsync<WeaponRow, string>("WeaponTable")),
                ("BreakthroughTable", () => DataTableManager.Instance.LoadTableAsync<BreakthroughRow, int>("BreakthroughTable")),
                ("UnlockNodeTable", () => DataTableManager.Instance.LoadTableAsync<UnlockNodeRow, int>("UnlockNodeTable")),
                ("WeaponLevelTable", () => DataTableManager.Instance.LoadTableAsync<WeaponLevelRow, int>("WeaponLevelTable")),
                ("TrinketTable", () => DataTableManager.Instance.LoadTableAsync<TrinketRow, string>("TrinketTable")),
                ("EnsembleTable", () => DataTableManager.Instance.LoadTableAsync<EnsembleRow, string>("EnsembleTable")),
                ("CharacterSkillTable", () => DataTableManager.Instance.LoadTableAsync<CharacterSkillRow, int>("CharacterSkillTable")),
                ("CharacterSkillLevelTable", () => DataTableManager.Instance.LoadTableAsync<CharacterSkillLevelRow, int>("CharacterSkillLevelTable")),
                ("SkillEffectTable", () => DataTableManager.Instance.LoadTableAsync<SkillEffectRow, int>("SkillEffectTable")),
                ("EnemyTable", () => DataTableManager.Instance.LoadTableAsync<EnemyRow, string>("EnemyTable")),
                ("WaveTable", () => DataTableManager.Instance.LoadTableAsync<WaveRow, string>("WaveTable")),
            };
            for (int i = 0; i < warmups.Length; i++)
            {
                await warmups[i].load();
                LoadProgressHub.Instance.Report(StageConfig, (i + 1) / (float)warmups.Length);
            }
            LoadProgressHub.Instance.Complete(StageConfig);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Preloader] 配置缓存预热失败：{e.Message}");
            LoadProgressHub.Instance.Complete(StageConfig); // 失败也放行，不让进度条卡死
        }
    }
}
