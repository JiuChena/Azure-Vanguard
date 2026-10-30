using Core.Gear;
using System.Collections.Generic;
using MessagePack;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次配置表行：对应 Assets/Module/Battle/Excel/WaveTable.xlsx。
    /// 一行 = 一个波次内一个出怪点的时间点排程；同波次多出怪点 = 多行（同 waveId）。
    /// StageTable.waveIds（分号序列）引用波次 id（waveId 列）驱动波次推进顺序；行主键 = id 列。
    /// T 列格式：`30[EN_Solider_AR_0001:2,EN0002:5]` = 本波次第 30 秒依次创建 2 个 1 级 EN_Solider_AR_0001、5 个 1 级 EN0002；
    /// 敌人 id 可带等级后缀 `EN_Solider_AR_0001@10`（10 级，缺省 1 级；等级成长 = EnemyRow 1 级基准 × LevelTable.multiplier）
    /// （创建间隔在 CombatManager 配置，表只定时刻/数量/等级）。
    /// </summary>
    [MessagePackObject]
    public sealed class WaveRow : IConfigTableRow<string>
    {
        // 行主键（如 WT0001），全表唯一；波次本身由 waveId 标识
        [Key("id")]
        public string Id;

        // 波次 id（如 W0001）；对应 StageTable.waveIds 元素，同波次多行同值
        [Key("waveId")]
        public string WaveId;

        // 出怪点 id；CombatManager 场景绑定（pointId → Transform）
        [Key("enemyCreatePointId")]
        public string EnemyCreatePointId;

        // 行进路线 id；CombatManager.routes 按 routeId 绑定路点序列（敌人沿折线依次走，走完转守点）；
        // 空 = 无路线，意图策略兜底直奔守点
        [Key("routeId")]
        public string RouteId;

        // 时间点条目列 T1~T8（空格跳过，不够再加列）
        [Key("t1")] public string T1;
        [Key("t2")] public string T2;
        [Key("t3")] public string T3;
        [Key("t4")] public string T4;
        [Key("t5")] public string T5;
        [Key("t6")] public string T6;
        [Key("t7")] public string T7;
        [Key("t8")] public string T8;

        [IgnoreMember]
        private List<WaveSpawnEntry> timePoints;

        /// <summary>本行时间点排程（懒解析 T1~T8 按列序合并；空行返回空列表）。</summary>
        [IgnoreMember]
        public IReadOnlyList<WaveSpawnEntry> TimePoints => timePoints ??= ParseTimePoints();

        private List<WaveSpawnEntry> ParseTimePoints()
        {
            var points = new List<WaveSpawnEntry>();
            string context = $"wave={WaveId}, row={Id}";
            ParseColumn(T1, context, points);
            ParseColumn(T2, context, points);
            ParseColumn(T3, context, points);
            ParseColumn(T4, context, points);
            ParseColumn(T5, context, points);
            ParseColumn(T6, context, points);
            ParseColumn(T7, context, points);
            ParseColumn(T8, context, points);
            return points;
        }

        private static void ParseColumn(string text, string context, List<WaveSpawnEntry> into)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            into.AddRange(WaveSpawnEntry.ParseCell(text, context));
        }

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;
    }
}
