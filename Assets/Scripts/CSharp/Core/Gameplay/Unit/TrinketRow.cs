using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 装备配置表行：对应 Assets/Module/Unit/Trinket/Excel/TrinketTables.xlsx 的 TrinketTable 页。
    /// 行 = 款式 × 品质（id 形如 Watch_Tier1_Q3）；装备不可升级不可洗练，掉落即定形，全部词条表配死。
    /// id 为权威（实例只存 itemId，类别/合奏/词条/图标全部查表派生）；同 kind 的 ensembleId
    /// 全行同值（合奏挂款式级），品质共享图标（品质走 UI 边框色区分）。
    /// </summary>
    [MessagePackObject]
    public sealed class TrinketRow : IConfigTableRow<string>
    {
        // 装备 id（款式_Q品质 形态），全表唯一主键，实例存储的权威 id
        [Key("id")]
        public string Id;

        // 装备名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 装备描述
        [Key("desc")]
        public string Desc;

        // 类别 id（Badge/Bag/Charm/… 开放清单），合奏"类别互异"判定与背包筛选用
        [Key("category")]
        public string Category;

        // 款式（如 Watch_Tier1，即图标名去 Trinket_Icon_ 前缀）
        [Key("kind")]
        public string Kind;

        // 品质 1-5（表配死，只影响数值不影响合奏）
        [Key("quality")]
        public int Quality;

        // 合奏 id（空 = 无合奏）；同 kind 全行同值，三件套判定挂款式级
        [Key("ensembleId")]
        public string EnsembleId;

        // 主属性键：复用 CharacterStatsCalculator 词表（atk/hp/def 平面键 + 百分比键加法区）
        [Key("mainStatType")]
        public string MainStatType;

        // 主属性值
        [Key("mainStatValue")]
        public float MainStatValue;

        // 副词条 1 键（固定 3 条副词条，数值比主词条小，无洗练等其他系统）
        [Key("subStat1Type")]
        public string SubStat1Type;

        // 副词条 1 值
        [Key("subStat1Value")]
        public float SubStat1Value;

        // 副词条 2 键
        [Key("subStat2Type")]
        public string SubStat2Type;

        // 副词条 2 值
        [Key("subStat2Value")]
        public float SubStat2Value;

        // 副词条 3 键
        [Key("subStat3Type")]
        public string SubStat3Type;

        // 副词条 3 值
        [Key("subStat3Value")]
        public float SubStat3Value;

        // 图标地址（png 文件名，IconLibrary 单图通道；品质共享图标）
        [Key("icon")]
        public string Icon;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
