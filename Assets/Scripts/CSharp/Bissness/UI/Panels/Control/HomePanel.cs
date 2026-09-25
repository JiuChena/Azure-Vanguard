using Core.Gear;
using Framework.Gameplay.Player;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

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
    //图标图集（头像 + 角色Icon），按存档资源名从中取 sprite
    [SerializeField] private SpriteAtlas avatarAtlas;
    //稀有度背景（下标 = 稀有度-1）；素材未入库前留空，代码保留默认图
    //TODO 稀有度边框素材入库后在 Inspector 按稀有度顺序配置
    [SerializeField] private Sprite[] rarityBgs;

    //展示数据模型，Control 的唯一数据来源
    private readonly HomePanelModel model = new HomePanelModel();

    private bool bgStatus = false;

    protected override void EventInit()
    {
        //TODO 菜单栏：menuBar.studentsButton/cultivatedButton/storeButton/gachaButton → 打开对应功能面板
        //TODO 菜单栏：menuBar.taskButton → 打开任务面板
        //TODO 状态栏：status.staminaAddButton/moneyAddButton/starlightStoneAddButton → 跳转对应获取途径
        //TODO 状态栏：status.settingsButton → 打开设置面板；status.bgExpandButton → 背景展开/收起
        //TODO 玩法：gameplay.combatButton → 进入战斗；gameplay.activitiesButton → 打开活动面板
        //TODO 玩家展示：playerDisplay.avatarBtn → 打开头像选择面板，选中后 PlayerInfoManager.Instance.SetAvatar
        
        status.bgExpandButton.onClick.AddListener(() =>
        {
            
        });
    }

    protected override void ComponentInit()
    {
        Timer.Instance.AddTimerEvent(1, () =>
        {
            animator.SetBool("Display", true);
        });
        RefreshPlayerDisplay();

        //TODO RefreshStatus()：体力/信用点/星光石文本，模型数据源接入后启用
        //TODO RefreshMenuTime()：顶部时间文本（menuBar.timeText，节点就绪后启用）
        //TODO 背景：bg.bgVideoPlayer 播放主城背景视频（循环/静音/淡入策略待定）
    }

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

        //头像：按存档资源名从图集取，取不到保留场景默认图
        Sprite avatar = avatarAtlas != null ? avatarAtlas.GetSprite(info.avatarName) : null;
        if (avatar != null && playerDisplay.avatarImg != null)
        {
            playerDisplay.avatarImg.sprite = avatar;
        }

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

    #endregion

    #region 名片展示角色（槽位节点待搭建）

    /// <summary>
    /// 设置名片展示角色（图标资源名 + 稀有度，稀有度从 1 开始）。
    /// TODO 新版式中尚未搭建名片槽位节点；节点就绪后在 HomePanel_PlayerDisplay 增加槽位字段，
    /// 并改由角色数据驱动（入参角色 ID，稀有度/图标名走角色配置表，展示列表持久化到 PlayerInfo）。
    /// </summary>
    private void SetDisplayCharacter(Transform slot, string chIconName, int rarity)
    {
        if (slot == null) return;

        //稀有度背景：下标 = 稀有度-1，越界或未配置时保留默认图
        Image rarityBg = slot.Find("BGRarity")?.GetComponent<Image>();
        if (rarityBg != null && rarityBgs != null && rarity >= 1 && rarity <= rarityBgs.Length && rarityBgs[rarity - 1] != null)
        {
            rarityBg.sprite = rarityBgs[rarity - 1];
        }

        //角色图标：按资源名从图集取，取不到保留默认图
        Image chIcon = slot.Find("CHIcon")?.GetComponent<Image>();
        Sprite icon = avatarAtlas != null ? avatarAtlas.GetSprite(chIconName) : null;
        if (chIcon != null && icon != null)
        {
            chIcon.sprite = icon;
        }
    }

    #endregion
}
