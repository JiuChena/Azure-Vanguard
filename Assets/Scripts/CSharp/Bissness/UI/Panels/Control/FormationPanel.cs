using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 战备面板控制层（MVC 的 C）：左右角色页循环切换、角色拖拽换位。
/// 状态栏（GearBar）为自管理复用组件，货币刷新与默认按钮零接线，面板仅注册返回按钮语义。
/// </summary>
public class FormationPanel : PanelBase, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    //角色槽位标签：拖拽换位的识别标记，由场景侧设置在角色节点上（layer 需为 UI）
    private const string CharacterTag = "Character";

    [Header("View 分区")]
    //顶部状态栏分区：返回/货币/加号/设置/回主页
    [SerializeField] private GearBar gearBar;
    //左右页切换分区：左右翻页按钮/编队索引页签/编队/战斗按钮
    [SerializeField] private CombatReadinessOtherView otherView;
    //左页角色列表分区：4 个角色信息栏，挂在 CHList1 页根节点上
    [SerializeField] private CHInfoList leftInfoList;
    //右页角色列表分区：4 个角色信息栏，挂在 CHList2 页根节点上
    [SerializeField] private CHInfoList rightInfoList;

    //当前是否显示左页（true=左页 false=右页）
    private bool isLeftPage = true;
    //拖拽起手时命中的角色节点（tag=character），松手换位判定用
    private Transform dragCharacter;
    //射线结果缓存列表，避免拖拽事件中反复分配
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    protected override void EventInit()
    {
        //返回按钮为本面板语义，手动注册返回回调（货币刷新/默认按钮由 GearBar 自管理，面板零接线）
        gearBar.RegisterGoBack(CloseSelf);

        //左右翻页：循环切换（最左再往左绕到最右，最右再往右绕回最左）
        otherView.leftButton.onClick.AddListener(SwitchCharacterPage);
        otherView.RightButton.onClick.AddListener(SwitchCharacterPage);

        //TODO 编队按钮：打开当前选中编队的编辑面板
        //TODO 战斗按钮：校验编队后进入战斗流程
        InitTeamToggles();
    }

    protected override void ComponentInit()
    {
        //默认显示左页，右页整页隐藏
        ShowLeftPage();
    }

    #region 编队索引页签

    //当前选中的编队索引（0 = 第 1 套配队）
    private int currentTeamIndex;

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

        //默认选中第 1 套配队，与 currentTeamIndex 同步
        otherView.toggles[0].isOn = true;
    }

    /// <summary>
    /// 切换编队：记录当前索引；编队配置 JSON 接入后在此加载对应配置并刷新角色槽位。
    /// </summary>
    /// <param name="teamIndex">编队索引（从 0 起）。</param>
    private void SwitchTeam(int teamIndex)
    {
        currentTeamIndex = teamIndex;

        //TODO 编队配置：读取编队配置 JSON 第 teamIndex 套，刷新 8 个角色槽位与信息栏
        Debug.Log($"[CombatReadinessPanel] 切换到第 {teamIndex + 1} 套配队（TODO 编队配置 JSON 未接入）");
    }

    #endregion

    #region 角色页切换

    /// <summary>
    /// 左右角色页循环切换：左页时切到右页，右页时切回左页（wrap 模式）。
    /// </summary>
    private void SwitchCharacterPage()
    {
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
    }

    /// <summary>
    /// 显示右页并隐藏左页（整页根节点开关）。
    /// </summary>
    private void ShowRightPage()
    {
        leftInfoList.gameObject.SetActive(false);
        rightInfoList.gameObject.SetActive(true);
        isLeftPage = false;
    }

    #endregion

    #region 角色拖拽换位

    /// <summary>
    /// 记录拖拽起手命中的角色节点；未从角色上起拖时为空。
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        dragCharacter = FindCharacter(eventData);
    }

    /// <summary>
    /// 拖拽过程不做跟随，松手时一次性判定换位。
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
    }

    /// <summary>
    /// 松手时命中另一角色则互换两者位置，并同步互换信息栏；落回原位或空处不处理。
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        //未从角色上起拖则不处理
        if (dragCharacter == null) return;

        //先取源并清空，避免异常路径下残留引用
        Transform source = dragCharacter;
        dragCharacter = null;

        Transform target = FindCharacter(eventData);

        //未命中或落回原角色则不换位
        if (target == null || target == source) return;

        SwapCharacter(source, target);
    }

    /// <summary>
    /// 互换两个角色的位置；信息栏不在角色子层级下时（标签设在角色展示层上），
    /// 用元组同步互换两个信息栏的位置使信息跟随角色。
    /// 另一页处于隐藏状态不会被射线命中，天然满足左页只与左页换、右页只与右页换。
    /// </summary>
    private void SwapCharacter(Transform source, Transform target)
    {
        (source.position, target.position) = (target.position, source.position);

        //信息栏在角色子层级下会随位置互换自然跟随，无需再换
        if (source.GetComponentInChildren<CharacterInformation>(true) != null) return;

        CharacterInformation sourceInfo = FindCharacterInformation(source);
        CharacterInformation targetInfo = FindCharacterInformation(target);
        if (sourceInfo == null || targetInfo == null)
        {
            Debug.LogWarning("[CombatReadinessPanel] 拖拽换位：未找到对应角色信息栏，信息未同步");
            return;
        }

        (sourceInfo.transform.position, targetInfo.transform.position) =
            (targetInfo.transform.position, sourceInfo.transform.position);
    }

    /// <summary>
    /// 从拖拽事件位置射线检测 UI，从命中节点向上查找 tag=character 的角色节点。
    /// </summary>
    private Transform FindCharacter(PointerEventData eventData)
    {
        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            //角色标签可能设在槽位根或展示层上，向上逐级查找；以本面板为界不越界匹配
            Transform node = result.gameObject.transform;
            while (node != null && node != transform)
            {
                if (node.CompareTag(CharacterTag)) return node;
                node = node.parent;
            }
        }
        return null;
    }

    /// <summary>
    /// 查找角色节点关联的信息栏：优先子层级，其次父节点（标签设在展示层上时信息栏是兄弟节点）。
    /// </summary>
    private CharacterInformation FindCharacterInformation(Transform character)
    {
        CharacterInformation info = character.GetComponentInChildren<CharacterInformation>(true);
        if (info == null && character.parent != null) info = character.parent.GetComponentInChildren<CharacterInformation>(true);
        return info;
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
