using System;
using System.Collections.Generic;
using Core.Gear;
using Framework.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出击/战斗选择面板控制层（MVC 的 C）：
/// - 统筹主页面（MainPage）与主关卡页面（MainStagePage）的分页展示与切换；
/// - 动态根据玩家主城大厅头像展示角色 3D 模型（自 Prefab-Unit-Character-CombateSelect 组加载，带 CombatSelect 标签）；
/// - 控制角色自律状态机（Idle 常态待机、随机定时 Walk 漫步、被点击播放 Reaction 反馈）；
/// - 动态刷新主关卡列表（经 ObjectsPool 池化复用 LevelItem 关卡条目）；
/// - 动态维护 GearBar 返回回调：主关卡页返回到主页面，主页面返回则关闭面板。
/// </summary>
public class CombatSelectPanel : PanelBase
{
    [Header("View 视图分区")]
    // 面板根视图（持有 GearBar、MainPage、MainStagePage）
    [SerializeField] private CombatSelectPanelView view;
    // 顶部状态栏分区（返回、货币展示、加号、设置等）
    [SerializeField] private GearBar gearBar;
    // 关卡条目预制体（LevelItem）：经 ObjectsPool 池化出池入池
    [SerializeField] private GameObject levelItemPrefab;

    // 模型加载标签约束：必须同时携带 CombatSelect 标签，防止与 Cultivate 面板同单位 ID 的展示模型交集串载
    private static readonly string[] ModelLabels = { "CombatSelect" };

    // 当前加载的角色模型资源租约（随面板关闭或切角级联释放）
    private ResourceLease<GameObject> characterModelLease;
    // 当前实例化的角色模型 GameObject
    private GameObject characterModelInstance;
    // 角色模型上的动画与行为控制代理组件
    private CombatSelectCharacterAgent characterAgent;
    // 当前已加载模型的角色 ID（防重复加载）
    private string loadedModelUnitId;

    // 当前页面下标：0 = MainPage（出击主页），1 = MainStagePage（主关卡页）
    private int currentPageIndex;

    /// <summary>关卡简单展示数据结构。</summary>
    private struct StageDisplayInfo
    {
        public string StageId;
        public string StageName;
        public string ScoreRank;
    }

    // 默认主线第一章关卡数据样例
    private static readonly StageDisplayInfo[] DefaultStages = new[]
    {
        new StageDisplayInfo { StageId = "101", StageName = "1-1 Encounter", ScoreRank = "S" },
        new StageDisplayInfo { StageId = "102", StageName = "1-2 Advance", ScoreRank = "S" },
        new StageDisplayInfo { StageId = "103", StageName = "1-3 Vanguard", ScoreRank = "S" },
        new StageDisplayInfo { StageId = "104", StageName = "1-4 Defense Line", ScoreRank = "S" },
        new StageDisplayInfo { StageId = "105", StageName = "1-5 Ambush", ScoreRank = "S" },
        new StageDisplayInfo { StageId = "106", StageName = "1-6 Breakthrough", ScoreRank = "S" },
    };

    #region PanelBase 生命周期

    /// <summary>
    /// 组件与引用初始化：缓存 View 与 GearBar，设置初始展示主页面，异步拉取大厅角色并装载关卡列表。
    /// </summary>
    protected override void ComponentInit()
    {
        // 查找并缓存 View 引用
        if (view == null)
        {
            view = GetComponent<CombatSelectPanelView>() ?? GetComponentInChildren<CombatSelectPanelView>(true);
        }
        if (view != null) view.EnsureBindings();

        // 查找并缓存 GearBar 引用
        if (gearBar == null)
        {
            gearBar = view != null && view.gearBar != null ? view.gearBar : GetComponentInChildren<GearBar>(true);
        }

        // 若预制体未直接关联 levelItemPrefab，尝试从 view 中获取
        if (levelItemPrefab == null && view != null && view.mainStagePage != null)
        {
            levelItemPrefab = view.mainStagePage.levelItemPrefab;
        }

        // 默认显示主页
        ShowPage(0);

        // 异步加载并展示玩家大厅选择的角色 3D 模型
        _ = LoadMainPageCharacterAsync();

        // 初始化主关卡页面数据与列表
        InitMainStagePage();
    }

    /// <summary>
    /// 事件监听与按钮绑定：绑定主关卡进入、副本未开放提示、角色点击反馈及 GearBar 返回。
    /// </summary>
    protected override void EventInit()
    {
        // 初始化 GearBar 返回按钮：主页状态下点击返回则关闭当前面板
        UpdateGearBarGoBack();

        if (view == null || view.mainPage == null) return;
        CombatSelectMainPageView main = view.mainPage;

        // 主关卡入口：点击切换至 MainStagePage
        if (main.mainLevelsButton != null)
        {
            main.mainLevelsButton.onClick.AddListener(EnterMainStagePage);
        }

        // 角色点击交互：转发至自律代理播放 Reaction 反应动作
        if (main.characterClickButton != null)
        {
            main.characterClickButton.onClick.AddListener(OnCharacterClicked);
        }

        // 副本入口（目前未开放，点击弹出 Notice 提示）
        BindComingSoonButton(main.challengeButton, "Challenge Dungeon");
        BindComingSoonButton(main.tideButton, "Tide Dungeon");
        BindComingSoonButton(main.moreLeftButton, "Special Dungeon");
        BindComingSoonButton(main.expDungeonButton, "EXP Material Dungeon");
        BindComingSoonButton(main.normalDungeonButton, "Normal Material Dungeon");
        BindComingSoonButton(main.bossDungeonButton, "Boss Material Dungeon");
        BindComingSoonButton(main.weeklyDungeonButton, "Weekly Material Dungeon");
        BindComingSoonButton(main.morwRightButton, "Material Dungeon");
    }

    /// <summary>
    /// 面板销毁时清理模型实例与释放资源租约。
    /// </summary>
    protected override void OnDestroy()
    {
        ReleaseCharacterModel();
        base.OnDestroy();
    }

    #endregion

    #region 页面流转与 GearBar 控制

    /// <summary>
    /// 切换当前展示页面。
    /// </summary>
    /// <param name="pageIndex">页面下标：0 = MainPage，1 = MainStagePage。</param>
    private void ShowPage(int pageIndex)
    {
        currentPageIndex = pageIndex;

        if (view != null)
        {
            if (view.mainPage != null)
            {
                view.mainPage.gameObject.SetActive(pageIndex == 0);
            }
            if (view.mainStagePage != null)
            {
                view.mainStagePage.gameObject.SetActive(pageIndex == 1);
            }
        }

        // 页面切换时同步切换 GearBar 的返回按钮语义
        UpdateGearBarGoBack();
    }

    /// <summary>
    /// 进入主关卡页面。
    /// </summary>
    private void EnterMainStagePage()
    {
        ShowPage(1);
    }

    /// <summary>
    /// 从主关卡页面返回到出击主页面。
    /// </summary>
    private void ReturnToMainPage()
    {
        ShowPage(0);
    }

    /// <summary>
    /// 刷新 GearBar 返回按钮事件：
    /// - 若当前处于主关卡页（MainStagePage），返回事件为切回出击主页（MainPage）；
    /// - 若当前处于出击主页（MainPage），返回事件为关闭本面板。
    /// </summary>
    private void UpdateGearBarGoBack()
    {
        if (gearBar == null) return;

        if (currentPageIndex == 1)
        {
            // 在主关卡页：返回主页
            gearBar.RegisterGoBack(ReturnToMainPage);
        }
        else
        {
            // 在主页：关闭面板
            gearBar.RegisterGoBack(CloseSelf);
        }
    }

    /// <summary>关闭当前面板。</summary>
    private void CloseSelf()
    {
        PanelManager.Instance.ClosePanel(this);
    }

    /// <summary>绑定未开放功能的提示按钮。</summary>
    private void BindComingSoonButton(Button btn, string functionName)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() =>
        {
            EventCenter.Instance.SetEventTrigger(EventNames.NoticeRequested, "Coming soon");
            Debug.Log($"[CombatSelectPanel] 功能暂未开放: {functionName}");
        });
    }

    #endregion

    #region 角色模型加载与交互

    /// <summary>
    /// 加载并展示主城大厅当前使用的角色 3D 模型。
    /// </summary>
    private async System.Threading.Tasks.Task LoadMainPageCharacterAsync()
    {
        // 获取玩家大厅当前配置的头像名称，并解析对应角色 ID（如 "CH0221"）
        string avatarName = PlayerInfoManager.Instance.Data != null ? PlayerInfoManager.Instance.Data.avatarName : null;
        string unitId = GetUnitIdFromAvatar(avatarName);
        if (string.IsNullOrEmpty(unitId)) unitId = "CH0221";

        // 已加载同角色则无需重复加载
        if (loadedModelUnitId == unitId && characterModelInstance != null) return;

        // 卸载并释放先前的模型
        ReleaseCharacterModel();
        loadedModelUnitId = unitId;

        ResourceLease<GameObject> lease = null;
        try
        {
            // 通过 ResourceScope 申请资源租约（要求携带 CombatSelect 标签以区分培养面板模型）
            lease = await AddressableManager.Instance.AcquireAssetWithLabelsAsync<GameObject>(unitId, ModelLabels, ResourceScope);
        }
        catch (Exception ex)
        {
            loadedModelUnitId = null;
            Debug.LogWarning($"[CombatSelectPanel] 角色展示模型加载失败: {unitId} (标签: CombatSelect), 异常: {ex.Message}");
            return;
        }

        // 异步等待期间如果面板已销毁或 Scope 已释放，则立即退租
        if (this == null || ResourceScope == null || ResourceScope.IsDisposed)
        {
            lease?.Dispose();
            return;
        }

        characterModelLease = lease;

        // 挂载至 MainPage 锚点
        Transform anchor = view != null && view.mainPage != null ? view.mainPage.characterPos : null;
        if (anchor == null || lease.Asset == null)
        {
            Debug.LogWarning("[CombatSelectPanel] 角色挂载锚点缺失或模型资产为空");
            return;
        }

        characterModelInstance = Instantiate(lease.Asset, anchor);
        characterModelInstance.transform.localPosition = Vector3.zero;
        characterModelInstance.transform.localRotation = Quaternion.identity;
        characterModelInstance.transform.localScale = Vector3.one;

        // 获取或装配角色自律动画控制代理
        characterAgent = characterModelInstance.GetComponent<CombatSelectCharacterAgent>();
        if (characterAgent == null)
        {
            characterAgent = characterModelInstance.AddComponent<CombatSelectCharacterAgent>();
        }
    }

    /// <summary>
    /// 释放当前持有的角色模型实例与资源租约。
    /// </summary>
    private void ReleaseCharacterModel()
    {
        if (characterModelInstance != null)
        {
            Destroy(characterModelInstance);
            characterModelInstance = null;
        }

        characterAgent = null;

        if (characterModelLease != null)
        {
            characterModelLease.Dispose();
            characterModelLease = null;
        }

        loadedModelUnitId = null;
    }

    /// <summary>
    /// 响应角色展示区点击：触发角色的 Reaction 反馈动作。
    /// </summary>
    private void OnCharacterClicked()
    {
        if (characterAgent != null)
        {
            characterAgent.TriggerReaction();
        }
    }

    /// <summary>
    /// 从头像名称解析角色 ID（如 "CHAvatar_PlayerAvatar_CH0221" → "CH0221"）。
    /// </summary>
    private static string GetUnitIdFromAvatar(string avatarName)
    {
        if (string.IsNullOrEmpty(avatarName)) return "CH0221";

        const string prefix = "CHAvatar_PlayerAvatar_";
        if (avatarName.StartsWith(prefix))
        {
            return avatarName.Substring(prefix.Length);
        }

        return avatarName;
    }

    #endregion

    #region 主关卡页面数据与列表刷新

    /// <summary>
    /// 初始化主关卡页面的章节信息及关卡滚动列表。
    /// </summary>
    private void InitMainStagePage()
    {
        if (view == null || view.mainStagePage == null) return;
        CombatSelectMainStagePageView stagePage = view.mainStagePage;

        // 章节标题与通关进度
        if (stagePage.titleText != null)
        {
            stagePage.titleText.text = "Chapter 01 - Operation Vanguard";
        }
        if (stagePage.processText != null)
        {
            stagePage.processText.text = "6 / 6";
        }
        if (stagePage.completeMark != null)
        {
            stagePage.completeMark.gameObject.SetActive(true);
        }

        // 刷新关卡条目列表
        RefreshStageList(stagePage);
    }

    /// <summary>
    /// 经由 ObjectsPool 池化复用刷新关卡条目列表。
    /// </summary>
    private void RefreshStageList(CombatSelectMainStagePageView stagePage)
    {
        if (stagePage.stageContent == null || levelItemPrefab == null) return;
        RectTransform content = stagePage.stageContent;

        // 回收旧条目至对象池
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            if (child.GetComponent<CombatSelectLevelItemView>() != null)
            {
                ObjectsPool.Instance.Put(child);
            }
        }

        // 生成关卡列表项
        for (int i = 0; i < DefaultStages.Length; i++)
        {
            StageDisplayInfo stage = DefaultStages[i];
            GameObject itemGo = ObjectsPool.Instance.Get(levelItemPrefab, content);
            if (itemGo == null) continue;

            itemGo.transform.localScale = Vector3.one;
            itemGo.transform.localPosition = Vector3.zero;

            CombatSelectLevelItemView itemView = itemGo.GetComponent<CombatSelectLevelItemView>();
            if (itemView == null)
            {
                itemView = itemGo.AddComponent<CombatSelectLevelItemView>();
            }
            itemView.EnsureBindings();

            // 填充关卡信息
            if (itemView.levelNameText != null)
            {
                itemView.levelNameText.text = stage.StageName;
            }
            if (itemView.enterButtonText != null)
            {
                itemView.enterButtonText.text = "Battle";
            }
            if (itemView.scoreIcon != null)
            {
                itemView.scoreIcon.gameObject.SetActive(!string.IsNullOrEmpty(stage.ScoreRank));
            }

            // 绑定进入关卡点击事件
            if (itemView.enterButton != null)
            {
                itemView.enterButton.onClick.RemoveAllListeners();
                itemView.enterButton.onClick.AddListener(() => OnStageEnterClicked(stage));
            }
        }
    }

    /// <summary>
    /// 关卡进入按钮点击处理。
    /// </summary>
    private void OnStageEnterClicked(StageDisplayInfo stage)
    {
        Debug.Log($"[CombatSelectPanel] 进入关卡: {stage.StageName} (ID={stage.StageId})");
        EventCenter.Instance.SetEventTrigger(EventNames.NoticeRequested, $"Selected: {stage.StageName}");
    }

    #endregion
}
