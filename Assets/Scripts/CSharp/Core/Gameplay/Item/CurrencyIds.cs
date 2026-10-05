namespace Framework.Gameplay.Item
{
    /// <summary>
    /// 货币物品 ID 常量表：货币是物品（type=货币），余额即背包持有量（InventoryManager.GetCount）。
    /// ItemTable 中必须存在对应 id 的行；体力不是物品（时间恢复/上限是玩家字段），不走此表。
    /// </summary>
    public static class CurrencyIds
    {
        /// <summary>金钱（基础流通货币，商店购买/出售结算）。</summary>
        public const int Gold = 1;

        /// <summary>星光石（付费货币）。</summary>
        public const int StarlightStone = 2;
    }
}
