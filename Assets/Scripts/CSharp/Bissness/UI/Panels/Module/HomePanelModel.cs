using Framework.Gameplay.Inventory;
using Framework.Gameplay.Item;
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
    /// TODO 体力：接入体力系统后由其管理器提供（含上限与恢复倒计时）。体力是玩家字段，不走背包。
    /// </summary>
    public int Stamina => 0;

    /// <summary>
    /// 金钱（基础流通货币）：货币是物品，余额 = 背包持有量。
    /// </summary>
    public long Money => InventoryManager.Instance.GetCount(CurrencyIds.Gold);

    /// <summary>
    /// 星光石（付费货币）：余额 = 背包持有量。
    /// </summary>
    public long StarlightStone => InventoryManager.Instance.GetCount(CurrencyIds.StarlightStone);

    /// <summary>
    /// TODO 顶部时间文本：接入本地/服务器时间后格式化（如 MM/dd HH:mm）。
    /// </summary>
    public string MenuTimeText => string.Empty;

    #endregion
}
