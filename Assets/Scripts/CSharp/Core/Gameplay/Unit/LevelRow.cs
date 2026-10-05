using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 等级成长表行：对应 Assets/Module/Unit/Character/Excel/LevelTable.xlsx。
    /// 全角色共享一条成长曲线：最终属性 = base × multiplier(level)；
    /// 曲线形状调整只改本表（逐级一行），不逐角色填成长率。
    /// 升级所需经验不进表（公式是死的，写在业务逻辑）。
    /// </summary>
    [MessagePackObject]
    public sealed class LevelRow : IConfigTableRow<int>
    {
        // 等级，全表唯一主键
        [Key("level")]
        public int Level;

        // 该等级的全属性成长系数（相对 1 级白板值）
        [Key("multiplier")]
        public float Multiplier;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => Level;
    }
}
