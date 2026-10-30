using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Player;
using Framework.Gameplay.Unit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 战备面板控制层（MVC 的 C）：六套编队页签切换、8 槽位信息与模型展示、角色拖拽换位、
/// 编辑队伍面板（仓库名单多选编入、选中排序、Mark/Num 角标）、战斗按钮校验进战斗。
/// 状态栏（GearBar）为自管理复用组件，货币刷新与默认按钮零接线，面板仅注册返回按钮语义。
/// </summary>
public class FormationPanel : PanelBase, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //模型加载标签：与培养面板同源（Cultivate 标签展示模型，实例化后不播 Timeline 即静立 Stat 姿态）
    private static readonly string[] ModelLabels = { "Cultivate" };

    [Header("View 分区")]
    //顶部状态栏分区：返回/货币/加号/设置/回主页
    [SerializeField] private GearBar gearBar;
    //左右页切换分区：左右翻页按钮/编队索引页签/编队/战斗按钮
    [SerializeField] private CombatReadinessOtherView otherView;
    //左页角色列表分区：4 个角色信息栏，挂在 CHList1 页根节点上
    [SerializeField] private CHInfoList leftInfoList;
    //右页角色列表分区：4 个角色信息栏，挂在 CHList2 页根节点上
    [SerializeField] private CHInfoList rightInfoList;

    [Header("编辑队伍分区")]
    //编辑队伍面板根节点：编队按钮激活，完成按钮收起
    [SerializeField] private GameObject teamEditPanelRoot;
    //底部操作分区根（Bot：TeamEdit/Finish 按钮）：编辑面板打开时隐藏，关闭时恢复
    [SerializeField] private GameObject botRoot;
    //编辑名单滚动内容区：列表项容器（多选组运行时补建到此节点）
    [SerializeField] private RectTransform teamEditContent;
    //完成编辑按钮
    [SerializeField] private Button teamEditCompleteButton;
    //编辑名单项预制体（面板内预置的 Item 实例，ObjectsPool 以名字池化复用）
    [SerializeField] private GameObject editItemTemplate;

    //当前是否显示左页（true=左页 false=右页）
    private bool isLeftPage = true;
    //拖拽起手命中的角色信息栏（槽位换位判定用）
    private CharacterInformation dragSourceInfo;
    //射线结果缓存列表，避免拖拽事件中反复分配
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    //当前选中的编队索引（0 = 第 1 套配队）
    private int currentTeamIndex;

    //角色表缓存：槽位信息与编辑名单填充用（主键 unitId，如 CH0221）
    private DataTable<CharacterRow, string> characterTable;

    //8 槽信息栏缓存：左页 0~3、右页 4~7，EventInit 建立一次
    private CharacterInformation[] slotInfos;

    //槽位模型租约/实例/加载令牌（下标 = 槽位 0~7；切槽/切队/关板按令牌判失效防竞态）
    private readonly ResourceLease<GameObject>[] slotModelLeases =
        new ResourceLease<GameObject>[FormationTeamData.TeamSlotCount];
    private readonly GameObject[] slotModelInstances = new GameObject[FormationTeamData.TeamSlotCount];
    private readonly int[] slotModelTokens = new int[FormationTeamData.TeamSlotCount];
    //各槽在途加载的角色 id（null=无在途）：同槽同角重复请求直接短路，防并发取址互杀共享条目
    private readonly string[] slotLoadingIds = new string[FormationTeamData.TeamSlotCount];

    //编辑名单展示数据：仓库条目与角色表行配对（编队中角色排前）
    private readonly List<(CharacterEntry entry, CharacterRow row)> editDisplayList =
        new List<(CharacterEntry, CharacterRow)>();

    //编辑名单多选组（运行时补建到 teamEditContent，上限 = 槽位数）
    private MultiToggleGroup editToggleGroup;

    protected override void EventInit()
    {
        //返回按钮为本面板语义，手动注册返回回调（货币刷新/默认按钮由 GearBar 自管理，面板零接线）
        gearBar.RegisterGoBack(CloseSelf);

        //左右翻页：循环切换（最左再往左绕到最右，最右再往右绕回最左）
        otherView.leftButton.onClick.AddListener(SwitchCharacterPage);
        otherView.RightButton.onClick.AddListener(SwitchCharacterPage);

        //编队/战斗按钮（Bot 分区，引用由 Inspector 拖好）
        if (otherView.teamEditButton != null) otherView.teamEditButton.onClick.AddListener(OpenTeamEdit);
        if (otherView.combatButton != null) otherView.combatButton.onClick.AddListener(StartBattle);
        if (teamEditCompleteButton != null) teamEditCompleteButton.onClick.AddListener(CloseTeamEdit);

        //8 槽信息栏缓存建立后初始化编队页签（建组+绑切队回调；定选在 ComponentInit 做）
        BuildSlotInfos();
        InitTeamToggles();
    }

    protected override void ComponentInit()
    {
        //编辑面板默认收起（预制体默认激活，进面板先隐藏）
        if (teamEditPanelRoot != null) teamEditPanelRoot.SetActive(false);

        //底部操作分区与翻页分区默认激活（预制体默认失活，操作按钮都藏在这两处）
        if (botRoot != null) botRoot.SetActive(true);
        if (otherView != null) otherView.gameObject.SetActive(true);

        //默认显示左页，右页整页隐藏
        ShowLeftPage();

        //定选第 1 套配队（Start 晚于全部 Toggle.OnEnable，页签此时才真正注册进 ToggleGroup；
        //Awake 阶段置 isOn 会被序列化 isOn=1 的页签在 OnEnable 重放时反杀，故定选放这里）
        if (otherView.toggles != null && otherView.toggles.Count > 0
            && otherView.toggles[0] != null && !otherView.toggles[0].isOn)
        {
            otherView.toggles[0].isOn = true; //触发 SwitchTeam(0) 首刷
        }

        //角色表异步就绪后首刷；事件先于表就绪时刷新以空表安全跳过
        InitTableAsync();
    }

    /// <summary>
    /// 异步等角色表就绪后首刷 8 槽；编辑面板已开时同步刷名单（表未就绪期间打开编辑面板以空表安全跳过）。
    /// </summary>
    private async void InitTableAsync()
    {
        try
        {
            characterTable = await DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable");
            if (characterTable == null)
            {
                Debug.LogError("[FormationPanel] 角色表加载失败，槽位信息为空");
                return;
            }
            RefreshTeamSlots();
            if (teamEditPanelRoot != null && teamEditPanelRoot.activeSelf) RefreshTeamEditList();
        }
        catch (Exception e)
        {
            Debug.LogError($"[FormationPanel] 角色表加载异常：{e.Message}");
        }
    }

    /// <summary>
    /// 面板销毁时释放全部槽位模型租约并注销编辑多选回调，防止向已销毁组件写入。
    /// </summary>
    protected override void OnDestroy()
    {
        ReleaseAllSlotModels();
        if (editToggleGroup != null) editToggleGroup.SelectionChanged -= OnEditSelectionChanged;
        base.OnDestroy();
    }

    #region 编队索引页签

    /// <summary>
    /// 初始化编队索引页签：保证单选（场景未配 ToggleGroup 时运行时补建），注册选中回调并默认选中第 1 套。
    /// </summary>
    private void InitTeamToggles()
    {
        if (otherView.toggles == null || otherView.toggles.Count == 0) return;

        //索引栏要求始终有且只有一个选中；场景未配置 ToggleGroup 时补建到页签容器节点上
        ToggleGroup group = otherView.toggles[0].GetComponentInParent<ToggleGroup>();
        if (group == null) group = otherView.toggles[0].transform.parent.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = false;

        for (int i = 0; i < otherView.toggles.Count; i++)
        {
            Toggle toggle = otherView.toggles[i];
            if (toggle == null) continue;
            toggle.group = group;

            //闭包捕获局部索引，避免循环变量陷阱
            int teamIndex = i;
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (!isOn) return;
                SwitchTeam(teamIndex);
            });
        }

        //默认选中不放这里：Awake 早于各 Toggle 的 OnEnable（注册进组发生在 OnEnable），
        //此处置 isOn 组内无成员可踢，且会被其他页签序列化的 isOn=1 在 OnEnable 反杀。
        //定选统一在 ComponentInit（Start）做。
    }

    /// <summary>
    /// 切换编队：记录索引并写入编队管理器（当前选中即存档），刷新 8 个角色槽位。
    /// </summary>
    /// <param name="teamIndex">编队索引（从 0 起）。</param>
    private void SwitchTeam(int teamIndex)
    {
        currentTeamIndex = teamIndex;
        FormationTeamManager.Instance.SetCurrentTeamIndex(teamIndex);
        RefreshTeamSlots();
    }

    #endregion

    #region 编队槽位（信息栏 + 模型）

    /// <summary>建立 8 槽信息栏缓存：左页 0~3、右页 4~7；缺失项报错定位（Inspector 未拖引用）。</summary>
    private void BuildSlotInfos()
    {
        slotInfos = new CharacterInformation[FormationTeamData.TeamSlotCount];
        FillSlotInfosFromList(leftInfoList, 0);
        FillSlotInfosFromList(rightInfoList, FormationTeamData.TeamSlotCount / 2);

        for (int i = 0; i < slotInfos.Length; i++)
        {
            if (slotInfos[i] == null) Debug.LogError($"[FormationPanel] 第 {i + 1} 槽信息栏引用缺失（CHInfoList 未拖满）");
        }
    }

    //把一个 CHInfoList 的 4 个信息栏填入槽位缓存（baseIndex 起）。
    private void FillSlotInfosFromList(CHInfoList list, int baseIndex)
    {
        if (list == null) return;
        CharacterInformation[] infos =
        {
            list.characterInformation1, list.characterInformation2,
            list.characterInformation3, list.characterInformation4,
        };
        for (int i = 0; i < infos.Length && baseIndex + i < slotInfos.Length; i++)
        {
            slotInfos[baseIndex + i] = infos[i];
        }
    }

    /// <summary>
    /// 刷新 8 个编队槽位：按当前编队槽位数据填充信息栏，并按槽位加载/清空角色模型。
    /// </summary>
    private void RefreshTeamSlots()
    {
        if (slotInfos == null) return;

        string[] slots = FormationTeamManager.Instance.GetTeamSlots(currentTeamIndex);
        for (int i = 0; i < slotInfos.Length; i++)
        {
            string unitId = i < slots.Length ? slots[i] : string.Empty;
            RefreshSlotInfo(slotInfos[i], unitId);
            _ = ShowSlotModelAsync(i, unitId);
        }
    }

    //填充单个槽位信息栏（名字/等级/星级，取角色表行与仓库养成条目）；空槽清空展示。
    private void RefreshSlotInfo(CharacterInformation info, string unitId)
    {
        if (info == null) return;

        if (string.IsNullOrEmpty(unitId) || characterTable == null
            || !characterTable.TryGet(unitId, out CharacterRow row))
        {
            //空槽：格子与信息面板整组隐藏（角色未编队时该槽位不做任何展示）
            info.gameObject.SetActive(false);
            return;
        }

        info.gameObject.SetActive(true);
        //名字随表走；等级/星级取仓库养成条目（表行 Rarity 即星数，与 CharacterListPanel 同口径）
        if (info.chNameText != null) info.chNameText.text = row.Name;
        if (info.chLevelText != null)
        {
            int level = CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry)
                ? entry.level
                : 1;
            info.chLevelText.text = $"Lv.{level}";
        }
        if (info.chStarNumberText != null) info.chStarNumberText.text = $"{row.Rarity}";
    }

    /// <summary>
    /// 加载槽位角色模型：培养面板同源（unitId + Cultivate 标签），实例挂槽位 LayerInBGUI 渲染层下并归一变换，
    /// 挂载后播 Stat 页 Timeline（pages[0]）。切换槽位/编队先卸旧再加载，等待期间连切/关板由令牌判失效、
    /// 结果直接归还；空槽仅卸载。
    /// </summary>
    /// <param name="slotIndex">槽位下标（0~7）。</param>
    /// <param name="unitId">角色 id（模型 Addressable 地址）；空串清空槽位模型。</param>
    private async Task ShowSlotModelAsync(int slotIndex, string unitId)
    {
        //同槽同角在途：直接让在途链落模型（本次请求不再起链，避免令牌互杀共享加载条目——
        //编队面板一轮点击/表就绪都会重跑 RefreshTeamSlots，同角两条链并发时后者必死，10-10 培养面板同型竞态）
        if (!string.IsNullOrEmpty(unitId) && slotLoadingIds[slotIndex] == unitId) return;

        int token = ++slotModelTokens[slotIndex];
        ReleaseSlotModel(slotIndex);
        slotLoadingIds[slotIndex] = string.IsNullOrEmpty(unitId) ? null : unitId;
        if (string.IsNullOrEmpty(unitId)) return;

        ResourceLease<GameObject> lease;
        try
        {
            lease = await AddressableManager.Instance.AcquireAssetWithLabelsAsync<GameObject>(unitId, ModelLabels);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FormationPanel] 编队模型加载失败：{unitId}（未登记 Addressable 或缺 Cultivate 标签）：{e.Message}");
            if (token == slotModelTokens[slotIndex]) slotLoadingIds[slotIndex] = null;
            return;
        }

        //令牌失效：等待期间已切槽/切队/关板，新租约立即归还避免悬挂引用
        if (token != slotModelTokens[slotIndex])
        {
            lease.Dispose();
            return;
        }

        //锚点：槽位专用渲染层 LayerInBGUI（未接时兜底槽位节点本身）；信息栏未接或资产为空时不挂模型
        Transform anchor = null;
        if (slotInfos != null && slotInfos[slotIndex] != null)
        {
            anchor = slotInfos[slotIndex].chModelAnchor != null
                ? slotInfos[slotIndex].chModelAnchor
                : slotInfos[slotIndex].transform;
        }
        if (anchor == null || lease.Asset == null)
        {
            Debug.LogError($"[FormationPanel] 槽位锚点缺失，模型未挂载：slot={slotIndex}");
            lease.Dispose();
            slotLoadingIds[slotIndex] = null;
            return;
        }

        slotModelLeases[slotIndex] = lease;
        GameObject instance = Instantiate(lease.Asset, anchor);
        instance.name = unitId;
        //位置/缩放归一，旋转保留源模型自带摆位（培养面板 ShowModelAsync 同口径）
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one;
        slotModelInstances[slotIndex] = instance;

        //播 Stat 页展示（pages[0]，培养面板同源；playOnAwake 已在预制体压关，须显式播）。
        //展示区视图传 null = 不转向，保持 LayerInBGUI 继承朝向；实例未挂组件或该页未配 Timeline 时静立。
        CultivateModelDisplay display = instance.GetComponent<CultivateModelDisplay>();
        if (display != null) display.Play(0, null);
        slotLoadingIds[slotIndex] = null;
    }

    /// <summary>卸载单槽模型：销毁实例 + 归还租约（引用计数归零时 AddressableManager 释放底层资源）。</summary>
    private void ReleaseSlotModel(int slotIndex)
    {
        if (slotModelInstances[slotIndex] != null) Destroy(slotModelInstances[slotIndex]);
        slotModelInstances[slotIndex] = null;
        slotModelLeases[slotIndex]?.Dispose();
        slotModelLeases[slotIndex] = null;
    }

    /// <summary>卸载全部槽位模型：先令在途加载令牌全部失效再逐槽释放（关板防竞态）。</summary>
    private void ReleaseAllSlotModels()
    {
        for (int i = 0; i < slotModelTokens.Length; i++)
        {
            slotModelTokens[i]++;
            ReleaseSlotModel(i);
            //关板即全链作废：在途标记一并清掉，重开面板的同角请求按新链加载
            slotLoadingIds[i] = null;
        }
    }

    #endregion

    #region 编辑队伍

    /// <summary>打开编辑队伍面板：激活并按当前编队刷新候选名单（多选组首次补建）。</summary>
    private void OpenTeamEdit()
    {
        if (teamEditPanelRoot == null)
        {
            Debug.LogError("[FormationPanel] 编辑队伍面板根引用缺失（Inspector 未拖）");
            return;
        }

        teamEditPanelRoot.SetActive(true);
        //编辑期间隐藏底部操作按钮（TeamEdit/Finish 与编辑面板互斥）
        if (botRoot != null) botRoot.SetActive(false);
        //左右翻页按钮与编辑面板互斥（CHListSwitch 整节点，编辑时操作对象已被收起）
        if (otherView != null) otherView.gameObject.SetActive(false);
        //编辑期间两页槽位列表整根收起（与翻页同一显隐口径，槽位模型随子树一并隐藏）
        if (leftInfoList != null) leftInfoList.gameObject.SetActive(false);
        if (rightInfoList != null) rightInfoList.gameObject.SetActive(false);
        EnsureEditToggleGroup();
        RefreshTeamEditList();
    }

    /// <summary>完成编辑：收起面板（编队变更已实时写编队管理器）。</summary>
    private void CloseTeamEdit()
    {
        if (teamEditPanelRoot != null) teamEditPanelRoot.SetActive(false);
        if (botRoot != null) botRoot.SetActive(true);
        if (otherView != null) otherView.gameObject.SetActive(true);
        //恢复编辑前的角色页（isLeftPage 仍记录开编辑时的页，翻页函数内部幂等回写）
        if (isLeftPage) ShowLeftPage();
        else ShowRightPage();
    }

    /// <summary>补建编辑名单多选组到 Content 节点（已建则跳过），上限 = 编队槽位数。</summary>
    private void EnsureEditToggleGroup()
    {
        if (editToggleGroup != null || teamEditContent == null) return;

        editToggleGroup = teamEditContent.GetComponent<MultiToggleGroup>();
        if (editToggleGroup == null) editToggleGroup = teamEditContent.gameObject.AddComponent<MultiToggleGroup>();
        editToggleGroup.MaxOnCount = FormationTeamData.TeamSlotCount;
        editToggleGroup.SelectionChanged += OnEditSelectionChanged;
    }

    /// <summary>
    /// 重建编辑名单：仓库快照合角色表行 → 编队中角色按槽位序排前八、其余保持仓库获得顺序 →
    /// 池化刷新列表项（入池先从多选组注销，出池静默置选中态再注册）。
    /// </summary>
    private void RefreshTeamEditList()
    {
        if (teamEditContent == null || editItemTemplate == null || characterTable == null) return;

        //取数：仓库快照合表行；表缺失行报错跳过，不阻断整个名单
        editDisplayList.Clear();
        foreach (CharacterEntry entry in CharacterWarehouseManager.Instance.Snapshot())
        {
            if (characterTable.TryGet(entry.unitId, out CharacterRow row)) editDisplayList.Add((entry, row));
            else Debug.LogError($"[FormationPanel] CharacterTable 无角色 {entry.unitId} 的行，已跳过");
        }

        //槽位映射：unitId → 编队槽位（编队中才进映射）
        string[] slots = FormationTeamManager.Instance.GetTeamSlots(currentTeamIndex);
        Dictionary<string, int> slotOfUnit = new Dictionary<string, int>(slots.Length);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!string.IsNullOrEmpty(slots[i])) slotOfUnit[slots[i]] = i;
        }

        //排序：编队中按槽位序排前，其余保持仓库获得顺序（两轮插入天然稳定）
        Dictionary<string, (CharacterEntry entry, CharacterRow row)> byId =
            new Dictionary<string, (CharacterEntry, CharacterRow)>(editDisplayList.Count);
        foreach ((CharacterEntry entry, CharacterRow row) in editDisplayList) byId[entry.unitId] = (entry, row);

        List<(CharacterEntry entry, CharacterRow row)> ordered =
            new List<(CharacterEntry, CharacterRow)>(editDisplayList.Count);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!string.IsNullOrEmpty(slots[i]) && byId.TryGetValue(slots[i], out var pair)) ordered.Add(pair);
        }
        foreach ((CharacterEntry entry, CharacterRow row) in editDisplayList)
        {
            if (!slotOfUnit.ContainsKey(entry.unitId)) ordered.Add((entry, row));
        }
        editDisplayList.Clear();
        editDisplayList.AddRange(ordered);

        //池化刷新：旧列表项入池（先从多选组注销），再按新顺序取出重绑——增删选中只重绑不销毁重建
        for (int i = teamEditContent.childCount - 1; i >= 0; i--)
        {
            TeamEditItemView cell = teamEditContent.GetChild(i).GetComponent<TeamEditItemView>();
            if (cell == null) continue;
            if (editToggleGroup != null && cell.toggle != null) editToggleGroup.Unregister(cell.toggle);
            ObjectsPool.Instance.Put(cell.gameObject);
        }
        foreach ((CharacterEntry entry, CharacterRow row) in editDisplayList)
        {
            GameObject go = ObjectsPool.Instance.Get(editItemTemplate, teamEditContent);
            if (go == null) continue;

            //挂载后归一变换：池的 Instantiate 在无父状态克隆，挂回画布链时尺度错位（CharacterListPanel 同口径）
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;

            FillEditItem(go.GetComponent<TeamEditItemView>(), entry, slotOfUnit);
        }
    }

    //填充单个编辑名单项：绑定数据、静默置选中态后注册进多选组、刷新角标与数字、异步头像。
    private void FillEditItem(TeamEditItemView cell, CharacterEntry entry, Dictionary<string, int> slotOfUnit)
    {
        if (cell == null) return;

        //绑定来源：池化复用下异步头像回包以此校验（改绑即丢弃）
        cell.unitId = entry.unitId;

        //选中态：先静默置位再注册（注册按当前 isOn 计入集合，绑定不触发选中回调）
        if (cell.toggle != null)
        {
            cell.toggle.SetIsOnWithoutNotify(slotOfUnit.ContainsKey(entry.unitId));
            if (editToggleGroup != null) editToggleGroup.Register(cell.toggle);
        }
        RefreshEditItemVisual(cell, slotOfUnit);

        //头像：单图按需（Collection 变体）；未命中保留默认图
        if (cell.iconImg != null) _ = FillEditAvatarAsync(cell, entry.unitId);
    }

    //刷新单个列表项的编队标记：选中显示 Mark/NumBG/Num，Num 切对应槽位数字切片。
    private void RefreshEditItemVisual(TeamEditItemView cell, Dictionary<string, int> slotOfUnit)
    {
        //TryGet 失败时 slot 保持 -1（inTeam 为 false 不使用）
        int slot = -1;
        bool inTeam = !string.IsNullOrEmpty(cell.unitId) && slotOfUnit.TryGetValue(cell.unitId, out slot);

        if (cell.markImg != null) cell.markImg.gameObject.SetActive(inTeam);
        if (cell.numBgImg != null) cell.numBgImg.gameObject.SetActive(inTeam);
        if (cell.numImg == null) return;

        cell.numImg.gameObject.SetActive(inTeam);
        if (inTeam && cell.numSprites != null && slot >= 0 && slot < cell.numSprites.Length && cell.numSprites[slot] != null)
        {
            cell.numImg.sprite = cell.numSprites[slot];
        }
    }

    /// <summary>
    /// 编辑选中变化（多选组广播）：按选中顺序写回当前编队（新选中排尾、取消中间自动前移压缩），
    /// 即时保存；随后重排名单与主界面 8 槽（重绑静默置位，不再触发本回调）。
    /// </summary>
    private void OnEditSelectionChanged(IReadOnlyList<Toggle> onToggles)
    {
        List<string> unitIds = new List<string>(FormationTeamData.TeamSlotCount);
        for (int i = 0; i < onToggles.Count && i < FormationTeamData.TeamSlotCount; i++)
        {
            //Toggle 在 Icon 子节点上，视图在条目根上——必须向上找
            TeamEditItemView cell = onToggles[i].GetComponentInParent<TeamEditItemView>();
            if (cell != null && !string.IsNullOrEmpty(cell.unitId)) unitIds.Add(cell.unitId);
        }
        FormationTeamManager.Instance.SetTeam(currentTeamIndex, unitIds);

        RefreshTeamEditList();
        RefreshTeamSlots();
    }

    /// <summary>异步填充列表项头像；回包时列表项已改绑其他角色（池化复用）则丢弃结果。</summary>
    private async Task FillEditAvatarAsync(TeamEditItemView cell, string unitId)
    {
        Sprite avatar = await IconLibrary.GetCharacterAvatarAsync(IconLibrary.VariantCollection, unitId, ResourceScope);
        if (avatar == null || cell == null || cell.unitId != unitId) return;
        if (cell.iconImg != null) cell.iconImg.sprite = avatar;
    }

    #endregion

    #region 战斗流程

    /// <summary>
    /// 战斗按钮：校验当前编队非空后进入战斗测试场景（编队变更已实时保存，无需再提交）。
    /// </summary>
    private void StartBattle()
    {
        if (FormationTeamManager.Instance.GetTeamUnitIds(currentTeamIndex).Count == 0)
        {
            EventCenter.Instance.SetEventTrigger(EventNames.NoticeRequested, "Team is empty");
            return;
        }

        //清空面板栈：主城/选关/编队等栈内面板随进战斗整体关闭（NoticePanel 常驻不入栈不受影响）
        PanelManager.Instance.CloseAllPanels();
        LoadSceneManager.Instance.LoadSceneAsync(SceneNames.CombatTest);
    }

    #endregion

    #region 角色页切换

    /// <summary>
    /// 左右角色页循环切换：左页时切到右页，右页时切回左页（wrap 模式）。
    /// </summary>
    private void SwitchCharacterPage()
    {
        //编辑期间两页槽位列表整根收起，翻页不生效（防止把编辑态收起的列表重新显出）
        if (teamEditPanelRoot != null && teamEditPanelRoot.activeSelf) return;
        if (isLeftPage) ShowRightPage();
        else ShowLeftPage();
    }

    /// <summary>
    /// 显示左页并隐藏右页（整页根节点开关）。
    /// </summary>
    private void ShowLeftPage()
    {
        leftInfoList.gameObject.SetActive(true);
        rightInfoList.gameObject.SetActive(false);
        isLeftPage = true;
        ReplayPageStatTimelines(0); //左页 4 槽
    }

    /// <summary>
    /// 显示右页并隐藏左页（整页根节点开关）。
    /// </summary>
    private void ShowRightPage()
    {
        leftInfoList.gameObject.SetActive(false);
        rightInfoList.gameObject.SetActive(true);
        isLeftPage = false;
        ReplayPageStatTimelines(FormationTeamData.TeamSlotCount / 2); //右页 4 槽
    }

    /// <summary>
    /// 对指定起始槽的半页模型重播 Stat 展示：PlayableDirector 随 GameObject 失活停止，
    /// 重激活不自动续播（playOnAwake 仅 Awake 起效一次），翻页/编辑开关恢复后需显式重播。
    /// </summary>
    /// <param name="startSlot">本页首槽下标（左页 0 / 右页 4）。</param>
    private void ReplayPageStatTimelines(int startSlot)
    {
        int end = startSlot + FormationTeamData.TeamSlotCount / 2;
        for (int i = startSlot; i < end && i < slotModelInstances.Length; i++)
        {
            GameObject instance = slotModelInstances[i];
            if (instance == null) continue; //空槽或在途加载（加载完成后 ShowSlotModelAsync 自播）
            CultivateModelDisplay display = instance.GetComponent<CultivateModelDisplay>();
            if (display != null) display.Play(0, null);
        }
    }

    #endregion

    #region 角色拖拽换位

    /// <summary>
    /// 记录拖拽起手命中的槽位信息栏；未从槽位上起拖时为空。
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        dragSourceInfo = FindSlotInfo(eventData);
    }

    /// <summary>
    /// 拖拽过程不做跟随，松手时一次性判定换位。
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
    }

    /// <summary>
    /// 松手时命中另一槽位则交换两者编队数据并整体重刷；落回原位或空处不处理。
    /// 换位走数据交换而非位置交换：CHList 布局组启用，位置改动会被布局覆盖。
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        //未从槽位上起拖则不处理
        CharacterInformation source = dragSourceInfo;
        dragSourceInfo = null;
        if (source == null) return;

        CharacterInformation target = FindSlotInfo(eventData);
        if (target == null || target == source) return;

        int sourceSlot = IndexOfSlotInfo(source);
        int targetSlot = IndexOfSlotInfo(target);
        if (sourceSlot < 0 || targetSlot < 0) return;

        FormationTeamManager.Instance.TrySwapSlots(currentTeamIndex, sourceSlot, targetSlot);
        RefreshTeamSlots();
    }

    /// <summary>
    /// 从拖拽事件位置射线检测 UI，向上查找首个带角色信息栏的槽位节点（以本面板为界不越界匹配）。
    /// </summary>
    private CharacterInformation FindSlotInfo(PointerEventData eventData)
    {
        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            Transform node = result.gameObject.transform;
            while (node != null && node != transform)
            {
                CharacterInformation info = node.GetComponent<CharacterInformation>();
                if (info != null) return info;
                node = node.parent;
            }
        }
        return null;
    }

    /// <summary>查信息栏对应的槽位下标；不属于本面板槽位返回 -1。</summary>
    private int IndexOfSlotInfo(CharacterInformation info)
    {
        for (int i = 0; i < slotInfos.Length; i++)
        {
            if (slotInfos[i] == info) return i;
        }
        return -1;
    }

    #endregion

    /// <summary>
    /// 返回上级：关闭本面板；面板尚未纳入 PanelManager 管理时（场景常驻阶段）关闭会被忽略。
    /// </summary>
    private void CloseSelf()
    {
        PanelManager.Instance.ClosePanel(this);
    }
}
