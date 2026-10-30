using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Buff
{
    /// <summary>
    /// Buff 表行：对应 Assets/Module/Battle/Excel/BuffTable.xlsx。
    /// buff 元数据（名称/描述/图标）供展示层按 buffId 查询；数值逻辑在 Buff 实例内（逻辑自洽），
    /// 本表不含任何数值列。buffId 为全局唯一主键，单位身上的 buff 经 IBuff.BuffId 标注同一编码。
    /// </summary>
    [MessagePackObject]
    public sealed class BuffRow : IConfigTableRow<int>
    {
        // buff id，全表唯一主键（单位身上 buff 注册时标注的编码）
        [Key("buffId")]
        public int BuffId;

        // buff 名称（显示用）
        [Key("name")]
        public string Name;

        // buff 描述文本（显示用）
        [Key("desc")]
        public string Desc;

        // buff 图标地址（IconLibrary 可寻址的 sprite 名）
        [Key("icon")]
        public string Icon;

        // 加成属性（逗号分隔多属性，如 "critrate,critdmg"；BuffDataLibrary 映射 StatType）
        [Key("statType")]
        public string StatType;

        // 1 级加成值（逗号分隔与 statType 一一对应，如 "0.10,0.30"）
        [Key("value")]
        public string Value;

        // 每级增量（逗号分隔与 statType 一一对应；随技能等级成长 value + growth×(level-1)）
        [Key("growth")]
        public string Growth;

        // 持续秒数（0 = 永久）
        [Key("duration")]
        public float Duration;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public int PrimaryKey => BuffId;
    }
}
