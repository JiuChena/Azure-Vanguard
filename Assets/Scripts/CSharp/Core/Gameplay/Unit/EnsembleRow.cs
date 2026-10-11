using System;
using System.Collections.Generic;
using System.Globalization;
using Core.Gear;
using MessagePack;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 合奏配置表行：对应 Assets/Module/Unit/Trinket/Excel/TrinketTables.xlsx 的 EnsembleTable 页。
    /// 合奏只认三件套（触发 = 三件同 ensembleId 且三件类别互异，两件/一件无任何效果）；
    /// bonus 列为属性键值对串（`key:value;key:value`，键复用 CharacterStatsCalculator 词表）。
    /// </summary>
    [MessagePackObject]
    public sealed class EnsembleRow : IConfigTableRow<string>
    {
        // 合奏 id，全表唯一主键（TrinketTable.ensembleId 列指向此表）
        [Key("id")]
        public string Id;

        // 合奏名称（显示用，后续接入本地化时替换为多语言键）
        [Key("name")]
        public string Name;

        // 合奏描述
        [Key("desc")]
        public string Desc;

        // 加成键值对串（如 atk_pct:0.1;hp_pct:0.1），解析见 GetBonus
        [Key("bonus")]
        public string Bonus;

        /// <summary>表行主键。</summary>
        [IgnoreMember]
        public string PrimaryKey => Id;

        /// <summary>
        /// 解析 bonus 列为键/值平行数组；空串/碎片/坏键值滤除（表配错不炸运行时，同 tags 列容错口径）。
        /// </summary>
        /// <param name="keys">解析出的属性键数组（保序）；无有效项为空数组。</param>
        /// <param name="values">与 keys 一一对应的加成值数组。</param>
        public void GetBonus(out string[] keys, out float[] values)
        {
            keys = Array.Empty<string>();
            values = Array.Empty<float>();
            if (string.IsNullOrWhiteSpace(Bonus)) return;

            string[] parts = Bonus.Split(';');
            var keyList = new List<string>(parts.Length);
            var valueList = new List<float>(parts.Length);
            foreach (string part in parts)
            {
                string token = part.Trim();
                if (token.Length == 0) continue;

                string[] pair = token.Split(':');
                if (pair.Length != 2) continue;

                string key = pair[0].Trim();
                if (key.Length == 0) continue;
                if (!float.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) continue;

                keyList.Add(key);
                valueList.Add(value);
            }

            keys = keyList.ToArray();
            values = valueList.ToArray();
        }
    }
}
