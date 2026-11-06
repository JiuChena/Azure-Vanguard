using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Framework.Gameplay.Battle
{
    /// <summary>
    /// 波次时间点条目（WaveTable T 列解析结果）：
    /// `30[EN_Solider_AR_0001:2,EN0002:5]` = 本波次第 30 秒时，依次创建 2 个 1 级 EN_Solider_AR_0001、5 个 1 级 EN0002；
    /// `30[EN_Solider_AR_0001@10:2]` = 同时刻创建 2 个 10 级 EN_Solider_AR_0001（`@等级` 可选，缺省 1 级）
    /// （创建节奏 = CombatBattleConfig.SpawnInterval，表只定时刻/数量/等级）。
    /// </summary>
    public sealed class WaveSpawnEntry
    {
        /// <summary>波内时刻（秒）；0 = 波开始即触发。</summary>
        public float Time;

        /// <summary>该时刻的出兵清单（表内书写顺序，依次创建）。</summary>
        public readonly List<WaveSpawnUnit> Units = new List<WaveSpawnUnit>();

        /// <summary>单个出兵组：敌人 id × 数量 × 等级。</summary>
        public readonly struct WaveSpawnUnit
        {
            public readonly string EnemyId;
            public readonly int Count;
            public readonly int Level;
            public WaveSpawnUnit(string enemyId, int count, int level)
            {
                EnemyId = enemyId; Count = count; Level = level;
            }
        }

        // 全/半角括号/逗号/冒号/顿号/等级符统一按分隔符认，Excel 手输不挑输入法
        private static readonly char[] OpenBrackets = { '[', '（', '(', '【' };
        private static readonly char[] CloseBrackets = { ']', '）', ')', '】' };
        private static readonly char[] GroupSeparators = { ',', '，', '、' };
        private static readonly char[] ValueSeparators = { ':', '：' };
        private static readonly char[] LevelSeparators = { '@', '＠' };

        /// <summary>
        /// 解析一格时间点文本（宽容全半角；格内允许多条目以分号分隔，常规一格一条）。
        /// 空格返回空列表；格式非法的条目 LogError 定位后跳过（不炸整行）。
        /// </summary>
        public static List<WaveSpawnEntry> ParseCell(string text, string context)
        {
            var entries = new List<WaveSpawnEntry>();
            if (string.IsNullOrWhiteSpace(text)) return entries;

            string[] chunks = text.Split(';');
            for (int i = 0; i < chunks.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(chunks[i])) continue;
                if (TryParse(chunks[i], context, out WaveSpawnEntry entry)) entries.Add(entry);
            }
            return entries;
        }

        private static bool TryParse(string chunk, string context, out WaveSpawnEntry entry)
        {
            entry = null;
            string text = chunk.Trim();

            int open = text.IndexOfAny(OpenBrackets);
            if (open <= 0)
            {
                Debug.LogError($"[WaveTable] 时间点条目缺时刻或括号：'{text}'（{context}）");
                return false;
            }
            int close = text.IndexOfAny(CloseBrackets, open + 1);
            if (close < 0)
            {
                Debug.LogError($"[WaveTable] 时间点条目括号未闭合：'{text}'（{context}）");
                return false;
            }

            // 时刻：括号前缀，秒；负数拒绝
            if (!float.TryParse(text.Substring(0, open).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float time) || time < 0f)
            {
                Debug.LogError($"[WaveTable] 时间点时刻非法：'{text}'（{context}）");
                return false;
            }

            var parsed = new WaveSpawnEntry { Time = time };
            string[] groups = text.Substring(open + 1, close - open - 1).Split(GroupSeparators);
            for (int i = 0; i < groups.Length; i++)
            {
                string[] pair = groups[i].Trim().Split(ValueSeparators);
                if (pair.Length != 2 || !int.TryParse(pair[1].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int count) || count < 1)
                {
                    Debug.LogError($"[WaveTable] 出兵组非法（应为 敌人id[@等级]:数量）：'{groups[i]}'（{context}）");
                    continue;
                }

                // 敌人 id 段可带等级后缀 id@level（@/＠ 均收）；无 @ = 1 级；等级 < 1 拒绝
                string idPart = pair[0].Trim();
                string enemyId = idPart;
                int level = 1;
                int at = idPart.IndexOfAny(LevelSeparators);
                if (at >= 0)
                {
                    if (!int.TryParse(idPart.Substring(at + 1).Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out level) || level < 1)
                    {
                        Debug.LogError($"[WaveTable] 出兵组等级非法（@ 后应为 ≥1 整数）：'{groups[i]}'（{context}）");
                        continue;
                    }
                    enemyId = idPart.Substring(0, at).Trim();
                }

                if (string.IsNullOrEmpty(enemyId))
                {
                    Debug.LogError($"[WaveTable] 出兵组敌人 id 为空：'{groups[i]}'（{context}）");
                    continue;
                }
                parsed.Units.Add(new WaveSpawnUnit(enemyId, count, level));
            }

            if (parsed.Units.Count == 0)
            {
                Debug.LogError($"[WaveTable] 时间点条目无有效出兵组：'{text}'（{context}）");
                return false;
            }
            entry = parsed;
            return true;
        }
    }
}
