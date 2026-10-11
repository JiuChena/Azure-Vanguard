using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Abilities;
using Framework.Gameplay.Player;
using Framework.Gameplay.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色列表面板控制层（MVC 的 C）：从玩家角色仓库 + 角色表取数，按排序方式刷新列表项；
/// 列表项经 ObjectsPool 池化复用（排序切换/事件刷新只重绑数据，不销毁重建），
/// 列表项点击打开角色养成面板（CultivatePanel）并绑定该角色。
/// 状态栏（GearBar）为自管理复用组件，货币刷新与默认按钮零接线，面板仅注册返回按钮语义。
/// </summary>
public class CharacterListPanel : PanelBase
{
    [Header("View 分区")]
    //顶部状态栏分区：返回/货币/加号/设置/回主页
    [SerializeField] private GearBar gearBar;
    //角色列表页分区：滚动内容区/排序下拉框
    [SerializeField] private CHListView chListView;

    [Header("数据")]
    //列表项预制体：实例化到 chListView.scrollContent 下
    [SerializeField] private GameObject itemPrefab;
    //稀有度背景（下标 = 稀有度-1）；素材未入库前留空，代码保留默认图
    [SerializeField] private Sprite[] rarityBgs;

    //角色表缓存：InitTableAsync 就绪后可用（主键为字符串 id，如 CH0221）；行缺失的仓库条目刷新时跳过
    private DataTable<CharacterRow, string> characterTable;

    //显示名准绳（SO.Name，2026-10-07 拍板）：unitId → 名称；表加载后预热一次，未命中回退表行 Name
    private readonly Dictionary<string, string> nameOverrides = new Dictionary<string, string>();

    //排序方式：下标与排序下拉框选项一致（0=默认/获得顺序 1=稀有度 2=等级）
    private int sortMode;

    //展示数据：仓库条目与角色表行的配对列表，刷新时整体重建
    private readonly List<(CharacterEntry entry, CharacterRow row)> displayList =
        new List<(CharacterEntry, CharacterRow)>();

    protected override void EventInit()
    {
        //返回按钮为本面板语义，手动注册返回回调（货币刷新/默认按钮由 GearBar 自管理，面板零接线）
        if (gearBar != null) gearBar.RegisterGoBack(CloseSelf);

        //排序下拉框：选项运行时填充（预制体只留默认占位项），变更即重刷列表
        if (chListView != null && chListView.sortType != null)
        {
            chListView.sortType.ClearOptions();
            chListView.sortType.AddOptions(new List<string> { "Default", "Rarity", "Level" });
            chListView.sortType.SetValueWithoutNotify(0);
            chListView.sortType.onValueChanged.AddListener(mode =>
            {
                sortMode = mode;
                RefreshList();
            });
        }

        //仓库变更 → 列表自动跟随（获得角色/养成数据变化都会广播）
        EventCenter.Instance.Register(EventNames.CharacterAcquired, RefreshList);
        EventCenter.Instance.Register(EventNames.CharacterDataUpdated, RefreshList);
    }

    protected override void ComponentInit()
    {
        //表异步就绪后首刷；事件先于表就绪到来时 RefreshList 以空表安全跳过
        InitTableAsync();
    }

    /// <summary>
    /// 面板销毁时撤销事件监听，防止向已销毁的列表组件写入。
    /// </summary>
    protected override void OnDestroy()
    {
        EventCenter.Instance.Unregister(EventNames.CharacterAcquired, RefreshList);
        EventCenter.Instance.Unregister(EventNames.CharacterDataUpdated, RefreshList);
        base.OnDestroy();
    }

    #region 列表刷新

    /// <summary>
    /// 重建列表：仓库快照合角色表行 → 按排序方式排序 → 实例化列表项并填充；
    /// 表未就绪或引用缺失时安全跳过（异步首刷稍后会再触发一次）。
    /// </summary>
    private void RefreshList()
    {
        if (chListView == null || chListView.scrollContent == null || itemPrefab == null || characterTable == null) return;

        //取数：仓库快照合表行；表缺失行报错跳过，不阻断整个列表
        displayList.Clear();
        foreach (CharacterEntry entry in CharacterWarehouseManager.Instance.Snapshot())
        {
            if (characterTable.TryGet(entry.unitId, out CharacterRow row)) displayList.Add((entry, row));
            else Debug.LogError($"[CharacterListPanel] CharacterTable 无角色 {entry.unitId} 的行，已跳过");
        }

        //排序：0=默认（获得顺序）1=稀有度降序 2=等级降序；同值按 id 升序保持稳定
        switch (sortMode)
        {
            case 1:
                displayList.Sort((a, b) => b.row.Rarity != a.row.Rarity
                    ? b.row.Rarity - a.row.Rarity : string.CompareOrdinal(a.entry.unitId, b.entry.unitId));
                break;
            case 2:
                displayList.Sort((a, b) => b.entry.level != a.entry.level
                    ? b.entry.level - a.entry.level : string.CompareOrdinal(a.entry.unitId, b.entry.unitId));
                break;
        }

        //池化刷新：旧列表项全部入池（ObjectsPool 挂池根并禁用），再按新顺序取出重新赋值——
        //排序切换/仓库事件只重绑数据，不销毁重建
        int pooledCount = 0;
        for (int i = chListView.scrollContent.childCount - 1; i >= 0; i--)
        {
            Transform child = chListView.scrollContent.GetChild(i);
            if (child.GetComponent<CHListItem>() != null)
            {
                ObjectsPool.Instance.Put(child.gameObject);
                pooledCount++;
            }
        }
        foreach ((CharacterEntry entry, CharacterRow row) in displayList)
        {
            GameObject go = ObjectsPool.Instance.Get(itemPrefab, chListView.scrollContent);
            CHListItem cell = go != null ? go.GetComponent<CHListItem>() : null;

            //挂载后归一变换：池的 Instantiate 在无父状态克隆，模板的局部尺度/位置被当作世界尺度，
            //挂回非 1 尺度的画布链时新实例的 scale 与位置同被放大 1/P 倍——
            //scale 显式归一，位置清零（GridLayout 接管排布前避免错位）
            if (go != null)
            {
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = Vector3.zero;
            }

            FillCell(cell, entry, row);

            //点击 → 打开养成面板并绑定该角色（池化复用：清掉上一任数据的监听再重绑；
            //按钮引用走 View 序列化字段拖赋值，不做运行时组件查找）
            Button clickButton = cell != null ? cell.clickButton : null;
            if (clickButton != null)
            {
                string unitId = entry.unitId;
                clickButton.onClick.RemoveAllListeners();
                clickButton.onClick.AddListener(() => OpenCultivate(unitId));
            }
        }

        //池化诊断：入池与取出同帧完成，池静止时无存货；入池数 ≥ 重建数即全程复用零新建
        Debug.Log($"[CharacterListPanel] 池化刷新：入池 {pooledCount}，重建 {displayList.Count} 项");
    }

    /// <summary>
    /// 打开角色养成面板并绑定角色；面板已开时直接补挂（OpenPanel 重名去重会吞回调）。
    /// </summary>
    /// <param name="unitId">要查看的角色 ID（如 CH0221）。</param>
    private void OpenCultivate(string unitId)
    {
        CultivatePanel panel = PanelManager.Instance.GetPanel<CultivatePanel>(UIPath.CULTIVATEPANEL);
        if (panel != null) panel.Bind(unitId);
        else PanelManager.Instance.OpenPanel<CultivatePanel>(UIPath.CULTIVATEPANEL, UILayer.Top, p => p.Bind(unitId));
    }

    /// <summary>
    /// 填充单个列表项：名字/等级/稀有度底图；角色图标列入表前保留默认图。
    /// </summary>
    /// <param name="cell">列表项 View；为 null 时跳过。</param>
    /// <param name="entry">仓库养成条目（等级等实时值）。</param>
    /// <param name="row">角色表行（名字/稀有度等静态值）。</param>
    private void FillCell(CHListItem cell, CharacterEntry entry, CharacterRow row)
    {
        if (cell == null) return;

        //绑定来源：池化复用下异步头像回包以此校验（改绑即丢弃）
        cell.unitId = entry.unitId;

        //名字准绳 = SO 的 Name（PreloadNamesAsync 预热），未命中回退表行 Name
        if (cell.nameText != null)
        {
            cell.nameText.text = nameOverrides.TryGetValue(entry.unitId, out string name)
                ? name : row.Name;
        }
        if (cell.levelText != null) cell.levelText.text = $"Lv.{entry.level}";

        //稀有度背景：下标 = 稀有度-1，越界或未配置时保留默认图（与 HomePanel 名片同口径）
        if (cell.rarityBg != null && rarityBgs != null
            && row.Rarity >= 1 && row.Rarity <= rarityBgs.Length && rarityBgs[row.Rarity - 1] != null)
        {
            cell.rarityBg.sprite = rarityBgs[row.Rarity - 1];
        }

        //星级角标：星数 = 稀有度；0 星时隐藏整个角标
        if (cell.starIcon != null) cell.starIcon.gameObject.SetActive(row.Rarity > 0);
        if (cell.starNumText != null) cell.starNumText.text = $"{row.Rarity}";

        //头像：单图按需（CHAvatar_Collection_<unitId>）；未命中保留默认图
        if (cell.iconImg != null) _ = FillAvatarAsync(cell, entry.unitId);
    }

    /// <summary>异步填充单元格头像；回包时单元格已改绑其他角色（池化复用）则丢弃结果。</summary>
    private static async Task FillAvatarAsync(CHListItem cell, string unitId)
    {
        Sprite avatar = await IconLibrary.GetCharacterAvatarAsync(IconLibrary.VariantCollection, unitId);
        if (avatar == null || cell == null || cell.unitId != unitId) return;
        if (cell.iconImg != null) cell.iconImg.sprite = avatar;
    }

    #endregion

    #region Private

    /// <summary>
    /// 加载角色表（DataTableManager 命中缓存时直接短路）并首刷列表；加载失败时报错，列表保持空。
    /// </summary>
    private async void InitTableAsync()
    {
        try
        {
            characterTable = await DataTableManager.Instance.LoadTableAsync<CharacterRow, string>("CharacterTable");
            if (characterTable == null)
            {
                Debug.LogError("[CharacterListPanel] 角色表加载失败，列表为空");
                return;
            }
            await PreloadNamesAsync();
            RefreshList();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// 预热显示名：批量加载全表角色的 UnitConfig（常驻缓存），名字以 SO 的 Name 为准绳；
    /// 加载失败的 id 不入映射，FillCell 回退表行 Name。
    /// </summary>
    private async Task PreloadNamesAsync()
    {
        try
        {
            List<string> ids = new List<string>(characterTable.Count);
            foreach (CharacterRow row in characterTable.Rows) ids.Add(row.Id);
            await UnitConfigLibrary.PreloadAsync(ids);

            nameOverrides.Clear();
            foreach (string id in ids)
            {
                if (UnitConfigLibrary.TryGetCached(id, out UnitConfig config)
                    && !string.IsNullOrEmpty(config.Name)) nameOverrides[id] = config.Name;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>返回主城：关闭本面板。</summary>
    private void CloseSelf()
    {
        PanelManager.Instance.ClosePanel(this);
    }

    #endregion
}
