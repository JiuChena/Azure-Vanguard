using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Inventory;
using Framework.Gameplay.Item;
using Framework.Gameplay.Player;
using Framework.Gameplay.Progression;
using Framework.Gameplay.Trinket;
using Framework.Gameplay.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// 角色养成面板控制层（MVC 的 C）：左侧角色池化选择列表 + 分页页签 + 属性页/武器页的技能书喂养升级。
/// 由角色列表面板点击列表项打开（OpenPanel 回调 Bind 传入角色）；数据经 CultivatePanelModel 中转。
/// 升级口径：书格 Icon 暂存 +1 / Reduce 暂存 -1 / Fill 自动填充 → LevelUp 整批确认结算；
/// 跨级另收升级消耗（信用点，只读格跟在书格后）；升级区与突破区互斥显隐（到上限切突破态），
/// 突破区只读展示下一阶段消耗，Breakthrough 按钮直接执行。
/// </summary>
public class CultivatePanel : PanelBase
{
    [Header("View 分区")]
    //面板视图分区：角色列表/页签/页面/属性页引用
    [SerializeField] private CultivatePanelView view;
    //顶部状态栏分区：返回/货币/加号/设置/回主页
    [SerializeField] private GearBar gearBar;
    //角色列表项预制体（CultivateCHListItem）：经 ObjectsPool 出池入池，序列化引用持有
    [SerializeField] private GameObject itemPrefab;

    //消耗条目预制体（CostItem）：经 ObjectsPool 出池入池，序列化引用持有
    [SerializeField] private GameObject costItemPrefab;

    //角色标签格子预制体（CHTag）：经 ObjectsPool 出池入池，序列化引用持有
    [SerializeField] private GameObject chTagPrefab;

    //装备背包条目预制体（BagItem）：经 ObjectsPool 出池入池，序列化引用持有
    [SerializeField] private GameObject bagItemPrefab;

    //装备背包筛选项预制体（TrinketTypeItem）：经 ObjectsPool 出池入池，序列化引用持有
    [SerializeField] private GameObject filterItemPrefab;

    //角色标签图集条目地址（Addressables 组内图集单条目，载入后按 GetSprite(名) 取切片；Inspector 可改）
    [SerializeField] private string tagAtlasAddress = "Tag";

    //模型加载标签约束：必须包含 Cultivate 标签，防止与 CombatSelect 面板同单位 ID 的展示模型交集串载
    private static readonly string[] ModelLabels = { "Cultivate" };

    //展示数据模型：Control 选中角色后填充
    private readonly CultivatePanelModel model = new CultivatePanelModel();

    //角色表缓存：InitAsync 就绪后可用（主键为字符串 id，如 CH0221）
    private DataTable<CharacterRow, string> characterTable;

    //武器表缓存：InitAsync 就绪后可用（主键 = 绑定角色 unitId，1:1 不可更换）
    private DataTable<WeaponRow, string> weaponTable;

    //物品表缓存：消耗条目图标按 itemId 查 ItemRow.icon（物品 id 为字符串）
    private DataTable<ItemRow, string> itemTable;

    //装备表缓存：装备槽位词条/图标按实例 itemId 查 TrinketRow（与 TrinketManager 同用 DataTableManager 缓存实例）
    private DataTable<TrinketRow, string> trinketTable;

    //合奏表缓存：生效合奏的展示文案按 ensembleId 查 EnsembleRow
    private DataTable<EnsembleRow, string> ensembleTable;

    //装备词条行模板缓存（键 = 文本组件）：首次替换捕获预制体静态串（应含 {0}/{1}），此后只动占位符
    private readonly Dictionary<TMP_Text, string> trinketStatTemplates = new Dictionary<TMP_Text, string>();

    //装备背包分区缓存（首次刷新时取自 view.trinketPage.bag）
    private TrinketBagView trinketBag;

    //装备背包交互是否已绑定（筛选按钮开合/关闭按钮，懒绑定一次）
    private bool trinketBagBound;

    //筛选选项是否已构建（两张表就绪后构建一次；类别 = TrinketTable 去重，套装 = EnsembleTable 全行）
    private bool trinketBagOptionsBuilt;

    //背包筛选状态（类别键/合奏 id；空集合 = 不过滤，组内多选并集，两组之间取交集）
    private readonly HashSet<string> trinketCategoryFilter = new HashSet<string>();
    private readonly HashSet<string> trinketEnsembleFilter = new HashSet<string>();

    //详情卡当前绑定的装备 id：异步图标回包校验（防串写）
    private string trinketMsgBoundItemId;

    //背包条目点击的换装目标槽（点槽位开背包时记录，0-2；-1 = 未从槽位进入，换装时兜底 0）
    private int trinketSwapSlotIndex = -1;

    //两页技能书 id（按品质升序，下标即书格顺序；Inspector 可覆盖，默认角色书 Exp_Character_0-3 / 武器书 Exp_Weapon_0-3）
    [SerializeField] private string[] statBookItemIds = { "Exp_Character_0", "Exp_Character_1", "Exp_Character_2", "Exp_Character_3" };
    [SerializeField] private string[] weaponBookItemIds = { "Exp_Weapon_0", "Exp_Weapon_1", "Exp_Weapon_2", "Exp_Weapon_3" };

    //两页书格运行时缓存（EnsureBookCells 按 bookContent 动态生成，固定 4 格不入池）
    private CostItemView[] statBookCells;
    private CostItemView[] weaponBookCells;

    //两页暂存使用量（下标对齐书 id 数组）：Icon +1 / Reduce -1 / Fill 整批预暂存 / LevelUp 确认结算后清零
    private long[] statStaged = Array.Empty<long>();
    private long[] weaponStaged = Array.Empty<long>();

    //两页 Has 文本模板缓存：首次填充取格子静态串（应含 {0}/{1}），此后只改占位符
    private string statHasTemplate;
    private string weaponHasTemplate;

    //消耗图标缓存：itemId → sprite（IconLibrary 失败不入缓存，下次重试）
    private readonly Dictionary<string, Sprite> costIconCache = new Dictionary<string, Sprite>();

    //技能图标缓存：SkillRow.icon 地址 → sprite（Sprite-Character-Skill-Icon 组；失败不入缓存，下次重试）
    private readonly Dictionary<string, Sprite> skillIconCache = new Dictionary<string, Sprite>();

    //标签图集加载任务（去重并发；成功后常驻复用，失败清空下次刷新重试）
    private Task<SpriteAtlas> tagAtlasTask;

    //突破材料充足/不足文案色（用户拍板：深绿 008E0C / 深红 8E0000）
    private static readonly Color BreakthroughEnoughColor = new Color32(0x00, 0x8E, 0x0C, 0xFF);
    private static readonly Color BreakthroughShortageColor = new Color32(0x8E, 0x00, 0x00, 0xFF);

    //ComponentInit 是否已执行：Bind 依赖此标记区分"首开预绑"与"已开补挂"
    private bool started;

    //当前模型租约：切换角色/关闭面板时 Dispose 归还引用计数
    private ResourceLease<GameObject> modelLease;
    //当前模型实例：挂在展示区锚点下的实例（非预制体资产）
    private GameObject modelInstance;
    //当前实例上的分页展示组件：预制体未挂时为 null（仅静态展示）
    private CultivateModelDisplay modelDisplay;
    //当前已加载模型的角色 ID：关板扫漏按此查缓存残留
    private string loadedModelId;
    //当前页下标：模型展示跟随切页
    private int currentPageIndex;
    //加载竞态令牌：连点切角/关板后，旧加载结果按令牌失效直接归还
    private int modelLoadToken;

    //技能格下标 → 技能类型（SkillRow.SkillTypes 取值；与 Abilities 子节点顺序对齐 普攻/天赋/E/Q）
    private static readonly string[] AbilitySlotTypes = { SkillRow.SkillTypes.Normal, SkillRow.SkillTypes.Talent, SkillRow.SkillTypes.E, SkillRow.SkillTypes.Burst };

    //技能格当前行缓存（下标对齐技能格；无行 = null，对应格隐藏）
    private SkillRow[] abilityRows = Array.Empty<SkillRow>();

    //当前详情展示的技能格下标（-1 = 未展示；刷新时原选中仍有效则保持）
    private int abilityDetailIndex = -1;

    protected override void EventInit()
    {
        //返回按钮为本面板语义，手动注册返回回调（货币刷新/默认按钮由 GearBar 自管理，面板零接线）
        if (gearBar != null) gearBar.RegisterGoBack(CloseSelf);

        //页签切换：互斥与页面激活由 Control 维护（预制体无 ToggleGroup）
        if (view != null && view.pageToggles != null)
        {
            for (int i = 0; i < view.pageToggles.Length; i++)
            {
                int index = i;
                view.pageToggles[i].onValueChanged.AddListener(on =>
                {
                    if (on) ShowPage(index);
                });
            }
        }

        //属性页喂养操作：书格 Icon 暂存 +1 / Reduce 暂存 -1，Fill 自动填充，LevelUp 确认结算，Breakthrough 直接执行
        if (view != null && view.statPage != null)
        {
            BindFeedSection(view.statPage.fillButton,
                view.statPage.levelUpButton, view.statPage.breakthroughButton, true);
        }

        //武器页喂养操作：口径与属性页同（对象是武器技能书与武器等级），突破按钮同执行角色突破
        if (view != null && view.weaponPage != null)
        {
            BindFeedSection(view.weaponPage.fillButton,
                view.weaponPage.levelUpButton, view.weaponPage.breakthroughButton, false);
        }

        //技能页技能格切换：ToggleGroup 单选（预制体），Mark 由 Toggle transition 驱动，控制层只刷详情区
        if (view != null && view.abilityPage != null && view.abilityPage.skillToggles != null)
        {
            for (int i = 0; i < view.abilityPage.skillToggles.Length; i++)
            {
                int index = i;
                view.abilityPage.skillToggles[i].onValueChanged.AddListener(on =>
                {
                    if (on) ShowAbilityDetail(index);
                });
            }

            //升级按钮直接执行（交互与突破同款）；消耗列表为纯展示，无按钮可绑
            if (view.abilityPage.upgradeButton != null) view.abilityPage.upgradeButton.onClick.AddListener(OnSkillUpgradeClicked);
        }

        //仓库变更 → 展示自动跟随（获得角色刷列表，养成数据刷当前展示，装备数据刷装备页）
        EventCenter.Instance.Register(EventNames.CharacterAcquired, OnCharacterAcquired);
        EventCenter.Instance.Register(EventNames.CharacterDataUpdated, OnCharacterDataUpdated);
        EventCenter.Instance.Register(EventNames.TrinketUpdated, OnTrinketUpdated);
        //背包变化 → 当前页消耗展示跟随（升级/突破列表的持有量文本与染色）
        EventCenter.Instance.Register(EventNames.BagUpdated, OnBagUpdated);
    }

    protected override void ComponentInit()
    {
        started = true;
        InitAsync();
    }

    /// <summary>
    /// 面板销毁时撤销事件监听，防止向已销毁的组件写入。
    /// 池化单元格留在 ObjectsPool 根节点，面板销毁不影响，下次打开直接复用。
    /// </summary>
    protected override void OnDestroy()
    {
        //模型展示兜底回收：实例销毁 + 租约归还 + 缓存残留扫漏
        ReleaseModelAndSweep();
        EventCenter.Instance.Unregister(EventNames.CharacterAcquired, OnCharacterAcquired);
        EventCenter.Instance.Unregister(EventNames.CharacterDataUpdated, OnCharacterDataUpdated);
        EventCenter.Instance.Unregister(EventNames.TrinketUpdated, OnTrinketUpdated);
        EventCenter.Instance.Unregister(EventNames.BagUpdated, OnBagUpdated);
        base.OnDestroy();
    }

    #region 对外接口

    /// <summary>
    /// 绑定要查看的角色。两条路径：OpenPanel 回调（Start 前调用，仅记录，InitAsync 按此选中）；
    /// 面板已开时补挂（OpenPanel 重名去重会吞回调，调用方先用 GetPanel 探测后直呼本方法）。
    /// </summary>
    /// <param name="unitId">要查看的角色 ID（如 CH0221）。</param>
    public void Bind(string unitId)
    {
        if (model.UnitId == unitId) return;
        model.UnitId = unitId;
        if (started) _ = SelectCharacterAsync(unitId);
    }

    #endregion

    #region 初始化与选择

    /// <summary>养成表就绪 → 刷角色列表 → 选中绑定角色（未绑定或已不拥有时落到第一个）。</summary>
    private async void InitAsync()
    {
        try
        {
            if (!await CharacterProgressionManager.Instance.EnsureReadyAsync())
            {
                Debug.LogError("[CultivatePanel] 养成表加载失败，面板数据为空");
                return;
            }

            characterTable = await DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable");
            weaponTable = await DataTableManager.Instance.LoadTableAsync<WeaponRow, string>("WeaponTable");
            itemTable = await DataTableManager.Instance.LoadTableAsync<ItemRow, string>("ItemTable");
            trinketTable = await DataTableManager.Instance.LoadTableAsync<TrinketRow, string>("TrinketTable");
            ensembleTable = await DataTableManager.Instance.LoadTableAsync<EnsembleRow, string>("EnsembleTable");

            //默认页：属性页（预制体四个页面均为隐藏态）
            ShowPage(0);
            if (view != null && view.pageToggles != null && view.pageToggles.Length > 0
                && view.pageToggles[0] != null)
            {
                view.pageToggles[0].SetIsOnWithoutNotify(true);
            }

            RefreshBar();
            if (string.IsNullOrEmpty(model.UnitId) || !CharacterWarehouseManager.Instance.HasCharacter(model.UnitId))
            {
                IReadOnlyList<CharacterEntry> snapshot = CharacterWarehouseManager.Instance.Snapshot();
                if (snapshot.Count > 0) await SelectCharacterAsync(snapshot[0].unitId);
            }
            else
            {
                await SelectCharacterAsync(model.UnitId);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// 选中角色：取仓库条目 + 表行 + 属性结算，填充模型后刷新属性页与列表选中态。
    /// </summary>
    private async Task SelectCharacterAsync(string unitId)
    {
        try
        {
            if (!CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry))
            {
                Debug.LogError($"[CultivatePanel] 角色 {unitId} 未拥有，忽略选择");
                return;
            }

            //切角重置两页暂存：暂存量属于上一角色；同角色回流刷新（广播）不清暂存
            if (model.UnitId != unitId)
            {
                Array.Clear(statStaged, 0, statStaged.Length);
                Array.Clear(weaponStaged, 0, weaponStaged.Length);
            }

            model.UnitId = unitId;
            model.Entry = entry;
            model.Row = characterTable != null && characterTable.TryGet(unitId, out CharacterRow row) ? row : null;
            if (model.Row == null) Debug.LogError($"[CultivatePanel] CharacterTable 无角色 {unitId} 的行");

            //显示名取表行 Name（表驱动：名字随表走，面板不再加载 UnitConfig；行缺失时上面已报错）
            model.UnitName = model.Row != null && !string.IsNullOrEmpty(model.Row.Name) ? model.Row.Name : null;

            model.Stats = await CharacterProgressionManager.Instance.CalculateStatsAsync(unitId);

            UpdateStatPage();
            UpdateBarSelection();
            _ = RefreshFeedSectionAsync(true);
            _ = RefreshWeaponPageAsync();
            _ = RefreshTrinketPageAsync();
            _ = RefreshAbilityPageAsync();
            await ShowModelAsync(unitId);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    #endregion

    #region 角色列表（池化）

    /// <summary>
    /// 刷新左侧角色列表：旧单元格全部入池，再按仓库快照从列表项预制体逐个取出重新赋值——增删角色不销毁重建。
    /// </summary>
    private void RefreshBar()
    {
        if (view == null || view.characterBar == null
            || view.characterBar.content == null || itemPrefab == null) return;

        //旧单元格全部入池（ObjectsPool 挂池根并禁用）
        for (int i = view.characterBar.content.childCount - 1; i >= 0; i--)
        {
            Transform child = view.characterBar.content.GetChild(i);
            if (child.GetComponent<CultivateCharacterItem>() != null)
                ObjectsPool.Instance.Put(child.gameObject);
        }

        foreach (CharacterEntry entry in CharacterWarehouseManager.Instance.Snapshot())
        {
            GameObject go = ObjectsPool.Instance.Get(itemPrefab, view.characterBar.content);
            CultivateCharacterItem cell = go != null ? go.GetComponent<CultivateCharacterItem>() : null;
            if (cell == null) continue;

            //挂载后归一变换：池的 Instantiate 在无父状态克隆，模板的局部尺度/位置被当作世界尺度，
            //挂回非 1 尺度的画布链时新实例的 scale 与位置同被放大 1/P 倍（实测 108 倍）——
            //scale 显式归一，localPosition 三轴归零含 z 轴（GridLayout 接管排布前避免错位）
            if (go != null)
            {
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = Vector3.zero;
            }

            string unitId = entry.unitId;
            cell.unitId = unitId;
            if (cell.toggle != null)
            {
                //池化复用：清掉上一任数据的监听再重绑；选中即切换角色，取消选中强制弹回（单选语义）
                cell.toggle.onValueChanged.RemoveAllListeners();
                cell.toggle.onValueChanged.AddListener(on =>
                {
                    if (cell.markList != null) cell.markList.ToggleSet(on);
                    if (on) _ = SelectCharacterAsync(unitId);
                    else cell.toggle.SetIsOnWithoutNotify(true);
                });
            }

            //头像：单图按需（CHAvatar_Small_<unitId>）；未命中保留默认图
            if (cell.icon != null) _ = FillBarAvatarAsync(cell, unitId);

            UpdateBarCellSelection(cell, unitId == model.UnitId);
        }
    }

    /// <summary>异步填充左列单元格头像；回包时单元格已改绑其他角色（池化复用）则丢弃结果。</summary>
    private async Task FillBarAvatarAsync(CultivateCharacterItem cell, string unitId)
    {
        Sprite avatar = await IconLibrary.GetCharacterAvatarAsync(IconLibrary.VariantSmall, unitId, ResourceScope);
        if (avatar == null || cell == null || cell.unitId != unitId) return;
        if (cell.icon != null) cell.icon.sprite = avatar;
    }

    /// <summary>刷新列表各单元格选中态（SetIsOnWithoutNotify 不触发 onValueChanged，无回环）。</summary>
    private void UpdateBarSelection()
    {
        if (view == null || view.characterBar == null || view.characterBar.content == null) return;
        for (int i = 0; i < view.characterBar.content.childCount; i++)
        {
            CultivateCharacterItem cell = view.characterBar.content.GetChild(i).GetComponent<CultivateCharacterItem>();
            if (cell != null) UpdateBarCellSelection(cell, cell.unitId == model.UnitId);
        }
    }

    /// <summary>单元格选中态：开关与选中标记同步；标记在监听重绑时随初始态对齐。</summary>
    private void UpdateBarCellSelection(CultivateCharacterItem cell, bool selected)
    {
        if (cell == null) return;
        if (cell.toggle != null) cell.toggle.SetIsOnWithoutNotify(selected);
        if (cell.markList != null) cell.markList.ToggleSet(selected);
    }

    #endregion

    #region 页签与页面

    /// <summary>
    /// 切换页面：只激活下标命中的页面，模型展示同步切页。Ascend（突破页）等未建页面占位提示，不切页。
    /// </summary>
    private void ShowPage(int index)
    {
        if (view == null || view.pages == null) return;
        if (index < 0 || index >= view.pages.Length)
        {
            Debug.Log($"[CultivatePanel] 页签 {index} 对应页面未建（TODO：Ascend 突破页随突破 UI 落地）");
            return;
        }
        currentPageIndex = index;
        for (int i = 0; i < view.pages.Length; i++)
        {
            if (view.pages[i] != null) view.pages[i].SetActive(i == index);
        }
        //进入武器页/装备页/技能页时重刷一次：切角发生在其他页时数据可能已过期
        if (index == 1) _ = RefreshWeaponPageAsync();
        if (index == 2) _ = RefreshTrinketPageAsync();
        if (index == 3) _ = RefreshAbilityPageAsync();
        RefreshModelPage();
    }

    #endregion

    #region 角色模型展示

    /// <summary>
    /// 加载并展示角色模型：按单位 ID + UI/Non-Combat 标签交集解析（模型预制体与 UnitConfigs 的 SO
    /// 同址不同型，加载以类型参数过滤），实例挂展示区锚点并归一变换；切换角色先卸旧再加载，
    /// 加载等待期间连点切角/关板由令牌判失效、结果直接归还。
    /// 同角短路：已挂载或在途（升级/突破广播回流刷新）直接返回——模型内容与数值无关，
    /// 不重载也消掉并发刷新双加载竞态（输家退租把共享句柄计数砸到 0）的触发源头。
    /// </summary>
    /// <param name="unitId">角色 ID（模型 Addressable 地址，如 CH0221）。</param>
    private async Task ShowModelAsync(string unitId)
    {
        if (loadedModelId == unitId) return;

        int token = ++modelLoadToken;
        ReleaseModel();
        loadedModelId = unitId; //在途占位：加载完成前同角再入同样短路（双广播只留一路加载）

        ResourceLease<GameObject> lease;
        try
        {
            lease = await AddressableManager.Instance.AcquireAssetWithLabelsAsync<GameObject>(unitId, ModelLabels);
        }
        catch (Exception e)
        {
            loadedModelId = null; //失败清占位，下次刷新可重试
            Debug.LogError($"[CultivatePanel] 角色模型加载失败：{unitId}（未登记 Addressable 或缺 UI/Non-Combat 标签）：{e.Message}");
            return;
        }

        //令牌失效：等待期间已切角/关板，新租约立即归还避免悬挂引用（占位已归新链，不清）
        if (token != modelLoadToken)
        {
            lease.Dispose();
            return;
        }

        //锚点缺失：展示区未接或资产为空时不挂模型
        Transform anchor = view != null && view.displayView != null ? view.displayView.pos : null;
        if (anchor == null || lease.Asset == null)
        {
            Debug.LogError("[CultivatePanel] 展示区锚点缺失，模型未挂载");
            lease.Dispose();
            loadedModelId = null;
            return;
        }

        modelLease = lease;
        modelInstance = Instantiate(lease.Asset, anchor);
        modelInstance.name = unitId;
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;
        modelDisplay = modelInstance.GetComponent<CultivateModelDisplay>();
        RefreshModelPage();
    }

    /// <summary>模型跟随切页：交给实例上的分页展示组件（未挂组件的模型仅静态展示）。</summary>
    private void RefreshModelPage()
    {
        if (modelDisplay != null) modelDisplay.Play(currentPageIndex, view != null ? view.displayView : null);
    }

    /// <summary>卸载当前模型：销毁实例 + 归还租约（引用计数归零时 AddressableManager 释放底层资源）。</summary>
    private void ReleaseModel()
    {
        if (modelInstance != null) Destroy(modelInstance);
        modelInstance = null;
        modelDisplay = null;
        modelLease?.Dispose();
        modelLease = null;
        loadedModelId = null;
    }

    /// <summary>
    /// 卸载并扫漏：面板是模型资源唯一持有者，归还后缓存不应再有该资源；
    /// 仍有残留说明存在漏释放，记 Error 当场暴露。
    /// </summary>
    private void ReleaseModelAndSweep()
    {
        //先记下扫漏凭据：模型按标签加载、条目按 InternalId 入账，扫漏必须用租约的精确 ResourceKey（与加载键同模式）
        string releasedKey = modelLease?.ResourceKey;
        ReleaseModel();
        if (string.IsNullOrEmpty(releasedKey)) return;
        if (AddressableManager.Instance.TryGetDebugInfoByResourceKey(releasedKey, out ResourceDebugInfo info))
            Debug.LogError($"[CultivatePanel] 扫漏：模型资源未释放干净 {releasedKey}，剩余引用 {info.ReferenceCount}");
    }

    #endregion

    #region 武器页

    /// <summary>
    /// 刷新武器页：武器名/图标/等级/白值/主属性/特殊词条/星级。
    /// 白值与主属性取 <see cref="CharacterProgressionManager.GetWeaponContributionAsync"/> 的结算口径，
    /// 展示与属性结算同源；表行缺失时报错跳过，页面保留旧值。
    /// </summary>
    private async Task RefreshWeaponPageAsync()
    {
        if (view == null || view.weaponPage == null || !model.HasData) return;
        CultivateWeaponPageView page = view.weaponPage;
        string unitId = model.UnitId;

        if (weaponTable == null || !weaponTable.TryGet(unitId, out WeaponRow weaponRow))
        {
            Debug.LogError($"[CultivatePanel] WeaponTable 无武器行 {unitId}");
            return;
        }

        WeaponStatContribution weapon = await CharacterProgressionManager.Instance.GetWeaponContributionAsync(unitId);

        //回包时已切角（池化/连点）则丢弃，防止串写
        if (model.UnitId != unitId) return;

        if (page.nameText != null) page.nameText.text = weaponRow.Name;

        //武器图标：单图按需（CHWeapon_<unitId>，用户 WeaponIcon 组地址约定）；未命中保留默认图
        if (page.iconImg != null) _ = FillWeaponIconAsync(page.iconImg, unitId);

        //星级 = 角色稀有度（武器 1:1 绑定，与属性页同口径显隐）
        if (page.stars != null)
        {
            for (int i = 0; i < page.stars.Length; i++)
            {
                if (page.stars[i] != null) page.stars[i].gameObject.SetActive(i < model.Row.Rarity);
            }
        }

        //武器经验进度条与等级文本：满级时隐藏进度条并显示 "Lv.{当前} - MAX"，非满级时按比例驱动（与属性页同口径）
        int weaponLevel = Math.Max(1, model.Entry.weaponLevel);
        bool isWeaponMax = weaponLevel >= model.LevelCap;
        if (page.levelText != null && isWeaponMax) page.levelText.text = $"Lv.{weaponLevel} - MAX";
        if (page.progressFillRect != null)
        {
            if (isWeaponMax)
            {
                SetExpProgressBarActive(page.progressFillRect, false);
            }
            else
            {
                SetExpProgressBarActive(page.progressFillRect, true);
                Vector2 anchorMax = page.progressFillRect.anchorMax;
                anchorMax.x = model.WeaponExpProgress;
                page.progressFillRect.anchorMax = anchorMax;
            }
        }

        //白值：Top 区 "Base Stats" 下的数值与类型图标（无武器贡献时显示 0）
        if (page.baseStatText != null)
        {
            page.baseStatText.text = weapon.HasWeapon ? weapon.WhiteValue.ToString() : "0";
        }

        //主属性：Bot 区名 + 数值与类型图标（当前只有一条主属性，名按 MainStatType 写英文键名，数值按百分比格式化）
        if (page.mainStatNameText != null) page.mainStatNameText.text = MainStatName(weaponRow.MainStatType);
        if (page.mainStatValueText != null)
        {
            page.mainStatValueText.text = weapon.BonusValues.Length > 0 ? FormatPercent(weapon.BonusValues[0]) : "-";
        }

        // 异步更新武器基础属性与主属性图标
        _ = UpdateWeaponPropertyIconsAsync(page.baseStatIcon, weaponRow.BaseStatType, page.mainStatIcon, weaponRow.MainStatType, unitId);

        //特殊词条：展示文本（战斗框架落地前不生效）
        if (page.specialEffectText != null && !string.IsNullOrEmpty(weaponRow.SpecialDesc))
        {
            page.specialEffectText.text = weaponRow.SpecialDesc;
        }

        //喂养区：技能书格 + 突破列表（与属性页同口径）
        await RefreshFeedSectionAsync(false);
    }

    /// <summary>
    /// 异步更新武器页 BasicProperty 分区的白值属性图标与主属性图标。
    /// 白值类型仅限小攻击(Tag_SubDPS)、小生命(Tag_Bruiser)、小防御(Tag_Defender)；
    /// 主属性为百分比数值(Tag_SubDPS/Tag_Bruiser/Tag_Defender/Tag_MainDPS/Tag_Buffer/Tag_Healer/Tag_SetBonus)。
    /// </summary>
    /// <param name="baseIcon">白值属性 Image 组件。</param>
    /// <param name="baseStatType">白值类型（atk/hp/def）。</param>
    /// <param name="mainIcon">主属性 Image 组件。</param>
    /// <param name="mainStatType">主属性类型（如 crit_rate/dmg_bonus/atk_pct 等）。</param>
    /// <param name="unitId">请求发起时的单位 ID，用于异步回包时防串写。</param>
    /// <returns>异步任务。</returns>
    private async Task UpdateWeaponPropertyIconsAsync(Image baseIcon, string baseStatType, Image mainIcon, string mainStatType, string unitId)
    {
        // 校验至少有一个图标组件需要更新
        if (baseIcon == null && mainIcon == null) return;

        // 获取 Tag 图集
        SpriteAtlas atlas = await EnsureTagAtlasAsync();

        // 校验面板数据与角色一致性
        if (!model.HasData || model.UnitId != unitId || atlas == null) return;

        // 更新白值属性图标
        if (baseIcon != null)
        {
            string baseSpriteName = GetWeaponBaseStatSpriteName(baseStatType);
            Sprite baseSprite = atlas.GetSprite(baseSpriteName) ?? (baseSpriteName == "Tag_Defender" ? atlas.GetSprite("Tag_Small_Defence") : null);
            if (baseSprite != null)
            {
                baseIcon.sprite = baseSprite;
                baseIcon.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[CultivatePanel] 未在 Tag 图集中找到武器基础属性图标: {baseSpriteName} (baseStatType={baseStatType})");
            }
        }

        // 更新主属性图标
        if (mainIcon != null)
        {
            string mainSpriteName = GetWeaponMainStatSpriteName(mainStatType);
            Sprite mainSprite = atlas.GetSprite(mainSpriteName) ?? (mainSpriteName == "Tag_Defender" ? atlas.GetSprite("Tag_Small_Defence") : (mainSpriteName == "Tag_Healer" ? atlas.GetSprite("Tag_Small_Heal") : null));
            if (mainSprite != null)
            {
                mainIcon.sprite = mainSprite;
                mainIcon.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[CultivatePanel] 未在 Tag 图集中找到武器主属性图标: {mainSpriteName} (mainStatType={mainStatType})");
            }
        }
    }

    /// <summary>
    /// 根据武器白值属性类型解析 Tag 图集中对应的 Sprite 名称。
    /// 基础属性支持：小攻击(Tag_SubDPS)、小生命(Tag_Bruiser)、小防御(Tag_Defender)。
    /// </summary>
    /// <param name="statType">白值类型字符串（atk / hp / def）。</param>
    /// <returns>Tag 图集切片名称。</returns>
    private static string GetWeaponBaseStatSpriteName(string statType)
    {
        if (string.IsNullOrWhiteSpace(statType)) return "Tag_SubDPS";

        switch (statType.ToLowerInvariant())
        {
            case "atk":
            case "attack":
                return "Tag_SubDPS";

            case "hp":
            case "health":
            case "max_hp":
            case "bruiser":
                return "Tag_Bruiser";

            case "def":
            case "defence":
            case "defense":
            case "defender":
                return "Tag_Defender";

            default:
                return "Tag_SubDPS";
        }
    }

    /// <summary>
    /// 根据武器主属性类型解析 Tag 图集中对应的 Sprite 名称。
    /// 映射规则：
    /// 百分比攻击力(atk_pct) → Tag_SubDPS；
    /// 百分比生命值(hp_pct) → Tag_Bruiser；
    /// 百分比防御力(def_pct) → Tag_Defender；
    /// 百分比暴击率(crit_rate) → Tag_MainDPS；
    /// 暴击伤害(crit_dmg/crit_empower) → Tag_Buffer；
    /// 治疗加成(heal_bonus) → Tag_Healer；
    /// 伤害加成(dmg_bonus/burst_pierce) → Tag_SetBonus。
    /// </summary>
    /// <param name="statType">主属性键名。</param>
    /// <returns>Tag 图集切片名称。</returns>
    private static string GetWeaponMainStatSpriteName(string statType)
    {
        if (string.IsNullOrWhiteSpace(statType)) return "Tag_SetBonus";

        switch (statType.ToLowerInvariant())
        {
            case "atk_pct":
            case "atk":
            case "subdps":
                return "Tag_SubDPS";

            case "hp_pct":
            case "hp":
            case "bruiser":
                return "Tag_Bruiser";

            case "def_pct":
            case "def":
            case "defence":
            case "defense":
            case "defender":
                return "Tag_Defender";

            case "crit_rate":
            case "maindps":
                return "Tag_MainDPS";

            case "crit_dmg":
            case "crit_empower":
            case "buffer":
                return "Tag_Buffer";

            case "heal_bonus":
            case "heal":
            case "healer":
                return "Tag_Healer";

            case "dmg_bonus":
            case "burst_pierce":
            case "setbonus":
            case "set_bonus":
                return "Tag_SetBonus";

            case "energy_recharge":
                return "Tag_CostReduce";

            default:
                return "Tag_SetBonus";
        }
    }

    /// <summary>异步填充武器图标；回包时已切角则丢弃结果。</summary>
    private async Task FillWeaponIconAsync(Image icon, string unitId)
    {
        Sprite iconSprite = await IconLibrary.GetAsync($"CHWeapon_{unitId}", ResourceScope);
        if (iconSprite == null || icon == null || model.UnitId != unitId) return;
        icon.sprite = iconSprite;
    }

    /// <summary>主属性显示名：按主属性键写英文（对齐预制体静态样例 "CritRate" 的拼法）。</summary>
    private static string MainStatName(string statType)
    {
        switch (statType)
        {
            case "atk": return "Atk";
            case "hp": return "Hp";
            case "def": return "Def";
            case "crit_rate": return "CritRate";
            case "crit_dmg": return "CritDmg";
            case "crit_empower": return "CritEmpower";
            case "dmg_bonus": return "DmgBonus";
            case "heal_bonus": return "HealBonus";
            case "energy_recharge": return "EnergyRecharge";
            case "atk_pct": return "AtkPct";
            case "def_pct": return "DefPct";
            case "hp_pct": return "HpPct";
            case "burst_pierce": return "BurstPierce";
            default: return statType;
        }
    }

    #endregion

    #region 装备页

    /// <summary>装备数据变化（发放/穿脱）→ 装备页回流刷新。</summary>
    private void OnTrinketUpdated()
    {
        if (model.HasData) _ = RefreshTrinketPageAsync();
    }

    /// <summary>
    /// 刷新装备页：逐槽写空态/图标/主副词条，合奏区写生效描述。
    /// 在穿实例 = TrinketManager.GetWornBy（≤3，发放顺序，与身上三格一一对应）；
    /// 词条展示直读 TrinketTable 行（表配死无成长）；空槽 = NullText 激活 + 全词条 "-"。
    /// </summary>
    private async Task RefreshTrinketPageAsync()
    {
        if (view == null || view.trinketPage == null || !model.HasData) return;
        CultivateTrinketPageView page = view.trinketPage;
        string unitId = model.UnitId;

        //表缺失时按需补载（InitAsync 预载失败时的兜底，命中 DataTableManager 缓存则零代价）
        if (trinketTable == null) trinketTable = await DataTableManager.Instance.LoadTableAsync<TrinketRow, string>("TrinketTable");
        if (ensembleTable == null) ensembleTable = await DataTableManager.Instance.LoadTableAsync<EnsembleRow, string>("EnsembleTable");

        List<TrinketInstance> worn = TrinketManager.Instance.GetWornBy(unitId);

        //等待期间已切角则丢弃，防止串写
        if (model.UnitId != unitId || !model.HasData) return;

        //先统一捕获词条/套装名模板：必须在任何落显（含空槽 "-" 覆写）之前，否则预制体模板串会丢失
        CaptureTrinketStatTemplates(page);

        if (page.slots != null)
        {
            for (int i = 0; i < page.slots.Length; i++)
            {
                TrinketSlotView slot = page.slots[i];
                if (slot == null) continue;

                TrinketInstance inst = i < worn.Count ? worn[i] : null;

                //表行缺失按空槽处理（配置错误由服务层报错定位，此处保页面可用）
                if (inst == null || trinketTable == null || !trinketTable.TryGet(inst.itemId, out TrinketRow row))
                {
                    if (inst != null) Debug.LogError($"[CultivatePanel] TrinketTable 无装备行 {inst.itemId}");
                    BindEmptySlot(slot);
                    continue;
                }

                slot.instanceId = inst.instanceId;
                if (slot.nullText != null) slot.nullText.gameObject.SetActive(false);
                //Icon 节点预制体默认失活（空槽态），有件必须激活否则图加载了也不渲染
                if (slot.iconImg != null)
                {
                    slot.iconImg.gameObject.SetActive(true);
                    _ = FillTrinketIconAsync(slot, inst.instanceId, row.Icon);
                }

                if (slot.mainStatText != null)
                {
                    slot.mainStatText.text = FormatStatLine(slot.mainStatText, row.MainStatType, row.MainStatValue);
                }
                WriteSubStat(slot.subStatTexts, 0, row.SubStat1Type, row.SubStat1Value);
                WriteSubStat(slot.subStatTexts, 1, row.SubStat2Type, row.SubStat2Value);
                WriteSubStat(slot.subStatTexts, 2, row.SubStat3Type, row.SubStat3Value);

                //套装名：该件配了合奏写 "套装：<名>"（模板替换），无合奏 "-"
                WriteTrinketSetName(slot.setBonusNameText, row.EnsembleId);
            }
        }

        //合奏区：三件套生效时写展示文案（名 + 描述），未触发清空
        if (page.setBonusText != null)
        {
            string ensembleId = TrinketManager.Instance.EvaluateEnsemble(worn);
            if (!string.IsNullOrEmpty(ensembleId) && ensembleTable != null
                && ensembleTable.TryGet(ensembleId, out EnsembleRow ensemble))
            {
                page.setBonusText.text = string.IsNullOrEmpty(ensemble.Desc)
                    ? ensemble.Name
                    : $"{ensemble.Name}\n{ensemble.Desc}";
            }
            else
            {
                page.setBonusText.text = string.Empty;
            }
        }

        //背包分区跟随刷新（发放/穿脱/进页/切角共用此链）
        _ = RefreshTrinketBagAsync();
    }

    /// <summary>空槽落显：NullText 激活、全词条与套装名 "-"、实例标记清零（在途图标回包按此丢弃）。</summary>
    private static void BindEmptySlot(TrinketSlotView slot)
    {
        slot.instanceId = 0;
        if (slot.nullText != null) slot.nullText.gameObject.SetActive(true);
        //Icon 节点失活与 NullText 显隐对称（防预制体默认激活时空槽露出默认图）
        if (slot.iconImg != null) slot.iconImg.gameObject.SetActive(false);
        if (slot.mainStatText != null) slot.mainStatText.text = "-";
        if (slot.setBonusNameText != null) slot.setBonusNameText.text = "-";
        if (slot.subStatTexts == null) return;
        for (int j = 0; j < slot.subStatTexts.Length; j++)
        {
            if (slot.subStatTexts[j] != null) slot.subStatTexts[j].text = "-";
        }
    }

    /// <summary>副词条落显：格子缺失跳过；表未配词条键（空串）按 "-"。</summary>
    private void WriteSubStat(TMP_Text[] cells, int index, string statType, float value)
    {
        if (cells == null || index < 0 || index >= cells.Length || cells[index] == null) return;
        cells[index].text = string.IsNullOrEmpty(statType)
            ? "-"
            : FormatStatLine(cells[index], statType, value);
    }

    /// <summary>异步填充装备图标（TrinketRow.icon 单图按需）；回包时槽位已改绑其他实例则丢弃。</summary>
    private async Task FillTrinketIconAsync(TrinketSlotView slot, ulong instanceId, string iconAddress)
    {
        if (string.IsNullOrEmpty(iconAddress)) return;
        Sprite sprite = await IconLibrary.GetAsync(iconAddress, ResourceScope);
        if (sprite == null || slot == null || slot.instanceId != instanceId) return;
        slot.iconImg.sprite = sprite;
    }

    /// <summary>
    /// 词条行落显：预制体静态串含 {0}/{1} 占位时按模板替换（{0}=加成名称、{1}=数值），
    /// 首次替换捕获模板缓存，此后只动占位符；模板缺失时直写完整串。
    /// </summary>
    private string FormatStatLine(TMP_Text text, string statType, float value)
    {
        string name = MainStatName(statType);
        if (!trinketStatTemplates.TryGetValue(text, out string template)
            || string.IsNullOrEmpty(template) || !template.Contains("{0}"))
        {
            if (text.text.Contains("{0}") && text.text.Contains("{1}"))
            {
                template = text.text;
                trinketStatTemplates[text] = template;
            }
            else
            {
                //模板缺失：直写完整串（口径与模板替换一致）
                return $"{name} +{FormatStatValue(statType, value)}{(IsPercentStatKey(statType) ? "%" : "")}";
            }
        }

        string val = FormatStatValue(statType, value);
        if (IsPercentStatKey(statType))
        {
            //百分比键：% 由模板携带（"{1}%" → "1.5%"）；模板漏写 % 时补在数值后
            return template.Contains("{1}%")
                ? template.Replace("{0}", name).Replace("{1}", val)
                : template.Replace("{0}", name).Replace("{1}", $"{val}%");
        }
        //平面键：连带替换 "{1}%" 吃掉模板的 %（避免 "Atk +30%" 误导为百分比加成）；模板无 % 时普通替换
        return template.Contains("{1}%")
            ? template.Replace("{0}", name).Replace("{1}%", val)
            : template.Replace("{0}", name).Replace("{1}", val);
    }

    /// <summary>
    /// 词条数值格式化（裸数串，单位由模板携带）：百分比键 = 值 ×100 保留一位小数去尾零（0.015 → "1.5"）；
    /// 平面键 = 整数（30 → "30"）。
    /// </summary>
    private static string FormatStatValue(string statType, float value)
    {
        return IsPercentStatKey(statType)
            ? (value * 100f).ToString("0.#")
            : Mathf.RoundToInt(value).ToString();
    }

    /// <summary>
    /// 百分比词条键判定（显示口径）：*_pct 后缀 + 固定比率键集合；atk/hp/def 等平面键不在此列。
    /// </summary>
    private static bool IsPercentStatKey(string statType)
    {
        if (string.IsNullOrEmpty(statType)) return false;
        if (statType.EndsWith("_pct")) return true;
        switch (statType)
        {
            case "crit_rate":
            case "crit_dmg":
            case "crit_empower":
            case "energy_recharge":
            case "dmg_bonus":
            case "heal_bonus":
            case "burst_pierce":
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region 技能页

    /// <summary>
    /// 刷新技能页：SkillDataLibrary 就绪 → 按类型（普攻/E/Q）定位各行，行缓存对齐技能格
    /// （无行的格隐藏，预制体多出的格不露）→ 原选中仍有效则保持，否则默认选中第一个有行的格。
    /// 定位/取值/描述替换统一走 SkillDataLibrary；表未就绪时本次不刷（下次进页/回流重试）。
    /// </summary>
    private async Task RefreshAbilityPageAsync()
    {
        if (view == null || view.abilityPage == null || !model.HasData) return;
        string unitId = model.UnitId;
        Toggle[] toggles = view.abilityPage.skillToggles;
        if (toggles == null || toggles.Length == 0) return;

        await SkillDataLibrary.EnsureLoadedAsync();

        //等待期间已切角则丢弃，防止串写
        if (!model.HasData || model.UnitId != unitId) return;

        //行缓存对齐技能格：类型定位不到行（表缺该类技能）的格隐藏
        if (abilityRows.Length != toggles.Length) abilityRows = new SkillRow[toggles.Length];
        for (int i = 0; i < toggles.Length; i++)
        {
            SkillRow row = i < AbilitySlotTypes.Length
                && SkillDataLibrary.TryGetSkillRow(unitId, AbilitySlotTypes[i], out SkillRow found) ? found : null;
            abilityRows[i] = row;
            if (toggles[i] != null) toggles[i].gameObject.SetActive(row != null);
        }

        //原选中仍有效则保持（升级/突破广播回流不跳格），否则落到第一个有行的格；
        //SetIsOnWithoutNotify 不触发回调，详情手动刷
        int selected = -1;
        if (abilityDetailIndex >= 0 && abilityDetailIndex < abilityRows.Length && abilityRows[abilityDetailIndex] != null)
        {
            selected = abilityDetailIndex;
        }
        for (int i = 0; i < toggles.Length; i++)
        {
            if (abilityRows[i] == null || toggles[i] == null) continue;
            if (selected < 0) selected = i;
            toggles[i].SetIsOnWithoutNotify(i == selected);
        }
        ShowAbilityDetail(selected);

        //技能格图标（Icon 节点自身 Image；普攻全角色共用 NormalAttack，回包竞态由 FillSkillIconAsync 校验）
        for (int i = 0; i < toggles.Length; i++)
        {
            if (abilityRows[i] == null || toggles[i] == null) continue;
            Image slotIcon = toggles[i].GetComponent<Image>();
            if (slotIcon != null) _ = FillSkillIconAsync(slotIcon, abilityRows[i].Icon, unitId, i);
        }
    }

    /// <summary>
    /// 详情区落显（技能格选中回调与页刷新共用）：名 + 等级富文本（色随预制体静态样例）
    /// + 描述占位符替换（FormatDesc，等级夹到 [1, maxLevel] 与取值同口径）；无行/越界清空。
    /// 技能名与描述均为配置表来源显示名（豁免代码写英文规则）。
    /// </summary>
    private void ShowAbilityDetail(int index)
    {
        if (view == null || view.abilityPage == null) return;
        CultivateAbilityPageView page = view.abilityPage;
        abilityDetailIndex = index;

        //升级区跟随选中格刷新（消耗查询异步；切角/切格竞态由刷新方法内部令牌校验丢弃）
        _ = RefreshSkillUpgradeSectionAsync();

        SkillRow row = index >= 0 && index < abilityRows.Length ? abilityRows[index] : null;
        if (row == null)
        {
            if (page.nameText != null) page.nameText.text = string.Empty;
            if (page.descText != null) page.descText.text = string.Empty;
            return;
        }

        int level = Mathf.Clamp(AbilitySlotLevel(row.Type), 1, Mathf.Max(row.MaxLevel, 1));
        if (page.nameText != null) page.nameText.text = $"{row.Name} <color=#FFC900>Lv.{level}</color>";
        if (page.descText != null) page.descText.text = SkillDataLibrary.FormatDesc(row, level) ?? string.Empty;
        if (page.detailIcon != null) _ = FillSkillIconAsync(page.detailIcon, row.Icon, model.UnitId, index, isDetail: true);
    }

    /// <summary>
    /// 技能图标加载落显（SkillRow.icon → Sprite-Character-Skill-Icon 组内地址，带缓存）；
    /// 回包校验角色未切换且该格行未变（切角/切格竞态丢弃），isDetail 的详情图标还要求仍停在原格；
    /// 未登记/加载失败保留默认图，下次重试。
    /// </summary>
    private async Task FillSkillIconAsync(Image target, string address, string unitId, int slotIndex, bool isDetail = false)
    {
        if (target == null || string.IsNullOrEmpty(address)) return;
        if (skillIconCache.TryGetValue(address, out Sprite cached))
        {
            target.sprite = cached;
            return;
        }

        Sprite sprite = await IconLibrary.GetAsync(address, ResourceScope);
        if (sprite == null) return;
        skillIconCache[address] = sprite;

        //回包竞态校验：已切角或该格已换行则丢弃；详情图标另要求选中格未变
        if (target == null || !model.HasData || model.UnitId != unitId
            || slotIndex < 0 || slotIndex >= abilityRows.Length || abilityRows[slotIndex] == null
            || (isDetail && abilityDetailIndex != slotIndex)) return;
        target.sprite = sprite;
    }

    /// <summary>技能类型 → 仓库条目上的等级（普攻/天赋/E/Q；未知类型按普攻）。</summary>
    private int AbilitySlotLevel(string skillType)
    {
        if (model.Entry == null) return 1;
        switch (skillType)
        {
            case SkillRow.SkillTypes.E: return model.Entry.skillLevelE;
            case SkillRow.SkillTypes.Burst: return model.Entry.skillLevelQ;
            case SkillRow.SkillTypes.Talent: return model.Entry.skillLevelTalent;
            default: return model.Entry.skillLevelNormal;
        }
    }

    /// <summary>
    /// 刷新技能升级区（交互与突破同款只读展示）：Left/Breakthrough 滚动区按下一级消耗池化出格
    ///（have/need 染色，点击禁用、Reduce 隐藏）；已满级/无行清列表并置灰升级按钮。
    /// 等待消耗查询期间已切角/切格则丢弃（令牌校验），旧条目入池防重影。
    /// </summary>
    private async Task RefreshSkillUpgradeSectionAsync()
    {
        if (view == null || view.abilityPage == null || !model.HasData) return;
        RectTransform content = view.abilityPage.upgradeContent;
        Button upgradeButton = view.abilityPage.upgradeButton;
        string unitId = model.UnitId;
        int slotIndex = abilityDetailIndex;

        ItemCost[] costs = slotIndex >= 0 && slotIndex < abilityRows.Length && abilityRows[slotIndex] != null
            ? await CharacterProgressionManager.Instance.GetNextSkillLevelCostAsync(unitId, abilityRows[slotIndex].Type)
            : Array.Empty<ItemCost>();

        //等待期间已切角/切格则丢弃（回流重刷兜底）
        if (!model.HasData || model.UnitId != unitId || abilityDetailIndex != slotIndex) return;

        //旧条目入池（ObjectsPool 挂池根并禁用）
        if (content != null)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                if (child.GetComponent<CostItemView>() != null) ObjectsPool.Instance.Put(child);
            }
        }

        bool upgradable = costs.Length > 0;
        if (upgradeButton != null) upgradeButton.interactable = upgradable; // 已满级置灰
        if (content == null || costItemPrefab == null || !upgradable) return;

        foreach (ItemCost cost in costs)
        {
            GameObject go = ObjectsPool.Instance.Get(costItemPrefab, content);
            if (go == null) continue;
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;

            CostItemView item = go.GetComponent<CostItemView>();
            if (item == null) continue;

            item.itemId = cost.ItemId;
            if (item.iconImg != null) _ = FillCostIconAsync(item, cost.ItemId);
            //只读展示：点击禁用、Reduce 隐藏，Has 文本 = 持有/需要
            if (item.iconButton != null) item.iconButton.interactable = false;
            if (item.reduceButton != null) item.reduceButton.gameObject.SetActive(false);
            if (item.hasText != null)
            {
                long have = InventoryManager.Instance.GetCount(cost.ItemId);
                item.hasText.text = $"{have} / {cost.Count}";
                //升级展示与突破同色：持有 ≥ 需要 → 深绿足够，不足 → 深红缺口
                item.hasText.color = have >= cost.Count ? BreakthroughEnoughColor : BreakthroughShortageColor;
            }
        }
    }

    /// <summary>技能升级按钮：当前选中格的类型直接执行升级（材料足够即整体扣耗）；失败弹 Notice，成功经广播回流重刷。</summary>
    private async void OnSkillUpgradeClicked()
    {
        if (!model.HasData || abilityDetailIndex < 0 || abilityDetailIndex >= abilityRows.Length
            || abilityRows[abilityDetailIndex] == null) return;
        try
        {
            await CharacterProgressionManager.Instance.SkillLevelUpAsync(model.UnitId, abilityRows[abilityDetailIndex].Type);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    #endregion

    #region 装备背包

    /// <summary>
    /// 统一捕获词条行与套装名模板（键 = 文本组件，静态串含 {0} 才捕获）：必须在任何落显（含空槽 "-"）之前调用，
    /// 否则空槽覆写会销毁预制体里的模板串。幂等：已捕获的文本跳过。
    /// </summary>
    private void CaptureTrinketStatTemplates(CultivateTrinketPageView page)
    {
        if (page.slots != null)
        {
            foreach (TrinketSlotView slot in page.slots)
            {
                if (slot == null) continue;
                CaptureTrinketTemplate(slot.mainStatText);
                if (slot.subStatTexts != null)
                {
                    foreach (TMP_Text text in slot.subStatTexts) CaptureTrinketTemplate(text);
                }
                CaptureTrinketTemplate(slot.setBonusNameText);
            }
        }

        if (page.bag != null)
        {
            CaptureTrinketTemplate(page.bag.msgMainStatText);
            if (page.bag.msgSubStatTexts != null)
            {
                foreach (TMP_Text text in page.bag.msgSubStatTexts) CaptureTrinketTemplate(text);
            }
        }
    }

    /// <summary>捕获单个文本的模板串（含 {0} 才缓存；已捕获跳过）。</summary>
    private void CaptureTrinketTemplate(TMP_Text text)
    {
        if (text == null || trinketStatTemplates.ContainsKey(text)) return;
        if (text.text.Contains("{0}")) trinketStatTemplates[text] = text.text;
    }

    /// <summary>
    /// 槽位套装名落显：模板 "{0}"（接线脚本烘焙自用户静态串前缀 "套装："）替换为合奏名；
    /// 无合奏/合奏表未就绪写 "-"。合奏名来自 EnsembleTable（配置表来源显示名，豁免英文规则）。
    /// </summary>
    private void WriteTrinketSetName(TMP_Text text, string ensembleId)
    {
        if (text == null) return;
        if (string.IsNullOrEmpty(ensembleId) || ensembleTable == null
            || !ensembleTable.TryGet(ensembleId, out EnsembleRow ensemble))
        {
            text.text = "-";
            return;
        }

        text.text = trinketStatTemplates.TryGetValue(text, out string template) && !string.IsNullOrEmpty(template)
            ? template.Replace("{0}", ensemble.Name)
            : ensemble.Name;
    }

    /// <summary>
    /// 刷新装备背包：懒绑定交互 → 懒构建筛选选项 → 按当前筛选重建条目列表。
    /// 条目 = 持有实例按 itemId 聚合（一行一件 id，Count = 同 id 件数）；账号维度，与选中角色无关。
    /// </summary>
    private async Task RefreshTrinketBagAsync()
    {
        if (view == null || view.trinketPage == null || view.trinketPage.bag == null) return;
        if (trinketBag == null) trinketBag = view.trinketPage.bag;

        EnsureTrinketBagBound();

        //表缺失时按需补载（与装备槽位共用缓存；命中 DataTableManager 缓存则零代价）
        if (trinketTable == null) trinketTable = await DataTableManager.Instance.LoadTableAsync<TrinketRow, string>("TrinketTable");
        if (ensembleTable == null) ensembleTable = await DataTableManager.Instance.LoadTableAsync<EnsembleRow, string>("EnsembleTable");
        if (trinketTable == null) return;

        //套装选项依赖 EnsembleTable，两表齐了才构建（未就绪则下次刷新重试）
        if (!trinketBagOptionsBuilt && ensembleTable != null) BuildTrinketFilterOptions();

        RebuildTrinketBagCells();
    }

    /// <summary>绑定背包交互（懒绑定一次）：初始收起选项列表、筛选按钮开合（互斥展开）、关闭按钮。</summary>
    private void EnsureTrinketBagBound()
    {
        if (trinketBagBound) return;
        trinketBagBound = true;

        if (trinketBag.categoryList != null) trinketBag.categoryList.SetActive(false);
        if (trinketBag.ensembleList != null) trinketBag.ensembleList.SetActive(false);

        //初始整分区与详情卡都收起：背包只从槽位点击进入，详情卡只随悬停出现
        if (trinketBag.msgGo != null) trinketBag.msgGo.SetActive(false);
        trinketBag.gameObject.SetActive(false);

        if (trinketBag.categoryButton != null)
        {
            trinketBag.categoryButton.onClick.AddListener(() =>
            {
                bool opening = trinketBag.categoryList != null && !trinketBag.categoryList.activeSelf;
                if (trinketBag.categoryList != null) trinketBag.categoryList.SetActive(opening);
                if (trinketBag.ensembleList != null) trinketBag.ensembleList.SetActive(false);
            });
        }

        if (trinketBag.ensembleButton != null)
        {
            trinketBag.ensembleButton.onClick.AddListener(() =>
            {
                bool opening = trinketBag.ensembleList != null && !trinketBag.ensembleList.activeSelf;
                if (trinketBag.ensembleList != null) trinketBag.ensembleList.SetActive(opening);
                if (trinketBag.categoryList != null) trinketBag.categoryList.SetActive(false);
            });
        }

        if (trinketBag.closeBtn != null)
        {
            trinketBag.closeBtn.onClick.AddListener(() =>
            {
                if (trinketBag.categoryList != null) trinketBag.categoryList.SetActive(false);
                if (trinketBag.ensembleList != null) trinketBag.ensembleList.SetActive(false);
                if (trinketBag.msgGo != null) trinketBag.msgGo.SetActive(false);
                trinketBag.gameObject.SetActive(false);
            });
        }

        BindTrinketSlotOpeners();
    }

    /// <summary>
    /// 绑定三槽的背包开关（槽内 Button 子节点）：点击打开装备背包换装。
    /// 点击音效走该按钮已配置的持久化接线（InteractionAudioHelper.PlayAudio），运行时不重复接。
    /// </summary>
    private void BindTrinketSlotOpeners()
    {
        CultivateTrinketPageView page = view != null ? view.trinketPage : null;
        if (page == null || page.slots == null) return;
        for (int i = 0; i < page.slots.Length; i++)
        {
            TrinketSlotView slot = page.slots[i];
            if (slot == null || slot.openBagButton == null) continue;
            int slotIndex = i;
            slot.openBagButton.onClick.RemoveAllListeners();
            slot.openBagButton.onClick.AddListener(() => OpenTrinketBag(slotIndex));
        }
    }

    /// <summary>打开装备背包（槽位开关入口）：记录换装目标槽，激活分区并按当前筛选重建条目。</summary>
    private void OpenTrinketBag(int slotIndex)
    {
        trinketSwapSlotIndex = slotIndex;
        if (trinketBag == null && view != null && view.trinketPage != null) trinketBag = view.trinketPage.bag;
        if (trinketBag == null) return;
        trinketBag.gameObject.SetActive(true);
        _ = RefreshTrinketBagAsync();
    }

    /// <summary>
    /// 构建筛选选项（一次）：类别 = TrinketTable 类别列去重（表序），套装 = EnsembleTable 全行（表序）。
    /// 选项行经池化（filterItemPrefab），用户模板实例与旧行同步入池；Toggle 全不选 = 不过滤。
    /// </summary>
    private void BuildTrinketFilterOptions()
    {
        trinketBagOptionsBuilt = true;

        //类别去重（保持表序）
        var categories = new List<string>();
        foreach (TrinketRow row in trinketTable.Rows)
        {
            if (!string.IsNullOrEmpty(row.Category) && !categories.Contains(row.Category)) categories.Add(row.Category);
        }

        PutTrinketFilterRows(trinketBag.categoryOptionContent);
        if (trinketBag.categoryOptionContent != null)
        {
            foreach (string category in categories)
            {
                SpawnTrinketFilterRow(trinketBag.categoryOptionContent, category, false, category);
            }
        }

        PutTrinketFilterRows(trinketBag.ensembleOptionContent);
        if (trinketBag.ensembleOptionContent != null && ensembleTable != null)
        {
            foreach (EnsembleRow ensemble in ensembleTable.Rows)
            {
                SpawnTrinketFilterRow(trinketBag.ensembleOptionContent, ensemble.Id, true, ensemble.Name);
            }
        }
    }

    /// <summary>把内容区里的旧筛选项全部入池（含用户模板实例，须已挂 TrinketFilterItemView）。</summary>
    private static void PutTrinketFilterRows(RectTransform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child.GetComponent<TrinketFilterItemView>() != null) ObjectsPool.Instance.Put(child.gameObject);
        }
    }

    /// <summary>出池一行筛选项并绑定文案与开关行为；开关变化即更新过滤集合并重建背包列表。</summary>
    private void SpawnTrinketFilterRow(RectTransform content, string key, bool isEnsemble, string label)
    {
        GameObject go = ObjectsPool.Instance.Get(filterItemPrefab, content);
        TrinketFilterItemView item = go != null ? go.GetComponent<TrinketFilterItemView>() : null;
        if (item == null) return;

        //挂载后归一变换：与 CostItem 同口径（池的 Instantiate 在无父状态克隆会放大尺度）
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;

        item.filterKey = key;
        item.isEnsemble = isEnsemble;
        if (item.labelText != null) item.labelText.text = label;

        if (item.toggle != null)
        {
            HashSet<string> set = isEnsemble ? trinketEnsembleFilter : trinketCategoryFilter;
            item.toggle.SetIsOnWithoutNotify(set.Contains(key));
            item.toggle.onValueChanged.RemoveAllListeners();
            item.toggle.onValueChanged.AddListener(on =>
            {
                //池化复用：回包以行上当前 key 为准，闭包不可靠
                HashSet<string> target = item.isEnsemble ? trinketEnsembleFilter : trinketCategoryFilter;
                if (on) target.Add(item.filterKey);
                else target.Remove(item.filterKey);
                RebuildTrinketBagCells();
            });
        }
    }

    /// <summary>
    /// 按当前筛选重建背包条目：持有实例按 itemId 聚合 → 类别/套装过滤（空集合 = 不过滤，两组取交集）→ 池化重建。
    /// 行为纯同步（图标异步回包自带 boundItemId 校验），筛选开关连点安全。
    /// </summary>
    private void RebuildTrinketBagCells()
    {
        if (trinketBag == null || trinketBag.content == null || trinketTable == null) return;

        //旧行入池（含用户模板实例）
        for (int i = trinketBag.content.childCount - 1; i >= 0; i--)
        {
            Transform child = trinketBag.content.GetChild(i);
            if (child.GetComponent<TrinketBagItemView>() != null) ObjectsPool.Instance.Put(child.gameObject);
        }

        //聚合：itemId → 件数（保表序：首件出现顺序）
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        IReadOnlyList<TrinketInstance> snapshot = TrinketManager.Instance.Snapshot();
        for (int i = 0; i < snapshot.Count; i++)
        {
            string itemId = snapshot[i].itemId;
            if (counts.TryGetValue(itemId, out int count)) counts[itemId] = count + 1;
            else
            {
                counts[itemId] = 1;
                order.Add(itemId);
            }
        }

        foreach (string itemId in order)
        {
            if (!trinketTable.TryGet(itemId, out TrinketRow row)) continue;

            //筛选：类别并集 + 套装并集（空集合 = 不过滤）
            if (trinketCategoryFilter.Count > 0 && !trinketCategoryFilter.Contains(row.Category)) continue;
            if (trinketEnsembleFilter.Count > 0
                && (string.IsNullOrEmpty(row.EnsembleId) || !trinketEnsembleFilter.Contains(row.EnsembleId))) continue;

            GameObject go = ObjectsPool.Instance.Get(bagItemPrefab, trinketBag.content);
            TrinketBagItemView cell = go != null ? go.GetComponent<TrinketBagItemView>() : null;
            if (cell == null) continue;

            //挂载后归一变换：与 CostItem 同口径（池的 Instantiate 在无父状态克隆会放大尺度）
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;

            cell.boundItemId = itemId;

            //整行点击 → 给打开背包的槽位换装（音效由同行持久化接线承担）
            if (cell.clickButton != null)
            {
                cell.clickButton.onClick.RemoveAllListeners();
                cell.clickButton.onClick.AddListener(() => SwapTrinketAtSlot(cell.boundItemId));
            }

            //悬停 → 详情卡（指针事件从 ClickCatcher 冒泡到本格根；覆写赋值，池化复用免解绑）
            cell.hoverEntered = () => _ = ShowTrinketMsgAsync(cell.boundItemId);
            cell.hoverMoved = AlignTrinketMsgToCursor;
            cell.hoverExited = HideTrinketMsg;

            if (cell.setBadgeGo != null) cell.setBadgeGo.SetActive(!string.IsNullOrEmpty(row.EnsembleId));
            if (cell.iconImg != null) _ = FillTrinketBagIconAsync(cell, itemId, row.Icon);
            WriteTrinketBagCount(cell, counts[itemId]);
        }
    }

    /// <summary>异步填充背包条目图标（TrinketRow.icon 单图按需）；回包时格子已改绑其他 id 则丢弃。</summary>
    private async Task FillTrinketBagIconAsync(TrinketBagItemView cell, string itemId, string iconAddress)
    {
        if (string.IsNullOrEmpty(iconAddress)) return;
        Sprite sprite = await IconLibrary.GetAsync(iconAddress, ResourceScope);
        if (sprite == null || cell == null || cell.boundItemId != itemId) return;
        cell.iconImg.sprite = sprite;
    }

    /// <summary>
    /// 计数落显（Count = 同 id 持有件数）：数位精灵由 numberSheet 底图切片（D_Font 0-9，见 TrinketBagItemView），
    /// 底图未配置时隐藏计数节点；十位仅数量 >=10 显示，超两位钳 99。
    /// </summary>
    private static void WriteTrinketBagCount(TrinketBagItemView cell, int count)
    {
        if (cell.numberSheet == null || cell.unitsDigit == null)
        {
            if (cell.countGo != null) cell.countGo.SetActive(false);
            return;
        }

        if (cell.countGo != null) cell.countGo.SetActive(true);
        count = Mathf.Clamp(count, 0, 99);
        bool showTens = count >= 10;
        if (cell.tensDigit != null)
        {
            cell.tensDigit.gameObject.SetActive(showTens);
            if (showTens) cell.tensDigit.sprite = cell.GetDigitSprite(count / 10);
        }
        cell.unitsDigit.sprite = cell.GetDigitSprite(count % 10);
    }

    /// <summary>打开详情卡并填充该装备 id 的图标/套装名/词条（同 id 任意件词条相同，表配死）；异步图标回包带 id 校验。</summary>
    private async Task ShowTrinketMsgAsync(string itemId)
    {
        if (trinketBag == null || trinketTable == null || !trinketTable.TryGet(itemId, out TrinketRow row)) return;

        trinketMsgBoundItemId = itemId;
        if (trinketBag.msgGo != null)
        {
            trinketBag.msgGo.SetActive(true);
            AlignTrinketMsgToCursor(Input.mousePosition); //弹出即对齐鼠标，主体落在鼠标右侧
        }

        if (trinketBag.msgSetNameText != null)
        {
            trinketBag.msgSetNameText.text = !string.IsNullOrEmpty(row.EnsembleId)
                && ensembleTable != null
                && ensembleTable.TryGet(row.EnsembleId, out EnsembleRow ensemble)
                    ? ensemble.Name
                    : "-";
        }

        if (trinketBag.msgMainStatText != null)
        {
            trinketBag.msgMainStatText.text = FormatStatLine(trinketBag.msgMainStatText, row.MainStatType, row.MainStatValue);
        }
        WriteSubStat(trinketBag.msgSubStatTexts, 0, row.SubStat1Type, row.SubStat1Value);
        WriteSubStat(trinketBag.msgSubStatTexts, 1, row.SubStat2Type, row.SubStat2Value);
        WriteSubStat(trinketBag.msgSubStatTexts, 2, row.SubStat3Type, row.SubStat3Value);

        if (trinketBag.msgIcon != null && !string.IsNullOrEmpty(row.Icon))
        {
            Sprite sprite = await IconLibrary.GetAsync(row.Icon, ResourceScope);
            if (sprite == null || trinketBag == null || trinketMsgBoundItemId != itemId) return;
            trinketBag.msgIcon.sprite = sprite;
        }
    }

    /// <summary>
    /// 详情卡对齐鼠标：把 Msg/CurosrPoint 对齐标记落到鼠标所在位置（用户预制体已摆位，标记在卡体左侧），
    /// 卡主体呈现在鼠标右侧不挡指针。悬停弹出与条目内移动都会调用。
    /// </summary>
    private void AlignTrinketMsgToCursor(Vector2 screenPos)
    {
        if (trinketBag == null || trinketBag.msgGo == null || trinketBag.msgCursorPoint == null) return;
        RectTransform msgRect = (RectTransform)trinketBag.msgGo.transform;
        RectTransform parent = msgRect.parent as RectTransform;
        if (parent == null) return;

        Canvas canvas = trinketBag.msgCursorPoint.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, cam, out Vector2 local)) return;

        //CurosrPoint 相对 Msg 的局部偏移 × Msg 缩放 = 它在父局部系相对 Msg 原点的偏移
        Vector3 offset = Vector3.Scale(trinketBag.msgCursorPoint.localPosition, msgRect.localScale);
        Vector3 pos = msgRect.localPosition;
        pos.x = local.x - offset.x;
        pos.y = local.y - offset.y;
        msgRect.localPosition = pos;
    }

    /// <summary>收起详情卡（悬停离开背包条目时）。</summary>
    private void HideTrinketMsg()
    {
        trinketMsgBoundItemId = null;
        if (trinketBag != null && trinketBag.msgGo != null) trinketBag.msgGo.SetActive(false);
    }

    /// <summary>
    /// 背包条目点击 = 给打开背包的槽位换装（trinketSwapSlotIndex）：新件穿上、该槽旧件脱下回背包，
    /// 永不触发"满/已穿"拒绝——无空位由换下动作腾出；同 id 全在身上时点击不动作（背包行计数含在穿件）。
    /// 新件列表序排在末尾（实例无持久槽位，三自由格属性等价，格序以界面为准）。
    /// </summary>
    private void SwapTrinketAtSlot(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || model == null) return;
        IReadOnlyList<TrinketInstance> worn = TrinketManager.Instance.GetWornBy(model.UnitId);
        int slotIndex = Mathf.Clamp(trinketSwapSlotIndex, 0, 2);
        //同 id 无未穿实例 = 该款全部在穿（背包行计数含在穿件），点击不动作
        IReadOnlyList<TrinketInstance> snapshot = TrinketManager.Instance.Snapshot();
        TrinketInstance target = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i].itemId == itemId && string.IsNullOrEmpty(snapshot[i].wearerId))
            {
                target = snapshot[i];
                break;
            }
        }
        if (target == null) return;
        //换下该槽旧件（腾位）再穿新件——顺序保证 Equip 不撞"已满/已穿"拒绝
        if (slotIndex < worn.Count) TrinketManager.Instance.Unequip(worn[slotIndex].instanceId);
        TrinketManager.Instance.Equip(target.instanceId, model.UnitId);
    }

    #endregion

    #region 技能书喂养（暂存 + 确认结算）

    /// <summary>绑定一页的喂养按钮：Fill/LevelUp/Breakthrough（每页一次，EventInit 调用；书格点击在生成时绑定）。</summary>
    private void BindFeedSection(Button fill, Button levelUp, Button breakthrough, bool isStatPage)
    {
        if (fill != null) fill.onClick.AddListener(() => OnFillClicked(isStatPage));
        if (levelUp != null) levelUp.onClick.AddListener(() => OnLevelUpClicked(isStatPage));
        if (breakthrough != null) breakthrough.onClick.AddListener(OnBreakthroughClicked);
    }

    /// <summary>
    /// 书格动态生成（属性/武器页各一组，固定数格不入池）：缓存缺失或格数与 ids 不符时按 ids 数量
    /// 从 costItemPrefab Instantiate 进 bookContent（GridLayout 自动排列），生成即绑 Icon/Reduce 点击。
    /// 容器或预制体引用未接时返回 null（喂养区静默跳过）；重建仅发生在首次访问/书数变化，此时内容区只会有书格。
    /// </summary>
    private CostItemView[] EnsureBookCells(bool isStatPage)
    {
        RectTransform content = isStatPage
            ? (view.statPage != null ? view.statPage.bookContent : null)
            : (view.weaponPage != null ? view.weaponPage.bookContent : null);
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        CostItemView[] cached = isStatPage ? statBookCells : weaponBookCells;
        if (content == null || costItemPrefab == null) return null;
        if (cached != null && cached.Length == ids.Length) return cached;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        CostItemView[] cells = new CostItemView[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            CostItemView cell = Instantiate(costItemPrefab, content).GetComponent<CostItemView>();
            cells[i] = cell;
            if (cell == null) continue;
            int index = i;
            if (cell.iconButton != null) cell.iconButton.onClick.AddListener(() => OnBookIconClicked(isStatPage, index));
            if (cell.reduceButton != null) cell.reduceButton.onClick.AddListener(() => OnBookReduceClicked(isStatPage, index));
        }
        if (isStatPage) statBookCells = cells; else weaponBookCells = cells;
        return cells;
    }

    /// <summary>
    /// 刷新一页喂养区：书格绑定（物品 id + 图标）+ 暂存态落显（Has 文本/Reduce 可用态/LevelUp 预览）
    /// + 突破材料只读列表。角色页与武器页共用口径（武器页的喂养对象是武器等级/经验）。
    /// </summary>
    private async Task RefreshFeedSectionAsync(bool isStatPage)
    {
        if (view == null || !model.HasData) return;
        CostItemView[] cells = EnsureBookCells(isStatPage);
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        if (cells == null) return;

        //暂存数组对齐书格数（Inspector 调整书 id 数组后自动扩）
        long[] staged = isStatPage ? statStaged : weaponStaged;
        if (staged.Length != ids.Length)
        {
            staged = new long[ids.Length];
            if (isStatPage) statStaged = staged; else weaponStaged = staged;
        }

        for (int i = 0; i < cells.Length && i < ids.Length; i++)
        {
            CostItemView cell = cells[i];
            if (cell == null) continue;
            cell.itemId = ids[i];
            if (cell.iconImg != null) _ = FillCostIconAsync(cell, ids[i]);
            if (cell.iconButton != null) cell.iconButton.interactable = true; // 只读池化格共用同预制体，喂养格恒可点
        }

        RefreshStagedState(isStatPage);

        //突破列表与升级/突破互斥显隐共用一次消耗查询（回包已切角则丢弃）
        string unitId = model.UnitId;
        ItemCost[] breakthroughCosts = await CharacterProgressionManager.Instance.GetNextBreakthroughCostsAsync(unitId);
        if (!model.HasData || model.UnitId != unitId) return;
        UpdateSectionVisibility(isStatPage, breakthroughCosts);
        RefreshBreakthroughList(isStatPage, breakthroughCosts);
    }

    /// <summary>
    /// 升级/突破互斥显隐（升级和突破只显示一个）：等级达当前上限且还有下一突破阶段时切到突破态
    ///（升级区与 Fill/LevelUp 按钮隐藏、突破区与 Breakthrough 按钮显示），否则维持升级态；
    /// 已满破时（无下一阶段）回到升级态并置灰喂养按钮（等级无法再升，升级区仅供查看）。
    /// </summary>
    private void UpdateSectionVisibility(bool isStatPage, ItemCost[] breakthroughCosts)
    {
        if (view == null || !model.HasData) return;
        RectTransform levelUpSection = isStatPage
            ? (view.statPage != null ? view.statPage.levelUpSection : null)
            : (view.weaponPage != null ? view.weaponPage.levelUpSection : null);
        RectTransform breakthroughSection = isStatPage
            ? (view.statPage != null ? view.statPage.breakthroughSection : null)
            : (view.weaponPage != null ? view.weaponPage.breakthroughSection : null);
        Button fill = isStatPage
            ? (view.statPage != null ? view.statPage.fillButton : null)
            : (view.weaponPage != null ? view.weaponPage.fillButton : null);
        Button levelUp = isStatPage
            ? (view.statPage != null ? view.statPage.levelUpButton : null)
            : (view.weaponPage != null ? view.weaponPage.levelUpButton : null);
        Button breakthrough = isStatPage
            ? (view.statPage != null ? view.statPage.breakthroughButton : null)
            : (view.weaponPage != null ? view.weaponPage.breakthroughButton : null);
        if (levelUpSection == null && breakthroughSection == null
            && fill == null && levelUp == null && breakthrough == null) return;

        //突破看角色等级（两页同执行角色突破）；喂养上限看本页升级对象（属性页角色等级 / 武器页武器等级）
        bool readyToBreak = breakthroughCosts != null && breakthroughCosts.Length > 0
            && model.Entry.level >= model.LevelCap;
        int feedLevel = isStatPage ? model.Entry.level : Math.Max(1, model.Entry.weaponLevel);
        bool atFeedCap = feedLevel >= model.LevelCap;

        if (levelUpSection != null) levelUpSection.gameObject.SetActive(!readyToBreak);
        if (breakthroughSection != null) breakthroughSection.gameObject.SetActive(readyToBreak);
        if (fill != null) fill.gameObject.SetActive(!readyToBreak);
        if (levelUp != null) levelUp.gameObject.SetActive(!readyToBreak);
        if (breakthrough != null) breakthrough.gameObject.SetActive(readyToBreak);
        if (fill != null) fill.interactable = !atFeedCap;
        if (levelUp != null) levelUp.interactable = !atFeedCap;
    }

    /// <summary>
    /// 暂存态落显：每格 Has 文本（{0}/{1} = 使用/持有）、Reduce 可用态（使用量 &gt; 0）、
    /// LevelUp 按钮文案（空暂存还原模板；有暂存显示 "Lv a-b" 结算预览，与 SettleExp 同口径）。
    /// 外部变动（结算消耗/获得道具）可能吃掉已暂存量，先钳回持有内再落显。
    /// </summary>
    private void RefreshStagedState(bool isStatPage)
    {
        if (view == null || !model.HasData) return;
        CostItemView[] cells = EnsureBookCells(isStatPage);
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        long[] staged = isStatPage ? statStaged : weaponStaged;
        if (cells == null) return;

        for (int i = 0; i < cells.Length && i < ids.Length && i < staged.Length; i++)
        {
            CostItemView cell = cells[i];
            if (cell == null) continue;
            long held = InventoryManager.Instance.GetCount(ids[i]);
            if (staged[i] > held) staged[i] = held;
            if (cell.hasText != null) SetHasText(isStatPage, cell.hasText, staged[i], held);
            //Reduce 只在有暂存量时显示（0 时隐藏），与"Icon +1 / Reduce -1"的喂养语义配套
            if (cell.reduceButton != null) cell.reduceButton.gameObject.SetActive(staged[i] > 0);
        }

        int targetLevel = RefreshLevelUpPreview(isStatPage);
        RefreshUpgradeCostCells(isStatPage, targetLevel);
    }

    /// <summary>
    /// 书格 Icon 点击：暂存 +1（钳制 ≤ 持有量），刷新该页暂存态。
    /// 已达喂养上限不暂存（升不了级）；暂存经验已够推到上限时也不暂存（再点的书全会被溢出丢弃）。
    /// </summary>
    private void OnBookIconClicked(bool isStatPage, int index)
    {
        if (!model.HasData) return;
        int feedLevel = isStatPage ? model.Entry.level : Math.Max(1, model.Entry.weaponLevel);
        if (feedLevel >= model.LevelCap) return;
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        long[] staged = isStatPage ? statStaged : weaponStaged;
        if (index < 0 || index >= ids.Length || index >= staged.Length) return;
        //暂存经验已够推到上限：再点的书全会被溢出丢弃，直接无效（SettleExp 同口径投影）
        int feedExp = isStatPage ? model.Entry.exp : model.Entry.weaponExp;
        CharacterProgressionManager.SettleExp(feedLevel, feedExp, StagedExp(isStatPage), model.LevelCap, out int projected, out _, out _);
        if (projected >= model.LevelCap) return;
        staged[index] = Math.Min(staged[index] + 1, InventoryManager.Instance.GetCount(ids[index]));
        RefreshStagedState(isStatPage);
    }

    /// <summary>书格 Reduce 点击：暂存 -1（下限 0），刷新该页暂存态。</summary>
    private void OnBookReduceClicked(bool isStatPage, int index)
    {
        if (!model.HasData) return;
        long[] staged = isStatPage ? statStaged : weaponStaged;
        if (index < 0 || index >= staged.Length) return;
        staged[index] = Math.Max(0, staged[index] - 1);
        RefreshStagedState(isStatPage);
    }

    /// <summary>
    /// Fill 点击：按服务层填充计划整批预暂存（大书整填小书补零头，刚好够到可达最高级；
    /// 书本经验与逐级消耗任一不足即止）；不可填充（已达上限或一级都升不动）时暂存清零。
    /// </summary>
    private async void OnFillClicked(bool isStatPage)
    {
        if (!model.HasData) return;
        string unitId = model.UnitId;
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        try
        {
            FillPlan plan = isStatPage
                ? await CharacterProgressionManager.Instance.GetFillPlanAsync(unitId, ids)
                : await CharacterProgressionManager.Instance.GetWeaponFillPlanAsync(unitId, ids);
            if (!model.HasData || model.UnitId != unitId) return; // 等待期间已切角

            long[] staged = isStatPage ? statStaged : weaponStaged;
            if (staged.Length != ids.Length)
            {
                staged = new long[ids.Length];
                if (isStatPage) statStaged = staged; else weaponStaged = staged;
            }
            for (int i = 0; i < staged.Length; i++)
            {
                staged[i] = plan != null && plan.Counts != null && i < plan.Counts.Length ? plan.Counts[i] : 0;
            }
            RefreshStagedState(isStatPage);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// LevelUp 点击：暂存的技能书整批确认结算（服务层先验后扣，失败弹 Notice 且不扣耗，暂存保留供调整）；
    /// 成功清空暂存，页面数据经仓库广播回流重刷。
    /// </summary>
    private async void OnLevelUpClicked(bool isStatPage)
    {
        if (!model.HasData) return;
        string unitId = model.UnitId;
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        long[] staged = isStatPage ? statStaged : weaponStaged;

        List<ItemStack> books = new List<ItemStack>();
        for (int i = 0; i < ids.Length && i < staged.Length; i++)
        {
            if (staged[i] > 0) books.Add(new ItemStack { itemId = ids[i], count = staged[i] });
        }
        if (books.Count == 0) return; // 空暂存：无操作

        try
        {
            LevelUpResult result = isStatPage
                ? await CharacterProgressionManager.Instance.UseExpItemsAsync(unitId, books)
                : await CharacterProgressionManager.Instance.UseWeaponExpItemsAsync(unitId, books);
            if (!result.Success) return;

            //结算成功：清空暂存（消耗后的广播回流重刷页面数据）
            for (int i = 0; i < staged.Length; i++) staged[i] = 0;
            RefreshStagedState(isStatPage);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>Breakthrough 点击：请求角色突破（等级达上限且材料足够才成功）；失败弹 Notice，成功经广播回流重刷。</summary>
    private async void OnBreakthroughClicked()
    {
        if (!model.HasData) return;
        try
        {
            await CharacterProgressionManager.Instance.BreakthroughAsync(model.UnitId);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// 刷新突破材料只读列表：下一阶段突破消耗（need）vs 持有（have），CostItem 池化出池入池，
    /// 条目点击禁用、Reduce 隐藏（纯展示）；已满破清空列表。
    /// </summary>
    private void RefreshBreakthroughList(bool isStatPage, ItemCost[] costs)
    {
        if (view == null || !model.HasData) return;
        RectTransform content = isStatPage
            ? (view.statPage != null ? view.statPage.breakthroughContent : null)
            : (view.weaponPage != null ? view.weaponPage.breakthroughContent : null);
        if (content == null) return;

        //旧条目入池（ObjectsPool 挂池根并禁用）
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            if (child.GetComponent<CostItemView>() != null) ObjectsPool.Instance.Put(child);
        }

        if (costItemPrefab == null || costs == null || costs.Length == 0 || itemTable == null) return;

        foreach (ItemCost cost in costs)
        {
            GameObject go = ObjectsPool.Instance.Get(costItemPrefab, content);
            if (go == null) continue;

            //挂载后归一变换：与左列单元格同口径（池的 Instantiate 在无父状态克隆会放大尺度）
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;

            CostItemView item = go.GetComponent<CostItemView>();
            if (item == null) continue; // 预制体漏挂视图时跳过（Setup 脚本负责挂）

            item.itemId = cost.ItemId;
            if (item.iconImg != null) _ = FillCostIconAsync(item, cost.ItemId);
            //只读展示：点击禁用、Reduce 隐藏，Has 文本 = 持有/需要
            if (item.iconButton != null) item.iconButton.interactable = false;
            if (item.reduceButton != null) item.reduceButton.gameObject.SetActive(false);
            if (item.hasText != null)
            {
                long have = InventoryManager.Instance.GetCount(cost.ItemId);
                item.hasText.text = $"{have} / {cost.Count}";
                //突破展示：持有 ≥ 需要 → 深绿足够，不足 → 深红缺口
                item.hasText.color = have >= cost.Count ? BreakthroughEnoughColor : BreakthroughShortageColor;
            }
        }
    }

    /// <summary>固定书格判定：升级消耗格清理时跳过（书格是动态生成的固定实例，绝不能入池）。</summary>
    private static bool IsBookCell(CostItemView[] cells, GameObject go)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null && cells[i].gameObject == go) return true;
        }
        return false;
    }

    /// <summary>
    /// 升级消耗格（升级 Content 下、四本技能书之后，池化）：跨级才有的信用点等非书本消耗只读展示
    /// （持有/需要，点击禁用、Reduce 隐藏）；未跨级清空。消耗随暂存预览实时重算（与结算同口径）。
    /// </summary>
    private void RefreshUpgradeCostCells(bool isStatPage, int targetLevel)
    {
        if (view == null || !model.HasData || targetLevel < 0) return;
        CostItemView[] cells = isStatPage ? statBookCells : weaponBookCells;
        if (cells == null || cells.Length == 0 || cells[0] == null) return;
        RectTransform content = cells[0].transform.parent as RectTransform;
        if (content == null) return;

        int oldLevel = isStatPage ? model.Entry.level : Math.Max(1, model.Entry.weaponLevel);
        ItemCost[] costs = targetLevel > oldLevel
            ? CharacterProgressionManager.Instance.GetUpgradeCost(oldLevel, targetLevel)
            : Array.Empty<ItemCost>();

        //旧条目入池（固定书格跳过，绝不能入池）
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            if (IsBookCell(cells, child)) continue;
            if (child.GetComponent<CostItemView>() != null) ObjectsPool.Instance.Put(child);
        }
        if (costItemPrefab == null || costs.Length == 0) return;

        foreach (ItemCost cost in costs)
        {
            GameObject go = ObjectsPool.Instance.Get(costItemPrefab, content);
            if (go == null) continue;
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;

            CostItemView item = go.GetComponent<CostItemView>();
            if (item == null) continue;

            item.itemId = cost.ItemId;
            if (item.iconImg != null) _ = FillCostIconAsync(item, cost.ItemId);
            if (item.iconButton != null) item.iconButton.interactable = false;
            if (item.reduceButton != null) item.reduceButton.gameObject.SetActive(false);
            if (item.hasText != null)
            {
                item.hasText.text = $"{InventoryManager.Instance.GetCount(cost.ItemId)} / {cost.Count}";
                item.hasText.color = Color.white; //池化格可能复用自突破列表（带红/绿染色），重置回默认白
            }
        }
    }

    /// <summary>Has 文本落显：模板含 {0}/{1} 时替换为使用/持有（只动占位符），否则直写 "使用 / 持有"。</summary>
    private void SetHasText(bool isStatPage, TMP_Text text, long used, long held)
    {
        string template = isStatPage ? statHasTemplate : weaponHasTemplate;
        if (string.IsNullOrEmpty(template) || !template.Contains("{0}"))
        {
            //首次捕获：预制体静态串应含 {0}/{1} 占位（Setup 脚本负责烘焙）；不含则每次直写
            if (text.text.Contains("{0}"))
            {
                template = text.text;
                if (isStatPage) statHasTemplate = template; else weaponHasTemplate = template;
            }
            else
            {
                text.text = $"{used} / {held}";
                return;
            }
        }
        text.text = template.Replace("{0}", used.ToString()).Replace("{1}", held.ToString());
    }

    /// <summary>
    /// 升级预览落显（SettleExp 同口径）：
    /// 满级时显示 "Lv.{当前} - MAX"，并隐藏整根经验进度条；
    /// 非满级时显示等级文本 "Lv.当前 > Lv.目标"（无暂存显示 "当前 > -"），并驱动经验进度条预览。
    /// LevelUp 按钮文本不在此写（保持预制体静态文案）。返回预览目标级（空暂存 = 当前级；无数据 = -1），供升级消耗格刷新。
    /// </summary>
    private int RefreshLevelUpPreview(bool isStatPage)
    {
        if (!model.HasData) return -1;

        int oldLevel = isStatPage ? model.Entry.level : Math.Max(1, model.Entry.weaponLevel);
        int oldExp = isStatPage ? model.Entry.exp : model.Entry.weaponExp;
        int cap = model.LevelCap;
        bool isMax = oldLevel >= cap;

        long[] staged = isStatPage ? statStaged : weaponStaged;
        long stagedCount = 0;
        for (int i = 0; i < staged.Length; i++) stagedCount += staged[i];

        int target = oldLevel;
        int projectedExp = oldExp;
        if (!isMax && stagedCount > 0)
        {
            CharacterProgressionManager.SettleExp(oldLevel, oldExp, StagedExp(isStatPage), cap, out target, out projectedExp, out _);
        }

        // 等级文本（StatBase 的 LvFromTo / 武器页 Level）：满级显示 "Lv.{当前} - MAX"，非满级显示 "当前 > 目标"
        TMP_Text levelLabel = isStatPage
            ? (view.statPage != null ? view.statPage.levelText : null)
            : (view.weaponPage != null ? view.weaponPage.levelText : null);
        if (levelLabel != null)
        {
            levelLabel.text = isMax ? $"Lv.{oldLevel} - MAX" : (stagedCount > 0 ? $"Lv.{oldLevel} > Lv.{target}" : $"Lv.{oldLevel} > -");
        }

        // 经验进度条预览：满级时直接隐藏，非满级时显示并驱动比例
        RectTransform previewFill = isStatPage
            ? (view.statPage != null ? view.statPage.expFillRect : null)
            : (view.weaponPage != null ? view.weaponPage.progressFillRect : null);
        if (previewFill != null)
        {
            if (isMax)
            {
                SetExpProgressBarActive(previewFill, false);
            }
            else
            {
                SetExpProgressBarActive(previewFill, true);
                float fraction;
                if (stagedCount > 0)
                {
                    int need = CharacterProgressionManager.ExpToNext(target);
                    fraction = target >= cap ? 1f : need > 0 ? Mathf.Clamp01((float)projectedExp / need) : 0f;
                }
                else
                {
                    int need = CharacterProgressionManager.ExpToNext(oldLevel);
                    fraction = need > 0 ? Mathf.Clamp01((float)oldExp / need) : 0f;
                }
                Vector2 anchorMax = previewFill.anchorMax;
                anchorMax.x = fraction;
                previewFill.anchorMax = anchorMax;
            }
        }

        return target;
    }

    /// <summary>
    /// 控制经验进度条的显隐。满级时隐藏整根进度条（包含 Progress 容器及其背景与填充图）。
    /// </summary>
    /// <param name="fill">进度条填充矩形组件（Fill）。</param>
    /// <param name="active">是否显示。</param>
    private static void SetExpProgressBarActive(RectTransform fill, bool active)
    {
        if (fill == null) return;
        fill.gameObject.SetActive(active);
        // 若存在父级 Progress 容器（结构为 Progress/BG/Fill），同步控制其整体显隐
        if (fill.parent != null && fill.parent.parent != null && fill.parent.parent.name == "Progress")
        {
            fill.parent.parent.gameObject.SetActive(active);
        }
        else if (fill.parent != null && fill.parent.name == "Progress")
        {
            fill.parent.gameObject.SetActive(active);
        }
    }

    /// <summary>当前暂存量折合经验（staged 下标对齐书 id；书行缺失或非经验书按 0）。</summary>
    private long StagedExp(bool isStatPage)
    {
        if (itemTable == null) return 0;
        string[] ids = isStatPage ? statBookItemIds : weaponBookItemIds;
        long[] staged = isStatPage ? statStaged : weaponStaged;
        long total = 0;
        for (int i = 0; i < ids.Length && i < staged.Length; i++)
        {
            if (staged[i] > 0 && itemTable.TryGet(ids[i], out ItemRow row)) total += (long)row.Exp * staged[i];
        }
        return total;
    }

    /// <summary>异步填充条目图标（ItemRow.icon 按需加载并缓存）；回包时条目已改绑其他物品（池化复用）则丢弃。</summary>
    private async Task FillCostIconAsync(CostItemView item, string itemId)
    {
        if (itemTable == null) return;
        if (costIconCache.TryGetValue(itemId, out Sprite cached))
        {
            if (item != null && item.itemId == itemId && item.iconImg != null) item.iconImg.sprite = cached;
            return;
        }

        // 无图标配置或未登记 Addressables：保留默认图，下次重试
        if (!itemTable.TryGet(itemId, out ItemRow row) || string.IsNullOrEmpty(row.Icon)) return;
        Sprite sprite = await IconLibrary.GetAsync(row.Icon, ResourceScope);
        if (sprite == null) return;
        costIconCache[itemId] = sprite;

        if (item == null || item.itemId != itemId || item.iconImg == null) return;
        item.iconImg.sprite = sprite;
    }

    #endregion

    #region 展示刷新

    /// <summary>仓库获得角色 → 列表增删跟随。</summary>
    private void OnCharacterAcquired()
    {
        RefreshBar();
    }

    /// <summary>仓库养成数据变化 → 当前展示与列表选中态回流刷新。</summary>
    private void OnCharacterDataUpdated()
    {
        if (model.HasData) _ = SelectCharacterAsync(model.UnitId);
    }

    /// <summary>背包变化 → 当前页消耗展示跟随：属性/武器页刷喂养区（含突破列表），技能页刷升级区。</summary>
    private void OnBagUpdated()
    {
        if (!model.HasData) return;
        switch (currentPageIndex)
        {
            case 0: _ = RefreshFeedSectionAsync(true); break;
            case 1: _ = RefreshFeedSectionAsync(false); break;
            case 3: _ = RefreshSkillUpgradeSectionAsync(); break;
        }
    }

    /// <summary>
    /// 刷新属性页：名字/职业/等级/经验进度条/星级（=稀有度显隐）/七格属性面板。
    /// </summary>
    private void UpdateStatPage()
    {
        if (view == null || view.statPage == null || !model.HasData) return;
        CultivateStatPageView page = view.statPage;

        if (page.chNameText != null) page.chNameText.text =
            !string.IsNullOrEmpty(model.UnitName) ? model.UnitName : model.Row.Name;
        if (page.archetypeText != null) page.archetypeText.text =
            !string.IsNullOrEmpty(model.Row.Archetype) ? model.Row.Archetype : model.Row.Profession;

        // 异步更新 Archetype 主要定位图标（Tag 图集中的 Tag_Small_* 系列）
        _ = UpdateArchetypeIconAsync(page.archetypeIcon, model.Row.Archetype, model.Row.Id);

        //角色标签：池化复用（CHTag），图标走 Tag_<名> 通道，方括号属性首键作心情 emoji
        _ = RefreshTagsAsync(page.tagContent, model.Row.TagList, model.Row.Id);

        // 等级文本与经验进度条：满级时显示 "Lv.{当前} - MAX" 并隐藏整根进度条；非满级按当前比例驱动并交由 RefreshLevelUpPreview 处理升级预览
        int charLevel = model.Entry.level;
        bool isCharacterMax = charLevel >= model.LevelCap;
        if (page.levelText != null && isCharacterMax)
        {
            page.levelText.text = $"Lv.{charLevel} - MAX";
        }
        if (page.expFillRect != null)
        {
            if (isCharacterMax)
            {
                SetExpProgressBarActive(page.expFillRect, false);
            }
            else
            {
                SetExpProgressBarActive(page.expFillRect, true);
                Vector2 anchorMax = page.expFillRect.anchorMax;
                anchorMax.x = model.ExpProgress;
                page.expFillRect.anchorMax = anchorMax;
            }
        }

        //星级 = 稀有度：素材未配前仅显隐占位星（与 HomePanel 名片同口径）
        if (page.stars != null)
        {
            for (int i = 0; i < page.stars.Length; i++)
            {
                if (page.stars[i] != null) page.stars[i].gameObject.SetActive(i < model.Row.Rarity);
            }
        }

        //属性面板：与预制体七格顺序对齐（生命/攻击/防御/充能/暴击率/暴击伤害/伤害加成）
        if (page.statNumbers != null && page.statNumbers.Length >= 7)
        {
            CharacterStats s = model.Stats;
            SetStatText(page.statNumbers[0], s.MaxHp.ToString());
            SetStatText(page.statNumbers[1], s.Atk.ToString());
            SetStatText(page.statNumbers[2], s.Def.ToString());
            SetStatText(page.statNumbers[3], FormatPercent(s.EnergyRecharge));
            SetStatText(page.statNumbers[4], FormatPercent(s.CritRate));
            SetStatText(page.statNumbers[5], FormatPercent(s.CritDmg));
            SetStatText(page.statNumbers[6], FormatPercent(s.DamageBonus));
        }
    }

    /// <summary>属性格写值：引用缺失时跳过。</summary>
    private static void SetStatText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    /// <summary>
    /// 角色标签刷新（ObjectsPool 池化复用）：tagImage = 标签图（图集切片 Tag_&lt;标签名&gt;），
    /// 方括号附带属性首键（如 Feeling_0）作心情 emoji sprite 名（同图集切片），图缺失隐藏对应 Image；
    /// 图集先一次常驻载入，之后 GetSprite 纯同步查名；切角竞态按 unitId 失效，await 回来后若已切角
    /// 直接弃写（新刷新已接管容器，绝不二次入池）。
    /// </summary>
    private async Task RefreshTagsAsync(RectTransform content, CharacterTagDecl[] tags, string unitId)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) ObjectsPool.Instance.Put(content.GetChild(i).gameObject);
        if (chTagPrefab == null || tags == null || tags.Length == 0) return;

        SpriteAtlas atlas = await EnsureTagAtlasAsync();
        if (!model.HasData || model.Row.Id != unitId) return;
        if (atlas == null) return; //图集载入失败（已记日志），格子已清空即本帧表现

        foreach (CharacterTagDecl tag in tags)
        {
            GameObject go = ObjectsPool.Instance.Get(chTagPrefab, content);
            if (go == null) continue;
            //挂载后归一变换：与消耗格同口径（池的 Instantiate 在无父状态克隆会放大尺度）
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            CHTagView cell = go.GetComponent<CHTagView>();
            if (cell == null) continue; // 预制体漏挂视图时跳过

            if (cell.tagImage != null)
            {
                Sprite tagSprite = atlas.GetSprite($"Tag_{tag.Name}");
                cell.tagImage.sprite = tagSprite;
                cell.tagImage.enabled = tagSprite != null;
            }

            if (cell.emojiImage != null)
            {
                Sprite emoji = tag.Extras != null && tag.Extras.Length > 0 ? atlas.GetSprite(tag.Extras[0]) : null;
                cell.emojiImage.sprite = emoji;
                cell.emojiImage.enabled = emoji != null;
            }
        }
    }

    /// <summary>
    /// 异步更新属性页的角色主要位置类型（Archetype）图标。
    /// 从 Tag 图集载入对应的 Tag_Small_* 精灵切片并赋给图标组件。
    /// </summary>
    /// <param name="icon">展示定位类型的 Image 组件。</param>
    /// <param name="archetype">角色主要定位字符串（如 Support / Defence / DPS / Heal）。</param>
    /// <param name="unitId">请求发起时的单位 ID，用于异步回包时防串写。</param>
    /// <returns>异步任务。</returns>
    private async Task UpdateArchetypeIconAsync(Image icon, string archetype, string unitId)
    {
        // 校验组件有效性
        if (icon == null) return;

        // 获取 Tag 图集
        SpriteAtlas atlas = await EnsureTagAtlasAsync();

        // 校验面板数据与角色一致性
        if (!model.HasData || model.Row.Id != unitId || atlas == null) return;

        // 解析对应的切片名称
        string spriteName = GetArchetypeSpriteName(archetype);
        Sprite sprite = atlas.GetSprite(spriteName);

        // 赋值并显示图标
        if (sprite != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[CultivatePanel] 未在 Tag 图集中找到 Archetype 图标: {spriteName} (archetype={archetype})");
        }
    }

    /// <summary>
    /// 根据角色主要定位类型解析 Tag 图集中对应的 Tag_Small_* 切片名称。
    /// </summary>
    /// <param name="archetype">定位类型字符串。</param>
    /// <returns>Tag 图集内的 Sprite 切片名称。</returns>
    private static string GetArchetypeSpriteName(string archetype)
    {
        if (string.IsNullOrWhiteSpace(archetype)) return "Tag_Small_DPS";

        string key = archetype.Trim();
        if (key.StartsWith("Tag_Small_", StringComparison.OrdinalIgnoreCase)) return key;

        switch (key.ToLowerInvariant())
        {
            case "dps":
            case "maindps":
            case "subdps":
            case "attack":
                return "Tag_Small_DPS";

            case "defence":
            case "defense":
            case "tank":
            case "defender":
                return "Tag_Small_Defence";

            case "support":
            case "buffer":
                return "Tag_Small_Support";

            case "heal":
            case "healer":
                return "Tag_Small_Heal";

            default:
                return "Tag_Small_DPS";
        }
    }

    /// <summary>标签图集面板级加载（租约登记进 ResourceScope 随面板释放，任务去重）：失败返回 null 并清任务，下次刷新重试。</summary>
    private async Task<SpriteAtlas> EnsureTagAtlasAsync()
    {
        if (tagAtlasTask == null) tagAtlasTask = LoadTagAtlasAsync();
        SpriteAtlas atlas = await tagAtlasTask;
        if (atlas == null) tagAtlasTask = null;
        return atlas;
    }

    private async Task<SpriteAtlas> LoadTagAtlasAsync()
    {
        try
        {
            if (ResourceScope == null || ResourceScope.IsDisposed) return null;
            ResourceLease<SpriteAtlas> lease = await AddressableManager.Instance
                .AcquireAssetAsync<SpriteAtlas>(tagAtlasAddress, ResourceScope);
            //等待加载期间面板已销毁（作用域已释放）：租约未登记进作用域，手动归还防计数悬挂
            if (ResourceScope.IsDisposed)
            {
                lease?.Dispose();
                return null;
            }
            return lease?.Asset;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CultivatePanel] 标签图集加载失败：{tagAtlasAddress}（{e.Message}）");
            return null;
        }
    }

    /// <summary>暴击数值格式化：0~1 小数 → 整数百分比。</summary>
    private static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    #endregion

    /// <summary>返回角色列表：关闭本面板。</summary>
    private void CloseSelf()
    {
        PanelManager.Instance.ClosePanel(this);
    }
}
