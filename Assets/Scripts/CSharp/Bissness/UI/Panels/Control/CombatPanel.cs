using System.Collections.Generic;
using Cinemachine;
using Core.Gear;
using Framework.Gameplay.Battle;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 战斗 HUD 面板控制层（MVC 的 C）：
/// - 点数/波次走 EventCenter 事件驱动（CombatManager 装配后桥接广播），打开时先落当前值；
/// - 守点血量、部署槽位无逐帧事件，Update 轮询快照（值变更才重写文本）；
/// - 第四栏 NextStoreTimeText 语义 = 商店出现倒计时，商店未接入前不驱动（勿接 PointLedger 供给）；
/// - 部署槽位卡片拖放（屏幕侧）：拖动时卡片抬起/放回（CHContainer 本地 y ±50，切换播音效）、
///   指针射线落战场落点（deployGroundLayers 优先，未命中兜底 y=0 平面，幽灵始终跟手不拉回）喂幽灵预览、
///   松开走世界侧放置校验（CombatManager.EndDeployDrag）；
/// - 暂停按钮转发 CombatManager.TogglePause（③ 停推 + timeScale 冻结）。
/// 数据入口：CombatManager.Current（战斗场景唯一管理件），控制器未就绪时 HUD 保持空白等待。
/// </summary>
public class CombatPanel : PanelBase
{
    [Header("View 视图分区")]
    // 面板根视图（状态区四组 Icon+Text、八个部署槽位、暂停按钮）
    [SerializeField] private CombatPanelView view;

    [Header("UnitStatPanel 副面板")]
    [Tooltip("单位状态副面板（SetupUnitStatPanel 接线）：副面板关闭时由本面板 OnUpdate 调点击探针，" +
        "打开时探针让位给副面板自身 Update 接管点击")]
    [SerializeField] private UnitStatPanel unitStatPanel;

    [Header("Focus Camera（聚焦相机）")]
    [Tooltip("战场点击选中单位时把 Follow/LookAt 赋给单位视图的虚拟相机（CinemachineVirtualCamera，" +
        "SetupUnitStatPanel 接线）：虚拟相机数值不动，只赋这两个目标；清选中归 null 回默认视角")]
    [SerializeField] private Cinemachine.CinemachineVirtualCamera focusCamera;

    // 聚焦相机上次已赋目标视图（选中变更才重新赋值，避免每帧写 Cinemachine）
    private GameObject lastFocusTarget;

    [Header("Drag Ground（拖放落点）")]
    [Tooltip("放置落点检测层（拖动射线只与这些层求交取真实地面/高台顶面；空 = 退回 y=0 平面）")]
    [SerializeField] private LayerMask deployGroundLayers;

    // EventCenter 订阅句柄（Unregister 要求同一委托实例）
    private UnityAction<int> onPointsChanged;
    private UnityAction<int> onWaveChanged;
    private UnityAction<BattleOutcome> onOutcomeDecided;

    // 文本缓存：值未变不重写（避免 TMP 每帧重建）
    private string cachedPoints;
    private string cachedWaves;
    private string cachedHp;
    // 槽位渲染缓存（索引 = 槽位下标）：内容激活态与费用文本
    private readonly bool[] cachedSlotOccupied = new bool[8];
    private readonly string[] cachedSlotCost = new string[8];
    // 槽位头像去抖（槽位 → 已填充角色 id）
    private readonly string[] cachedSlotIcon = new string[8];

    // —— 编队卡拖放（屏幕侧）——
    // 卡片抬升量（CHContainer 本地 y，用户拍板 50）
    private const float CardLiftOffset = 50f;
    // 每槽卡片基线 y（CHContainer 本地坐标，拖动只在基线上加减抬升量）
    private readonly float[] slotBaseLocalY = new float[8];
    // 拖动状态：当前拖动槽位（-1 = 未拖动）与卡片是否处于抬起态
    private int draggingSlot = -1;
    private bool cardLifted;
    // 卡片音效源（面板自身 2D 源，全卡共用 view.cardToggleSfx 一份 clip）
    private AudioSource sfxSource;
    // 落点来源调试态：true=地面层射线命中；跌回 y=0 平面兜底的沿上告警一次（去重防逐帧刷屏）
    private bool lastGroundHitByRay = true;

    // 上一次见到的控制器（null → 就绪的沿上做一次全量种子刷新）
    private BattleController lastController;

    #region PanelBase 生命周期

    /// <summary>事件监听与按钮绑定：EventCenter 三事件订阅 + 暂停按钮。</summary>
    protected override void EventInit()
    {
        if (view == null)
        {
            view = GetComponent<CombatPanelView>() ?? GetComponentInChildren<CombatPanelView>(true);
        }
        if (view == null)
        {
            Debug.LogError("[CombatPanel] 缺 CombatPanelView，HUD 停摆。", this);
            return;
        }
        view.EnsureBindings();
        WireDeployCards();

        onPointsChanged = points => WritePoints(points);
        onWaveChanged = wave => WriteWaves(wave);
        onOutcomeDecided = outcome => Debug.Log($"[CombatPanel] 胜负判定：{outcome}");
        EventCenter.Instance.Register(EventNames.BattlePointChanged, onPointsChanged);
        EventCenter.Instance.Register(EventNames.BattleWaveChanged, onWaveChanged);
        EventCenter.Instance.Register(EventNames.BattleOutcomeDecided, onOutcomeDecided);

        if (view.pauseButton != null)
        {
            view.pauseButton.onClick.AddListener(OnPauseClicked);
        }
    }

    /// <summary>组件初始化：控制器已就绪则先落一次当前值（事件只报增量，打开晚于初值时兜底）。</summary>
    protected override void ComponentInit()
    {
        SeedIfControllerReady();
    }

    /// <summary>逐帧轮询：守点血量 / 补给倒计时 / 部署槽位（事件化挂账，值变更才重写）。</summary>
    protected override void OnUpdate()
    {
        CombatManager mgr = CombatManager.Current;
        BattleController controller = mgr != null ? mgr.Controller : null;

        SeedIfControllerReady();
        // 副面板点击探针（恒活宿主）：关闭时点单位 → 选中并打开；打开时探针自行让位（PollOpenClick 内部短路）
        if (unitStatPanel != null) unitStatPanel.PollOpenClick();
        UpdateFocusCamera();
        if (controller == null) return;

        WriteHp(mgr.GuardHp, mgr.GuardMaxHp);
        RefreshSlots(controller, mgr.DeployRoster);
    }

    /// <summary>
    /// 聚焦相机跟随选中单位：选中视图变化时把虚拟相机的 Follow/LookAt 赋给单位视图的
    /// Transform（虚拟相机其他数值不动），清选中（点空白）时归 null 回默认视角。
    /// 选中经 BattleSelection 统一读写，本面板 OnUpdate 轮询一处覆盖打开/关闭两条点击路径。
    /// </summary>
    private void UpdateFocusCamera()
    {
        if (focusCamera == null) return;
        BattleSelection selection = BattleSelection.Current;
        GameObject view = selection != null ? selection.SelectedView : null;
        if (view == lastFocusTarget) return;
        lastFocusTarget = view;
        Transform target = view != null ? view.transform : null;
        focusCamera.Follow = target;
        focusCamera.LookAt = target;
    }

    /// <summary>面板销毁时退订事件 + 取消进行中的拖放（委托句柄逐个注销）。</summary>
    protected override void OnDestroy()
    {
        if (draggingSlot != -1 && CombatManager.Current != null)
        {
            CombatManager.Current.CancelDeployDrag();
        }
        EventCenter.Instance.Unregister(EventNames.BattlePointChanged, onPointsChanged);
        EventCenter.Instance.Unregister(EventNames.BattleWaveChanged, onWaveChanged);
        EventCenter.Instance.Unregister(EventNames.BattleOutcomeDecided, onOutcomeDecided);
        base.OnDestroy();
    }

    #endregion

    #region 数据写入（值未变不重写）

    // 控制器首次就绪沿上：种子点数/波次当前值（事件与轮询之外的打开时兜底）。
    private void SeedIfControllerReady()
    {
        CombatManager mgr = CombatManager.Current;
        BattleController controller = mgr != null ? mgr.Controller : null;
        if (controller == null || controller == lastController) return;

        lastController = controller;
        WritePoints(controller.Ledger.Current);
        WriteWaves(controller.CurrentWaveNumber);
        cachedHp = null;
        for (int i = 0; i < cachedSlotOccupied.Length; i++)
        {
            cachedSlotOccupied[i] = false;
            cachedSlotCost[i] = null;
            cachedSlotIcon[i] = null;
        }
    }

    private void WritePoints(int points)
    {
        if (view == null || view.costPointText == null) return;
        string text = points.ToString();
        if (text == cachedPoints) return;
        cachedPoints = text;
        view.costPointText.text = text;
    }

    private void WriteWaves(int wave)
    {
        if (view == null || view.remainingWavesText == null) return;
        CombatManager mgr = CombatManager.Current;
        int total = mgr != null ? mgr.TotalWaves : 0;
        string text = total > 0 ? $"{wave} / {total}" : wave.ToString();
        if (text == cachedWaves) return;
        cachedWaves = text;
        view.remainingWavesText.text = text;
    }

    private void WriteHp(int hp, int maxHp)
    {
        if (view == null || view.remainingHpText == null) return;
        string text = $"{hp} / {maxHp}";
        if (text == cachedHp) return;
        cachedHp = text;
        view.remainingHpText.text = text;
    }

    // 部署槽位：名单序占用（i < 名单数 → 角色卡+费用），名单外空槽；已放置过的卡片回空槽显示。
    private void RefreshSlots(BattleController controller, IReadOnlyList<string> roster)
    {
        CombatManager mgr = CombatManager.Current;
        if (view == null || view.slotRoots == null) return;
        for (int i = 0; i < view.slotRoots.Length; i++)
        {
            if (view.slotRoots[i] == null) continue;

            bool occupied = roster != null && i < roster.Count
                && !(mgr != null && mgr.IsUnitDeployed(roster[i]));
            string cost = null;
            if (occupied && controller.TryGetDeployCost(roster[i], out int deployCost))
            {
                cost = deployCost.ToString();
            }

            if (occupied != cachedSlotOccupied[i] || cost != cachedSlotCost[i])
            {
                cachedSlotOccupied[i] = occupied;
                cachedSlotCost[i] = cost;
                if (view.slotContentNodes[i] != null) view.slotContentNodes[i].SetActive(occupied);
                if (view.slotEmptyNodes[i] != null) view.slotEmptyNodes[i].SetActive(!occupied);
                if (view.slotCostTexts[i] != null) view.slotCostTexts[i].text = cost;
            }

            // 头像：占用槽位异步填充（CHAvatar_Collection_<unitId>），未命中保留默认图
            if (occupied && view.slotIconImages[i] != null)
            {
                FillSlotIconAsync(i, roster[i]);
            }
        }
    }

    /// <summary>异步填充槽位头像；回包时槽位已换绑其他角色则丢弃结果。</summary>
    private async void FillSlotIconAsync(int slotIndex, string unitId)
    {
        if (cachedSlotIcon[slotIndex] == unitId) return;
        cachedSlotIcon[slotIndex] = unitId;

        Sprite avatar = await IconLibrary.GetCharacterAvatarAsync(
            IconLibrary.VariantCollection, unitId, ResourceScope);
        if (avatar == null || view == null || view.slotIconImages[slotIndex] == null) return;
        if (cachedSlotIcon[slotIndex] != unitId) return;
        view.slotIconImages[slotIndex].sprite = avatar;
    }

    #endregion

    #region 编队卡拖放（屏幕侧）

    /// <summary>
    /// 部署槽位拖拽接线：每槽运行时挂 DeployCardDragSource（行为接线不进预制体）并转发三事件；
    /// 顺带记录各槽卡片基线 y 与准备共用音效源。
    /// </summary>
    private void WireDeployCards()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        for (int i = 0; i < view.slotRoots.Length; i++)
        {
            GameObject slot = view.slotRoots[i];
            if (slot == null) continue;

            slotBaseLocalY[i] = view.slotContentNodes[i] != null
                ? view.slotContentNodes[i].transform.localPosition.y
                : 0f;

            DeployCardDragSource source = slot.GetComponent<DeployCardDragSource>();
            if (source == null) source = slot.AddComponent<DeployCardDragSource>();
            source.SlotIndex = i;
            source.DragStarted = OnCardDragStarted;
            source.DragMoved = OnCardDragMoved;
            source.DragEnded = OnCardDragEnded;
        }
    }

    // 开始拖动：仅未放置过的名单内角色可拖，点数不足不进入拖动（卡片费用即门槛）；
    // 拉起世界侧幽灵预览并抬起卡片
    private void OnCardDragStarted(int slotIndex)
    {
        if (draggingSlot != -1) return;
        string unitId = GetSlotUnitId(slotIndex);
        CombatManager mgr = CombatManager.Current;
        if (unitId == null || mgr == null || mgr.Controller == null) return;
        if (mgr.Controller.TryGetDeployCost(unitId, out int cost)
            && mgr.Controller.Ledger.Current < cost)
        {
            return;
        }

        draggingSlot = slotIndex;
        mgr.BeginDeployDrag(unitId);
        SetCardLifted(true);
    }

    // 拖动中：屏幕空间指针 y 与槽位 y 比较驱动卡片抬起/放回（切换播音效）；
    // 指针射线落战场地面（y=0 平面）的世界点同步给世界侧幽灵
    private void OnCardDragMoved(int slotIndex, Vector2 pointerScreen)
    {
        if (draggingSlot != slotIndex) return;
        bool wantLifted = IsPointerAboveSlot(slotIndex, pointerScreen);
        if (wantLifted != cardLifted) SetCardLifted(wantLifted);

        CombatManager mgr = CombatManager.Current;
        if (mgr != null && TryScreenToGround(pointerScreen, out Vector3 world))
        {
            mgr.UpdateDeployDrag(world);
        }
    }

    // 松开：抬起态 = 尝试放置（世界侧区域盒/障碍/过近全量校验 + SubmitDeploy）；
    // 放回态 = 取消。卡片复位（复位本身按切换口径播音效）。
    private void OnCardDragEnded(int slotIndex, Vector2 pointerScreen)
    {
        if (draggingSlot != slotIndex) return;
        CombatManager mgr = CombatManager.Current;
        if (mgr != null)
        {
            if (cardLifted && TryScreenToGround(pointerScreen, out Vector3 world))
            {
                mgr.EndDeployDrag(world);
            }
            else
            {
                mgr.CancelDeployDrag();
            }
        }
        // 先复位卡片（内部按 draggingSlot 找 CHContainer），再清拖动槽位
        SetCardLifted(false);
        draggingSlot = -1;
    }

    // 槽位当前可拖角色 id：名单内 && 未放置过；null = 不可拖（空槽/已放置/名单外）
    private string GetSlotUnitId(int slotIndex)
    {
        CombatManager mgr = CombatManager.Current;
        IReadOnlyList<string> roster = mgr != null ? mgr.DeployRoster : null;
        if (roster == null || slotIndex >= roster.Count) return null;
        string unitId = roster[slotIndex];
        return mgr.IsUnitDeployed(unitId) ? null : unitId;
    }

    // 屏幕空间指针是否高于槽位（高于 = 想放置，低于 = 想放回；用户拍板口径）。
    // Overlay 画布传 null 相机，Camera 模式带画布相机。
    private bool IsPointerAboveSlot(int slotIndex, Vector2 pointerScreen)
    {
        GameObject slot = view.slotRoots[slotIndex];
        if (slot == null) return false;
        Canvas canvas = view.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(
            canvasCamera, slot.transform.position);
        return pointerScreen.y >= slotScreen.y;
    }

    // 指针射线 → 落点：优先与拖放地面层求交（真实地形/高台顶即真实 y）；
    // 未命中兜底 y=0 平面——幽灵始终跟手（指向不可放置位置由红调表达，绝不拉回旧位，2026-10-19 用户拍板）
    private bool TryScreenToGround(Vector2 pointerScreen, out Vector3 world)
    {
        world = default;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(pointerScreen);
        if (deployGroundLayers.value != 0
            && Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, deployGroundLayers.value))
        {
            lastGroundHitByRay = true;
            world = hit.point;
            return true;
        }

        // 兜底沿上告警一次：射线没命中地面层（掩码漏层或指向地形外）——退 y=0 平面仅保证跟手
        if (lastGroundHitByRay)
        {
            lastGroundHitByRay = false;
            Debug.LogWarning("[CombatPanel] 拖动落点未命中 deployGroundLayers，退回 y=0 平面兜底" +
                "（高台不跟手=高台层没勾进掩码）", this);
        }
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (!ground.Raycast(ray, out float enter))
        {
            world = default;
            return false;
        }
        world = ray.GetPoint(enter);
        return true;
    }

    // 卡片升降：CHContainer 本地 y 在基线上加减抬升量；状态切换播音效（全卡共用一份 clip）
    private void SetCardLifted(bool lifted)
    {
        if (cardLifted == lifted) return;
        cardLifted = lifted;
        if (draggingSlot < 0 || draggingSlot >= view.slotContentNodes.Length) return;
        GameObject content = view.slotContentNodes[draggingSlot];
        if (content == null) return;

        Vector3 local = content.transform.localPosition;
        local.y = slotBaseLocalY[draggingSlot] + (lifted ? CardLiftOffset : 0f);
        content.transform.localPosition = local;

        if (view.cardToggleSfx != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(view.cardToggleSfx);
        }
    }

    #endregion

    /// <summary>暂停按钮：转发战斗管理器切换模拟暂停。</summary>
    private void OnPauseClicked()
    {
        if (CombatManager.Current == null) return;
        CombatManager.Current.TogglePause();
    }
}
