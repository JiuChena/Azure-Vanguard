using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能升级消耗表行：对应 Assets/Module/Unit/Character/Excel/SkillLevelTable.xlsx。
    /// 行 = 目标等级 L（2..10），内容 = 升到 L 级的消耗；全部技能（普攻/天赋/E/Q）共用一张表。
    /// 当前数值为占位样板（信用点 1200×L + 升级手册 L），待设计定案。
    /// </summary>
    [MessagePackObject]
    public sealed class SkillLevelRow : IConfigTableRow<int>
    {
        // 目标等级 L（本行 = 升到 L 的消耗），全表唯一主键
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
