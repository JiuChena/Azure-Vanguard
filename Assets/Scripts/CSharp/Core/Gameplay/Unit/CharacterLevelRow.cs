using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 角色等级表行：对应 Assets/Module/Unit/Character/Excel/CharacterLevelTable.xlsx
    /// （2026-11-02 由 LevelTable 与 UpgradeCostTable 合并改名）。
    /// 全角色共享一条成长曲线：最终属性 = base × multiplier(level)；
    /// expToNext = 本级升到下一级所需经验（满级 0，经验以本表为准）；
    /// costItems = 本级升到下一级的消耗（满级空）。武器升级沿用同一张表（经验/消耗共用口径）。
    /// </summary>
    [MessagePackObject]
    public sealed class CharacterLevelRow : IConfigTableRow<int>
    {
        // 等级，全表唯一主键（1..80）
        [Key("level")]
        public int Level;

        // 该等级的全属性成长系数（相对 1 级白板值）
        [Key("multiplier")]
        public float Multiplier;

        // 升到下一级所需经验（满级 0；口径 = 80 × level^1.5 向上取整，已配入表）
        [Key("expToNext")]
        public int ExpToNext;

        // 升到下一级的消耗 itemId:count 逗号分隔（满级空）
        [Key("costItems")]
        public string CostItems;

        /// <summary>解析后的升级消耗。</summary>
        [IgnoreMember]
        public ItemCost[] CostList => ItemCost.ParseList(CostItems);

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => Level;
    }
}
