using System.Threading.Tasks;
using Core.Gear;
using Framework.Gameplay.Inventory;
using Framework.Gameplay.Item;
using Framework.Gameplay.Player;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 主城面板控制层（MVC 的 C）：持有各 View 分区组件与展示数据模型。
/// 职责：把 <see cref="HomePanelModel"/> 的数据写入 View，把 View 的交互转成业务调用；
/// 自身不持有 UI 组件引用，一律通过 View 分区访问。
/// </summary>
public class HomePanel : PanelBase
{
    [Header("View 分区")]
    //背景分区：视频背景/渲染贴图/音频
    [SerializeField] private HomePanel_BG bg;
    //左上玩家展示分区：头像/昵称/等级/经验
    [SerializeField] private HomePanel_PlayerDisplay playerDisplay;
    //底部菜单栏分区：学生/培养/商店/抽卡/任务入口
    [SerializeField] private HomePanel_MenuBar menuBar;
    //右上状态栏分区：体力/货币/设置
    [SerializeField] private HomePanel_Status status;
    //右侧玩法入口分区：战斗/活动
    [SerializeField] private HomePanel_Gameplay gameplay;

    [Header("数据")]
    //面板图集
    [SerializeField] private SpriteAtlas panelAtlas;
    //稀有度背景（下标 = 稀有度-1）；素材未入库前留空，代码保留默认图
    //TODO 稀有度边框素材入库后在 Inspector 按稀有度顺序配置
    [SerializeField] private Sprite[] rarityBgs;

    [Header("状态栏添加模式")]
    //信用点加号单次添加数量：点击一次 status.moneyAddButton 直接增加的背包持有量，调试期用，Inspector 可调
    [SerializeField] private int statusMoneyAddAmount = 10000;
    //星光石加号单次添加数量：点击一次 status.starlightStoneAddButton 直接增加的背包持有量，调试期用，Inspector 可调
    [SerializeField] private int statusStarlightStoneAddAmount = 1000;
    //状态栏货币槽位绑定的物品 id（ItemTable 主键，Inspector 可调）——代码不设常量表
    [SerializeField] private string statusMoneyItemId = "Currency_CreditPoint";
    [SerializeField] private string statusStarlightStoneItemId = "Currency_StarlightStone";

    //展示数据模型，Control 的唯一数据来源（货币槽位 id 注入）
    private HomePanelModel model;

    // 当前持有的背景视频资源租约，用于跨角色切换时归还旧引用以及销毁时释放。
    private ResourceLease<VideoClip> currentVideoLease;

    // 界面控件显隐状态标志位：用于背景展开/收起按钮切换主界面 UI 元素的展示与隐藏。
    private bool gearDisplay = true;

    /// <summary>
    /// 初始化面板事件监听与按钮点击回调绑定。
    /// </summary>
    protected override void EventInit()
    {
        //背包变更 → 状态栏货币文本跟随刷新
        EventCenter.Instance.Register(EventNames.BagUpdated, OnBagUpdated);

        //养成入口：打开角色列表面板（养成 UI 第一层；列表项点击进 CultivatePanel，下一轮接线）
        menuBar.studentsButton.onClick.AddListener(() =>
            PanelManager.Instance.OpenPanel<CharacterListPanel>(UIPath.CHARACTERLISTPANEL, UILayer.Mid));
        menuBar.cultivatedButton.onClick.AddListener(() =>
            PanelManager.Instance.OpenPanel<CultivatePanel>(UIPath.CULTIVATEPANEL, UILayer.Mid));
        //TODO 菜单栏：menuBar.studentsButton/storeButton/gachaButton → 打开对应功能面板
        //TODO 菜单栏：menuBar.taskButton → 打开任务面板
        //调试期添加模式：加号直接增加持有量，变更即存即广播 BagUpdated → RefreshStatus 自动跟随
        status.moneyAddButton.onClick.AddListener(() => InventoryManager.Instance.AddItem(statusMoneyItemId, statusMoneyAddAmount));
        status.starlightStoneAddButton.onClick.AddListener(() => InventoryManager.Instance.AddItem(statusStarlightStoneItemId, statusStarlightStoneAddAmount));
        //TODO 状态栏：status.staminaAddButton → 跳转体力获取途径（体力域暂缓）；获取途径面板就绪后两个货币加号替换为 OpenPanel 跳转
        //TODO 状态栏：status.settingsButton → 打开设置面板；status.bgExpandButton → 背景展开/收起
        //玩法：gameplay.combatButton → 进入出击/关卡选择面板
        if (gameplay.combatButton != null)
        {
            gameplay.combatButton.onClick.AddListener(() => PanelManager.Instance.OpenPanel<CombatSelectPanel>(UIPath.COMBATSELECTPANEL, UILayer.Mid));
        }
        //TODO 玩家展示：playerDisplay.avatarBtn → 打开头像选择面板，选中后 PlayerInfoManager.Instance.SetAvatar
        
        status.bgExpandButton.onClick.AddListener(() =>
        {
            gearDisplay = !gearDisplay;
            animator.SetBool("Display", gearDisplay);
        });
    }

    /// <summary>
    /// 初始化面板组件与数据模型，启动初次刷新与背景视频异步加载。
    /// </summary>
    protected override void ComponentInit()
    {
        //模型随序列化 id 就绪后构建（货币槽位绑定 ItemTable 主键）
        model = new HomePanelModel(statusMoneyItemId, statusStarlightStoneItemId);

        Timer.Instance.AddTimerEvent(1, () =>
        {
            animator.SetBool("Display", true);
        });
        RefreshPlayerDisplay();
        RefreshStatus();

        // 异步加载并播放主城背景视频（资源租约绑定 ResourceScope 实现随面板关闭级联卸载）
        _ = LoadAndPlayBackgroundVideoAsync();

        //TODO RefreshMenuTime()：顶部时间文本（menuBar.timeText，节点就绪后启用）
    }

    /// <summary>
    /// 面板销毁时撤销事件监听并释放背景视频资源，防止向已销毁的组件写入。
    /// </summary>
    protected override void OnDestroy()
    {
        EventCenter.Instance.Unregister(EventNames.BagUpdated, OnBagUpdated);

        // 停止背景视频播放并清理贴图残影
        if (bg != null) bg.StopVideo();

        // 释放视频租约（ResourceScope 也会统一级联兜底释放）
        currentVideoLease?.Dispose();
        currentVideoLease = null;

        base.OnDestroy();
    }

    /// <summary>
    /// 背包变更回调：刷新状态栏货币文本。
    /// </summary>
    private void OnBagUpdated()
    {
        RefreshStatus();
    }

    /// <summary>
    /// 刷新右上状态栏：体力/金钱/星光石文本，数据一律从 Model 读。
    /// </summary>
    private void RefreshStatus()
    {
        if (status == null) return;

        //TODO 体力：体力系统接入后展示真实值（含上限）
        if (status.staminaText != null) status.staminaText.text = model.Stamina.ToString();
        if (status.moneyText != null) status.moneyText.text = model.Money.ToString();
        if (status.starlightStoneText != null) status.starlightStoneText.text = model.StarlightStone.ToString();
    }

    /// <summary>
    /// 面板每帧轮询回调（暂未启用高频轮询业务）。
    /// </summary>
    protected override void OnUpdate()
    {
        //TODO 顶部时间文本按秒刷新；用时间戳比较或 TimerEventManager，避免每帧写 text 产生 GC
    }

    #region 玩家展示

    /// <summary>
    /// 刷新玩家展示分区：头像/昵称/等级/经验条。
    /// </summary>
    private void RefreshPlayerDisplay()
    {
        if (playerDisplay == null) return;

        PlayerInfo info = model.PlayerInfo;

        //头像：单图按需（名片头像名存 PlayerInfo.avatarName）；未命中保留场景默认图
        _ = RefreshPlayerAvatarAsync(info.avatarName);

        if (playerDisplay.playerName != null) playerDisplay.playerName.text = info.nickName;
        if (playerDisplay.levelText != null) playerDisplay.levelText.text = $"Lv.{info.level}";

        //经验进度条：按父级宽度比例填充（左侧拉伸）；若素材改用 Filled 类型则换成 fillAmount
        if (playerDisplay.levelImg != null)
        {
            float progress = info.maxExp > 0 ? Mathf.Clamp01((float)info.exp / info.maxExp) : 0f;
            RectTransform fill = playerDisplay.levelImg.rectTransform;
            fill.anchorMax = new Vector2(progress, fill.anchorMax.y);
        }
    }

    /// <summary>
    /// 异步填充名片头像；回包时若玩家已更换头像（存档重载等）则丢弃结果。
    /// </summary>
    /// <param name="avatarName">头像图集键名。</param>
    /// <returns>异步任务。</returns>
    private async Task RefreshPlayerAvatarAsync(string avatarName)
    {
        Sprite avatar = await IconLibrary.GetAsync(avatarName);
        if (avatar == null || playerDisplay == null) return;
        if (model != null && model.PlayerInfo != null && model.PlayerInfo.avatarName != avatarName) return;
        if (playerDisplay.avatarImg != null) playerDisplay.avatarImg.sprite = avatar;
    }

    #endregion

    #region 名片展示角色（槽位节点待搭建）

    /// <summary>
    /// 设置名片展示角色（图标资源名 + 稀有度，稀有度从 1 开始）。
    /// TODO 新版式中尚未搭建名片槽位节点；节点就绪后在 HomePanel_PlayerDisplay 增加槽位字段，
    /// 并改由角色数据驱动（入参角色 ID，稀有度/图标名走角色配置表，展示列表持久化到 PlayerInfo）。
    /// </summary>
    /// <param name="slot">角色槽位 Transform 节点。</param>
    /// <param name="chIconName">角色头像图标资源名。</param>
    /// <param name="rarity">角色稀有度星级（从 1 开始）。</param>
    private void SetDisplayCharacter(Transform slot, string chIconName, int rarity)
    {
        if (slot == null) return;

        //稀有度背景：下标 = 稀有度-1，越界或未配置时保留默认图
        Image rarityBg = slot.Find("BGRarity")?.GetComponent<Image>();
        if (rarityBg != null && rarityBgs != null && rarity >= 1 && rarity <= rarityBgs.Length && rarityBgs[rarity - 1] != null)
        {
            rarityBg.sprite = rarityBgs[rarity - 1];
        }

        //角色图标：单图按需，取不到保留默认图（槽位节点未搭建，TODO 接角色数据后改传 unitId 走 CharacterAvatarKey）
        Image chIcon = slot.Find("CHIcon")?.GetComponent<Image>();
        if (chIcon != null) _ = FillSlotIconAsync(chIcon, chIconName);
    }

    /// <summary>
    /// 异步填充槽位图标。
    /// </summary>
    /// <param name="chIcon">展示图标的 Image 组件。</param>
    /// <param name="iconName">图标资源名。</param>
    /// <returns>异步任务。</returns>
    private static async Task FillSlotIconAsync(Image chIcon, string iconName)
    {
        Sprite icon = await IconLibrary.GetAsync(iconName);
        if (icon == null || chIcon == null) return;
        chIcon.sprite = icon;
    }

    #endregion

    #region 背景视频

    /// <summary>
    /// 异步加载并播放主城背景视频。
    /// 根据玩家当前佩戴的头像提取角色 ID（如 CH0221），通过 AddressableManager 获取视频资源租约并绑定至当前面板的 ResourceScope。
    /// 面板关闭销毁时，将由 ResourceScope 级联卸载视频资源。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task LoadAndPlayBackgroundVideoAsync()
    {
        // 校验背景分区组件与玩家展示数据
        if (bg == null || model == null || model.PlayerInfo == null) return;

        // 提取角色 ID（例如从 "CHAvatar_PlayerAvatar_CH0221" 中解析出 "CH0221"）
        string unitId = GetUnitIdFromAvatar(model.PlayerInfo.avatarName);
        if (string.IsNullOrEmpty(unitId)) unitId = "CH0221";

        try
        {
            // 通过面板自身的 ResourceScope 申请视频资源租约，实现随面板关闭级联自动卸载
            ResourceLease<VideoClip> lease = await AddressableManager.Instance.AcquireAssetAsync<VideoClip>(unitId, ResourceScope);

            // 异步等待返回后检查面板实例是否已被销毁
            if (this == null || bg == null)
            {
                lease.Dispose();
                return;
            }

            // 释放上一次播放持有的租约
            currentVideoLease?.Dispose();
            currentVideoLease = lease;

            // 开始播放背景视频
            if (lease.Asset != null)
            {
                bg.PlayVideo(lease.Asset);
            }
            else
            {
                Debug.LogWarning($"[HomePanel] 背景视频加载成功但 Asset 为空: {unitId}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HomePanel] 加载背景视频失败: {unitId}, 异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 从名片头像名称中提取角色 ID。
    /// 规则：移除前缀 "CHAvatar_PlayerAvatar_"，若格式不匹配或为空则回退至默认 ID "CH0221"。
    /// </summary>
    /// <param name="avatarName">玩家名片头像名称（如 "CHAvatar_PlayerAvatar_CH0221"）。</param>
    /// <returns>提取出的角色标识（如 "CH0221"）。</returns>
    private string GetUnitIdFromAvatar(string avatarName)
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
}
