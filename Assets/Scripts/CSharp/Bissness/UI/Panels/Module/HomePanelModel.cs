using Framework.Gameplay.Player;

/// <summary>
/// 主城面板展示数据模型（MVC 的 M）：Control 只读本模型驱动 View，不直接触碰数据系统。
/// 数据源逐步接入——玩家信息已接 PlayerInfoManager，其余为 TODO 占位，接入后只改这里。
/// </summary>
public class HomePanelModel
{
    #region 玩家展示（已接入）

    /// <summary>
    /// 玩家信息（昵称/等级/经验/头像名），来自持久化存档。
    /// </summary>
    public PlayerInfo PlayerInfo => PlayerInfoManager.Instance.Data;

    #endregion

    #region 状态栏（TODO 待接入数据源）

    /// <summary>
    /// TODO 体力：接入体力系统后由其管理器提供（含上限与恢复倒计时）。
    /// </summary>
    public int Stamina => 0;

    /// <summary>
    /// TODO 信用点（金钱）：接入货币系统后改为 InventoryManager.Instance.GetCount(货币ItemId)。
    /// </summary>
    public long Money => 0;

    /// <summary>
    /// TODO 星光石（付费货币）：接入货币系统后改为 InventoryManager.Instance.GetCount(货币ItemId)。
    /// </summary>
    public long StarlightStone => 0;

    /// <summary>
    /// TODO 顶部时间文本：接入本地/服务器时间后格式化（如 MM/dd HH:mm）。
    /// </summary>
    public string MenuTimeText => string.Empty;

    #endregion
}
