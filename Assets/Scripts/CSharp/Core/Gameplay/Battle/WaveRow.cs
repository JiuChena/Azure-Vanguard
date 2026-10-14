using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次配置表行：对应 Assets/Module/Battle/Excel/WaveTable.xlsx。
    /// 一行 = 一个波次内一种敌人的出兵配置；同波次多敌人 = 多行（同 waveIndex）。
    /// LevelConfig.waveIds（有序主键数组）驱动波次推进顺序，WaveIndex 作同关内排序冗余校验。
    /// </summary>
    [MessagePackObject]
    public sealed class WaveRow : IConfigTableRow<string>
    {
        // 波次 ID，全表唯一主键（如 W0001）；被 LevelConfig.waveIds 有序引用
        [Key("id")]
        public string Id;

        // 波次序号（1 起）；同关内按此列升序推进，与 waveIds 顺序互为校验
        [Key("waveIndex")]
        public int WaveIndex;

        // 敌人 id（EnemyTable 主键）
        [Key("enemyId")]
        public string EnemyId;

        // 该波次该敌人出兵数量
        [Key("count")]
        public int Count;

        // 波内出兵间隔（秒）：逐个生成两敌人之间的间隔
        [Key("spawnInterval")]
        public float SpawnInterval;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
