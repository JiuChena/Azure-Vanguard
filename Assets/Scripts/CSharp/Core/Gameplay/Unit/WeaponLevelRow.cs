using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 武器等级成长表行：对应 Assets/Module/Unit/Weapon/Excel/WeaponLevelTable.xlsx。
    /// 全武器共享一条成长曲线（与角色 LevelTable 同形状、独立可调）：
    /// 武器白值与主属性 = 1 级值 × multiplier(weaponLevel)。
    /// </summary>
    [MessagePackObject]
    public sealed class WeaponLevelRow : IConfigTableRow<int>
    {
        // 武器等级，全表唯一主键
        [Key("level")]
        public int Level;

        // 该等级的白值与主属性成长系数（相对 1 级值）
        [Key("multiplier")]
        public float Multiplier;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => Level;
    }
}
