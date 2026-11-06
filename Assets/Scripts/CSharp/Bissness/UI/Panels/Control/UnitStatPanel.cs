using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Abilities.Buff;
using Framework.Gameplay.Abilities.Display;
using Framework.Gameplay.Battle;
using Framework.Gameplay.Player;
using Framework.Gameplay.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// UnitStatPanel 副面板控制（嵌入 CombatPanel 的副面板，非 PanelBase——不占用
/// PanelManager 的 ESC 栈/ResourceScope/延迟销毁；显隐由本组件直接驱动 SetActive）。
///
/// 交互语义（用户拍板 2026-10-23）：
/// - 显示时失活 CombatPanel 的 CharacterList/Status/Pause 三兄弟，关闭时重新激活；
/// - 打开 = 左键点击战场单位选中（点击探针宿主 = CombatPanel.OnUpdate 调 <see cref="PollOpenClick"/>，
///   副面板自身关闭时组件不跑 Update，探针必须挂在恒活的 CombatPanel 上）；
/// - 关闭 = CloseBtn / 点空白（清选中）；ESC 本轮不接（PanelManager ESC 栈冲突，挂账）；
/// - 三页 Tab（Stat/Skills/Effect）Toggle 互斥切换；
/// - 打开即暂停战斗（③ 停推 + timeScale 冻结），关闭即恢复（打开前已手动暂停则保持不抢恢复）。
///
/// 数据链（对齐 DisplayAbility 交付）：
/// - 头部 + 字段：BattleSelection.SelectedView → GAS → OwnerContext.TryGet(Display) →
///   DisplayRuntimeData.BuildModel()（纯函数逐帧产全新模型）→ 渲染；选中单位变化时
///   技能页/头像刷新一次（技能走 SkillDataLibrary + 仓库技能等级，敌人 v1 技能页隐藏）；
/// - StatPage 定位 + 标签：静态角色表数据（CharacterRow），单位变化时刷新一次
///   （定位文本 = Archetype ?? Profession + Tag 图集 Tag_Small_* 图标；标签 = CHTagView 池化，
///   敌人 v1 无角色表行整块隐藏 + 清空标签）；
/// - EffectPage buff 行：同 Display 路径读 Buff 键 → BuffRuntimeData.Buffs（逐帧 BuildEffectSignature
///   签名门控 reconcile）→ BattleDataModule 查 SkillEffectTable 效果行（name/desc/icon）+ IconLibrary
///   异步图标，行池化克隆 EffectRowTemplate；表由战斗预热加载（PreloadAsync 已覆盖）。
/// </summary>
public class UnitStatPanel : MonoBehaviour
{
    // buff 影响行的数值高亮色（Modified 行）
    private static readonly Color BuffHighlightColor = new Color(1f, 0.9f, 0.25f);

    [Header("View")]
    [Tooltip("副面板视图（SetupUnitStatPanel 接线；缺 View 停摆不报错刷屏）")]
    [SerializeField] private UnitStatPanelView view;

    [Header("StatPage 定位与标签")]
    [Tooltip("角色标签单元格预制体（培养面板 chTagPrefab 同款 TagItem.prefab，SetupUnitStatPanel 抄引用）")]
    [SerializeField] private GameObject chTagPrefab;

    // Tag 图集地址（定位小图标 Tag_Small_* / 标签图 Tag_<名> 切片所在，与培养面板同口径）
    private const string TagAtlasAddress = "Tag";

    // Tag 图集加载任务（去重：同一次刷新只发一次加载）；失败清任务下次刷新重试
    private Task<SpriteAtlas> tagAtlasTask;

    // Tag 图集常驻租约：本面板非 PanelBase 无 ResourceScope，无作用域获取、OnDestroy 手动释放
    private ResourceLease<SpriteAtlas> tagAtlasLease;

    // 打开状态（open 与 gameObject.activeSelf 由 Close/Open 同步维护；CombatPanel 关闭副面板时
    // 本组件 Update 停跑，重开 CombatPanel 后状态仍在）
    private bool open;

    // 本面板打开前的暂停状态：打开时暂停战斗、关闭时恢复；打开前已暂停（玩家先按了暂停键）
    // 则关闭不抢恢复（保存之前的状态）
    private bool wasPausedBeforeOpen;

    // 已渲染技能的选中单位 id（技能/头像仅在单位变化时刷新一次；null = 未刷新过）
    private string lastUnitId;

    // 字段行数值的原始颜色（Modified 高亮前后切换用；首次写入时捕获）
    private readonly Color[] rowNormalColors = new Color[7];

    // 行颜色是否已捕获
    private readonly bool[] rowColorsCaptured = new bool[7];

    // 已渲染的 Effect 页签名（当前 active buffs 的 BuffId 序列含叠层；变化才 reconcile）
    private string lastEffectSignature;

    // 池化中的 buff 行绑定（EffectItemView，EffectItem 预制体挂的 ItemView）
    private readonly List<EffectItemView> effectRows = new List<EffectItemView>();

    // Buff 行模板缺失告警只报一次
    private bool effectTemplateWarned;

    // Buff 行模板保护已做（首见把模板实例失活，防其作为幻影行渲染；仅一次）
    private bool effectTemplateDisabled;

    /// <summary>面板当前是否打开（CombatPanel 探针据此决定是否接点击）。</summary>
    public bool IsOpen => open;

    #region 生命周期

    private void Awake()
    {
        if (view == null) view = GetComponent<UnitStatPanelView>();
        if (view == null)
        {
            Debug.LogError("[UnitStatPanel] 缺 UnitStatPanelView，副面板停摆。", this);
            enabled = false;
            return;
        }
        view.EnsureBindings();
        WireTabs();
        if (view.CloseBtn != null) view.CloseBtn.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (view != null && view.CloseBtn != null) view.CloseBtn.onClick.RemoveListener(Close);
        // 释放 Tag 图集常驻租约（本面板无 ResourceScope，必须手动归还）
        if (tagAtlasLease != null) tagAtlasLease.Dispose();
        tagAtlasLease = null;
    }

    #endregion

    #region 显隐与点击

    /// <summary>
    /// 逐帧（面板打开时）：点单位 → 重选中（切单位刷技能）；点空白 → 清选中并关闭；
    /// 无战斗/失去选中 → 关闭。头部 + 字段每帧刷新。
    /// </summary>
    private void Update()
    {
        if (!open) return;

        BattleSelection selection = BattleSelection.Current;
        if (selection == null || !selection.HasSelection)
        {
            Close();
            return;
        }

        if (CombatSelectionPicker.TryPick(out BattleUnitState unit, out GameObject picked))
        {
            selection.Select(unit, picked);
            RefreshSelection();
        }
        else if (CombatSelectionPicker.IsWorldClick())
        {
            selection.Clear();
            Close();
            return;
        }

        RefreshReadout();
    }

    /// <summary>
    /// 点击探针（CombatPanel.OnUpdate 恒活宿主调用）：面板关闭时，左键命中单位 → 选中并打开。
    /// 面板打开时不处理（本组件 Update 接管点击）。
    /// </summary>
    public void PollOpenClick()
    {
        if (open) return;
        BattleSelection selection = BattleSelection.Current;
        if (selection == null) return;
        if (!CombatSelectionPicker.TryPick(out BattleUnitState unit, out GameObject picked)) return;

        selection.Select(unit, picked);
        Open();
    }

    /// <summary>打开：激活本面板 + 失活三兄弟 + 全量刷新（技能强制重刷）+ 暂停战斗。</summary>
    public void Open()
    {
        if (open) return;
        open = true;
        gameObject.SetActive(true);
        SetSiblingsActive(false);
        lastUnitId = null;
        SynchronizeTabs();
        RefreshSelection();
        RefreshReadout();
        SetPauseForPanel(true);
    }

    /// <summary>关闭：失活本面板 + 重新激活三兄弟（选中保留，面板可重开）+ 恢复战斗。</summary>
    public void Close()
    {
        if (!open) return;
        open = false;
        gameObject.SetActive(false);
        SetSiblingsActive(true);
        SetPauseForPanel(false);
    }

    // 打开/关闭联动暂停：打开时暂停（③ 停推 + timeScale 冻结表现层）、关闭时恢复；
    // 打开前已暂停则关闭不恢复（保持玩家手动暂停）。无战斗时跳过。
    private void SetPauseForPanel(bool opening)
    {
        CombatService mgr = CombatService.Instance;
        if (!mgr.IsBattleActive) return;
        if (opening)
        {
            wasPausedBeforeOpen = mgr.SimulationPaused;
            if (!wasPausedBeforeOpen) mgr.TogglePause();
        }
        else if (!wasPausedBeforeOpen && mgr.SimulationPaused)
        {
            mgr.TogglePause();
        }
    }

    // 三兄弟显隐（CharacterList / Status / Pause；值未变不 SetActive）
    private void SetSiblingsActive(bool active)
    {
        GameObject[] siblings = view != null ? view.SiblingsToHideOnOpen : null;
        if (siblings == null) return;
        for (int i = 0; i < siblings.Length; i++)
        {
            GameObject sibling = siblings[i];
            if (sibling != null && sibling.activeSelf != active) sibling.SetActive(active);
        }
    }

    #endregion

    #region Tab 页切换

    // 三 Toggle → 三页；手动互斥（ToggleGroup 存在时已互斥，SetIsOnWithoutNotify 为无操作兜底）
    private void WireTabs()
    {
        Toggle[] toggles = view.PageToggles;
        if (toggles == null) return;
        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle toggle = toggles[i];
            if (toggle == null) continue;
            int index = i;
            toggle.onValueChanged.AddListener(on => { if (on) ActivateTab(index); });
        }
    }

    // 打开时按 Toggle 现状同步页显隐；全关时强制回第一页（Stat）
    private void SynchronizeTabs()
    {
        Toggle[] toggles = view.PageToggles;
        GameObject[] roots = view.PageRoots;
        bool anyOn = false;
        if (toggles != null)
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                bool on = toggles[i] != null && toggles[i].isOn;
                if (on) anyOn = true;
                SetPageActive(i, on, roots);
            }
        }
        if (!anyOn && toggles != null && toggles.Length > 0 && toggles[0] != null)
        {
            toggles[0].SetIsOnWithoutNotify(true);
            SetPageActive(0, true, roots);
        }
    }

    // 页显隐 + 兄弟页互斥（切到 index 时其余 Toggle 置 Off，ToggleGroup 在时无操作）
    private void ActivateTab(int index)
    {
        Toggle[] toggles = view.PageToggles;
        GameObject[] roots = view.PageRoots;
        if (toggles != null)
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                if (i == index || toggles[i] == null) continue;
                if (toggles[i].isOn) toggles[i].SetIsOnWithoutNotify(false);
            }
        }
        for (int i = 0; i < (roots != null ? roots.Length : 0); i++)
        {
            SetPageActive(i, i == index, roots);
        }
    }

    private void SetPageActive(int index, bool active, GameObject[] roots)
    {
        if (roots == null || index < 0 || index >= roots.Length || roots[index] == null) return;
        if (roots[index].activeSelf != active) roots[index].SetActive(active);
    }

    #endregion

    #region 数据刷新

    // 头部 + 字段逐帧：选中视图 → GAS → Display 数据 → BuildModel → 渲染（值未变不重写）
    private void RefreshReadout()
    {
        BattleSelection selection = BattleSelection.Current;
        if (selection == null || !selection.HasSelection) return;
        BattleUnitState unit = selection.SelectedUnit;
        if (unit == null) return;

        // Effect 页（buff 行）独立于 Display 键（Buff 能力只等 BattleUnit+Stat 双键，可能先就绪）
        RefreshEffectPage();

        DisplayRuntimeData displayData = null;
        GameObject selectedView = selection.SelectedView;
        if (selectedView != null)
        {
            // GetComponentInParent：命中视图可能是子骨骼节点（CH0221 碰撞体在 Bip001 Pelvis 上），GAS 在根——
            // 从命中节点沿父链上溯找根上的 GAS，否则子节点拿不到能力组件面板恒 Not ready（2026-10-27 修）
            GameplayAbilitySystem gas = selectedView.GetComponentInParent<GameplayAbilitySystem>();
            if (gas != null && gas.OwnerContext != null)
            {
                gas.OwnerContext.TryGet(AbilityRuntimeDataType.Display, out displayData);
            }
        }

        if (displayData == null)
        {
            WriteText(view.UnitNameText, unit.UnitId);
            WriteText(view.HealthText, "Not ready");
            if (view.HpFill != null) view.HpFill.fillAmount = 0f;
            ShowStatRows(0);
            return;
        }

        DisplayModel model = displayData.BuildModel();
        WriteText(view.UnitNameText, model.DisplayName);
        WriteText(view.HealthText, $"{model.CurrentHp} / {model.MaxHp}");
        if (view.HpFill != null) view.HpFill.fillAmount = model.HpRatio;
        RenderFields(model);
    }

    // Effect 页（buff 行）：签名门控 reconcile——面板打开战斗已暂停 buff 不 tick，签名未变不重刷；
    // Buff 表未加载先 fire-and-forget，就绪后签名含「表已加载」位触发一次重渲染。
    // 无 buff 键/未就绪（BuffAbility 未装配或仍等 BattleUnit+Stat）→ 空转不刷错（挂账装配项）。
    private void RefreshEffectPage()
    {
        BattleSelection selection = BattleSelection.Current;
        if (selection == null || !selection.HasSelection) return;
        GameObject selectedView = selection.SelectedView;
        if (selectedView == null) return;

        GameplayAbilitySystem gas = selectedView.GetComponentInParent<GameplayAbilitySystem>();
        if (gas == null || gas.OwnerContext == null) return;
        if (!gas.OwnerContext.TryGet(AbilityRuntimeDataType.Buff, out BuffRuntimeData buffData)) return;

        string signature = BuildEffectSignature(buffData);
        if (lastEffectSignature == signature) return;
        lastEffectSignature = signature;
        RefreshEffectRows(buffData, signature);
    }

    // 签名 = 当前 active buffs 的 BuffId 序列含叠层（如 "1,3,3,5"）；空列表 = 空串
    private static string BuildEffectSignature(BuffRuntimeData buffData)
    {
        if (buffData == null || buffData.BuffCount == 0) return string.Empty;
        IReadOnlyList<IBuff> buffs = buffData.Buffs;
        StringBuilder sb = new StringBuilder(buffs.Count * 4);
        for (int i = 0; i < buffs.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(buffs[i].BuffId);
        }
        return sb.ToString();
    }

    // 重渲染 buff 行：先回池现有行 → 无 buff 直接清空 → 模板缺失告警一次 → 池化克隆 + 填元数据
    // 效果数据查 SkillEffectTable（一条 effect 自带 name/icon/desc；BuffTable 已于 2026-11-01 删除）
    private void RefreshEffectRows(BuffRuntimeData buffData, string signature)
    {
        if (lastEffectSignature != signature) return; // 竞态弃写（面板打开期战斗已暂停，防御性）

        ClearEffectRows();
        if (buffData == null || buffData.BuffCount == 0) return;

        GameObject template = view.EffectRowTemplate;
        if (template == null)
        {
            if (!effectTemplateWarned)
            {
                effectTemplateWarned = true;
                Debug.LogWarning("[UnitStatPanel] Effect 页行模板缺失（EffectRowTemplate），buff 行不渲染。", this);
            }
            return;
        }
        ProtectEffectTemplate(template);

        Transform content = view.EffectContentRoot != null ? view.EffectContentRoot.transform : null;
        foreach (IBuff buff in buffData.Buffs)
        {
            GameObject clone = ObjectsPool.Instance.Get(template, content);
            if (clone == null) continue;
            // 挂载后归一变换：Item 模板 pivot 在中心（0.5,0.5），直接 localPosition=0 会让中心贴父左上、
            // 整体偏移出界；改为左上锚定 + pivot 左上 + 行序纵向堆叠（Effect 页 Content 左上布局）
            RectTransform rt = clone.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -effectRows.Count * rt.sizeDelta.y);
                // z 轴归零：池化克隆 + SetParent(worldPositionStays) 后 z 漂到 -8000，UI 应同一平面
                Vector3 localPos = rt.localPosition;
                localPos.z = 0f;
                rt.localPosition = localPos;
            }
            else
            {
                clone.transform.localScale = Vector3.one;
                clone.transform.localPosition = Vector3.zero;
            }

            EffectItemView row = clone.GetComponent<EffectItemView>();
            if (row == null) continue;
            effectRows.Add(row);

            bool found = BattleDataModule.TryGetSkillEffect(buff.BuffId, out SkillEffectRow effectRow);
            WriteText(row.buffName, found ? effectRow.Name : $"#{buff.BuffId}");
            WriteText(row.buffDesc, found ? effectRow.Desc : string.Empty);
            if (!found || string.IsNullOrEmpty(effectRow.Icon)) continue;
            LoadEffectIconAsync(row, effectRow.Icon, signature);
        }
    }

    // 现有 buff 行全部回池（reconcile 前置；行根可能被回收，集合同时清空）
    private void ClearEffectRows()
    {
        for (int i = 0; i < effectRows.Count; i++)
        {
            GameObject rowRoot = effectRows[i].gameObject;
            if (rowRoot != null) ObjectsPool.Instance.Put(rowRoot);
        }
        effectRows.Clear();
    }

    // Buff 行模板保护：模板实例若仍激活（设计期当行用），首见失活防其作为幻影行渲染（仅一次）
    private void ProtectEffectTemplate(GameObject template)
    {
        if (effectTemplateDisabled || template == null) return;
        if (template.activeSelf)
        {
            template.SetActive(false);
            Debug.LogWarning("[UnitStatPanel] Effect 页行模板实例仍激活（应只作模板），已失活防幻影行。", this);
        }
        effectTemplateDisabled = true;
    }

    // Buff 图标异步加载（失败保留默认图）；回包时签名已变（行已回池/重渲染）则丢弃
    private async void LoadEffectIconAsync(EffectItemView row, string icon, string signature)
    {
        if (string.IsNullOrEmpty(icon) || row.buffIcon == null) return;
        Sprite sprite;
        try
        {
            sprite = await IconLibrary.GetAsync(icon);
        }
        catch (System.Exception)
        {
            return; // icon 资产未配（Addressables 无 Location）→ 静默跳过，保留默认图
        }
        if (sprite != null && row.buffIcon != null && lastEffectSignature == signature)
        {
            row.buffIcon.sprite = sprite;
        }
    }

    // 字段行：按模型条目序写标签/值；超出面板行数隐藏；Modified 行数值高亮
    private void RenderFields(DisplayModel model)
    {
        int rows = Mathf.Min(model.Fields.Count, view.StatRowNames != null ? view.StatRowNames.Length : 0);
        ShowStatRows(rows);
        for (int i = 0; i < rows; i++)
        {
            DisplayFieldValue field = model.Fields[i];
            if (view.StatRowNames != null && i < view.StatRowNames.Length)
                WriteText(view.StatRowNames[i], field.Label);
            if (view.StatRowValues != null && i < view.StatRowValues.Length)
            {
                TMP_Text value = view.StatRowValues[i];
                WriteText(value, field.Text);
                if (value != null)
                {
                    if (!rowColorsCaptured[i]) { rowNormalColors[i] = value.color; rowColorsCaptured[i] = true; }
                    Color target = field.Modified ? BuffHighlightColor : rowNormalColors[i];
                    if (value.color != target) value.color = target;
                }
            }
        }
    }

    // 字段行显隐：只显示前 count 行（行根 = 标签文本的父级）
    private void ShowStatRows(int count)
    {
        if (view.StatRowNames == null) return;
        for (int i = 0; i < view.StatRowNames.Length; i++)
        {
            TMP_Text name = view.StatRowNames[i];
            GameObject row = name != null ? name.transform.parent.gameObject : null;
            bool show = i < count;
            if (row != null && row.activeSelf != show) row.SetActive(show);
        }
    }

    // 技能 + 头像：选中单位变化时刷新一次（技能表 + 仓库技能等级；敌人 v1 隐藏全部技能行）
    private async void RefreshSelection()
    {
        BattleSelection selection = BattleSelection.Current;
        if (selection == null || !selection.HasSelection) return;
        BattleUnitState unit = selection.SelectedUnit;
        if (unit == null || unit.UnitId == lastUnitId) return;
        lastUnitId = unit.UnitId;

        // 头像：角色走 IconLibrary（地址 CHAvatar_Small_<unitId>）；敌人 v1 无头像保留默认图
        if (view.AvatarImage != null && unit.Camp == BattleCamp.Character)
        {
            Sprite avatar = await IconLibrary.GetCharacterAvatarAsync(IconLibrary.VariantSmall, unit.UnitId);
            if (avatar != null && view.AvatarImage != null && lastUnitId == unit.UnitId)
            {
                view.AvatarImage.sprite = avatar;
            }
        }

        // 定位 + 标签：静态角色表数据，单位变化时刷新一次（敌人 v1 无角色表行：整块隐藏 + 清空标签）
        RefreshArchetypeAndTags(unit);

        if (view.SkillRows == null || view.SkillRows.Length == 0) return;

        if (unit.Camp != BattleCamp.Character)
        {
            HideAllSkillRows();
            return;
        }

        await SkillDataLibrary.EnsureLoadedAsync();
        for (int i = 0; i < view.SkillRows.Length; i++)
        {
            UnitStatPanelView.SkillRowBinding row = view.SkillRows[i];
            if (row == null) continue;

            if (!SkillDataLibrary.TryGetSkillRow(unit.UnitId, row.SkillType, out CharacterSkillRow skillRow))
            {
                if (row.RowRoot != null && row.RowRoot.activeSelf) row.RowRoot.SetActive(false);
                continue;
            }

            WriteText(row.SkillName, skillRow.Name);
            int level = SkillLevelForType(unit.UnitId, row.SkillType);
            string desc = SkillDataLibrary.FormatDesc(skillRow, level);
            WriteText(row.SkillDescription, desc ?? string.Empty);
            if (row.RowRoot != null && !row.RowRoot.activeSelf) row.RowRoot.SetActive(true);
            LoadSkillIconAsync(row, skillRow.Icon, unit.UnitId);
        }
    }

    private void HideAllSkillRows()
    {
        for (int i = 0; i < view.SkillRows.Length; i++)
        {
            UnitStatPanelView.SkillRowBinding row = view.SkillRows[i];
            if (row != null && row.RowRoot != null && row.RowRoot.activeSelf) row.RowRoot.SetActive(false);
        }
    }

    // 技能等级：角色走仓库养成条目（CharacterSkillLevelTable 对应等级），无条目/敌人回退 1
    private static int SkillLevelForType(string unitId, string skillType)
    {
        int level = 1;
        if (CharacterWarehouseManager.Instance.TryGetEntry(unitId, out CharacterEntry entry) && entry != null)
        {
            switch (skillType)
            {
                case CharacterSkillRow.SkillTypes.E: level = entry.skillLevelE; break;
                case CharacterSkillRow.SkillTypes.Burst: level = entry.skillLevelQ; break;
                case CharacterSkillRow.SkillTypes.Talent: level = entry.skillLevelTalent; break;
                default: level = entry.skillLevelNormal; break;
            }
        }
        return Mathf.Max(level, 1);
    }

    // 技能图标异步加载（失败保留默认图）；回包时已切单位则丢弃
    private async void LoadSkillIconAsync(UnitStatPanelView.SkillRowBinding row, string icon, string unitId)
    {
        if (string.IsNullOrEmpty(icon) || row.SkillIcon == null) return;
        Sprite sprite = await IconLibrary.GetAsync(icon);
        if (sprite != null && row.SkillIcon != null && lastUnitId == unitId)
        {
            row.SkillIcon.sprite = sprite;
        }
    }

    // 定位 + 标签刷新：敌人 v1 无 CharacterTable 行 → 隐藏定位节点并清空标签容器；
    // 角色 → 激活节点 + 异步查表刷文本/图标/标签（切单位按 lastUnitId 弃写）。
    private void RefreshArchetypeAndTags(BattleUnitState unit)
    {
        if (unit.Camp != BattleCamp.Character)
        {
            if (view.ArchetypeRoot != null && view.ArchetypeRoot.activeSelf)
                view.ArchetypeRoot.SetActive(false);
            ClearTagContent();
            return;
        }
        if (view.ArchetypeRoot != null && !view.ArchetypeRoot.activeSelf)
            view.ArchetypeRoot.SetActive(true);
        _ = RefreshArchetypeAndTagsAsync(unit.UnitId);
    }

    // 异步：查角色表取行（DataTableManager 命中缓存短路）→ 定位文本 + 图标 + 标签池化
    private async Task RefreshArchetypeAndTagsAsync(string unitId)
    {
        DataTable<CharacterRow, string> table;
        try
        {
            table = await DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return;
        }
        if (table == null || lastUnitId != unitId) return;
        if (!table.TryGet(unitId, out CharacterRow row)) return;

        string archetype = !string.IsNullOrEmpty(row.Archetype) ? row.Archetype : row.Profession;
        WriteText(view.ArchetypeText, archetype ?? string.Empty);
        _ = UpdateArchetypeIconAsync(view.ArchetypeIcon, archetype, unitId);
        _ = RefreshTagsAsync(view.TagContent, row.TagList, unitId);
    }

    // 异步：定位图标（Tag 图集 Tag_Small_* 切片）；切单位/图集未就绪弃写
    private async Task UpdateArchetypeIconAsync(Image icon, string archetype, string unitId)
    {
        if (icon == null) return;
        SpriteAtlas atlas = await EnsureTagAtlasAsync();
        if (lastUnitId != unitId || atlas == null) return;

        string spriteName = GetArchetypeSpriteName(archetype);
        Sprite sprite = atlas.GetSprite(spriteName);
        if (sprite != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[UnitStatPanel] 未在 Tag 图集中找到 Archetype 图标：{spriteName}（archetype={archetype}）");
        }
    }

    // 角色标签池化刷新（ObjectsPool 复用）：tagImage = 图集切片 Tag_<标签名>，方括号附带属性
    // 首键（如 Feeling_0）作心情 emoji 切片名；图缺失隐藏对应 Image；切单位竞态按 unitId 弃写。
    private async Task RefreshTagsAsync(RectTransform content, CharacterTagDecl[] tags, string unitId)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) ObjectsPool.Instance.Put(content.GetChild(i).gameObject);
        if (chTagPrefab == null || tags == null || tags.Length == 0) return;

        SpriteAtlas atlas = await EnsureTagAtlasAsync();
        if (lastUnitId != unitId || atlas == null) return;

        foreach (CharacterTagDecl tag in tags)
        {
            GameObject go = ObjectsPool.Instance.Get(chTagPrefab, content);
            if (go == null) continue;
            // 挂载后归一变换（池的 Instantiate 在无父状态克隆会放大尺度；与培养面板同口径）
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            CHTagView cell = go.GetComponent<CHTagView>();
            if (cell == null) continue;

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

    // 标签容器清空（敌人 v1：无标签，回池全部单元格）
    private void ClearTagContent()
    {
        if (view.TagContent == null) return;
        for (int i = view.TagContent.childCount - 1; i >= 0; i--)
            ObjectsPool.Instance.Put(view.TagContent.GetChild(i).gameObject);
    }

    // 定位类型 → Tag 图集 Tag_Small_* 切片名（培养面板同口径映射；空/未知回退 DPS）
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

    // Tag 图集加载（任务去重）：失败清任务，下次刷新重试
    private async Task<SpriteAtlas> EnsureTagAtlasAsync()
    {
        if (tagAtlasTask == null) tagAtlasTask = LoadTagAtlasAsync();
        SpriteAtlas atlas = await tagAtlasTask;
        if (atlas == null) tagAtlasTask = null;
        return atlas;
    }

    // Tag 图集常驻加载（本面板无 ResourceScope：无作用域租约自持，OnDestroy 释放；失败记日志返回 null）
    private async Task<SpriteAtlas> LoadTagAtlasAsync()
    {
        try
        {
            if (tagAtlasLease == null || tagAtlasLease.IsReleased)
                tagAtlasLease = await AddressableManager.Instance.AcquireAssetAsync<SpriteAtlas>(TagAtlasAddress);
            return tagAtlasLease != null ? tagAtlasLease.Asset : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UnitStatPanel] 标签图集加载失败：{TagAtlasAddress}（{e.Message}）");
            return null;
        }
    }

    #endregion

    // 文本写入节流：内容未变不重写（TMP 重建开销）
    private static void WriteText(TMP_Text target, string content)
    {
        if (target == null || target.text == content) return;
        target.text = content;
    }
}
