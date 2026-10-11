using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 升级消耗表行：对应 Assets/Module/Unit/Character/Excel/UpgradeCostTable.xlsx。
    /// 行 = 当前等级 L（1..79），内容 = 从 L 升到 L+1 的信用点消耗（信用点 = 2）；
    /// 角色与武器共用一张表（等级上限都随角色突破走）。当前数值为占位样板（500×L），待设计定案。
    /// </summary>
    [MessagePackObject]
    public sealed class UpgradeCostRow : IConfigTableRow<int>
    {
        // 当前等级 L（本行 = L → L+1 的消耗），全表唯一主键
        [Key("id")]
        public int Id;

        // 升级消耗 itemId:count 逗号分隔（信用点 = 2）
        [Key("costItems")]
        public string CostItems;

        /// <summary>解析后的升级消耗。</summary>
        [IgnoreMember]
        public ItemCost[] CostList => ItemCost.ParseList(CostItems);

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => Id;
    }
}
