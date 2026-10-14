using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Gear;
using UnityEngine;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能参数库：SkillTable 的运行时查询门面（表现层数据源，战斗接入后同源取值）。
    /// 职责三件：定位（角色 id + 技能类型 → 技能行，走 skillId 编码约定）、取值（占位符键 →
    /// 该等级数值，值 = 初值 + 每级成长×(等级-1)）、描述替换（{键} / {键:percent} 占位符）。
    /// 表经 DataTableManager 加载并全期缓存；params 在加载期解析一次建索引，查询期零解析。
    /// 描述占位符与 params 键的对应关系在加载期校验，缺键大声报错（防配错静默显示原文）。
    /// </summary>
    public static class SkillDataLibrary
    {
        // 描述文本占位符：{键} 或 {键:percent}（percent = 数值×100 加 % 展示，数据侧始终存规范 float）
        private static readonly Regex PlaceholderPattern = new Regex(@"\{([a-z][a-z0-9_]*)(?::(percent))?\}");

        // 技能表缓存（EnsureLoadedAsync 成功后可用）
        private static DataTable<SkillRow, int> table;

        // 技能 id → 解析后的参数集
        private static Dictionary<int, SkillParam[]> paramIndex;

        // 加载任务（并发调用共享同一次加载）
        private static Task<DataTable<SkillRow, int>> loadTask;

        /// <summary>
        /// 确保技能表已加载并建立参数索引（幂等，并发调用共享同一次加载）；
        /// 表加载失败时保持未加载状态，下次调用重试。
        /// </summary>
        public static async Task EnsureLoadedAsync()
        {
            if (paramIndex != null) return;

            loadTask ??= DataTableManager.Instance.LoadTableAsync<SkillRow, int>("SkillTable");
            table = await loadTask;
            if (table == null) return;

            // 加载期建索引 + 验键：desc 中的占位符必须能从 params 取到，缺键当场报错
            paramIndex = new Dictionary<int, SkillParam[]>(table.Count);
            for (int i = 0; i < table.Rows.Count; i++)
            {
                SkillRow row = table.Rows[i];
                SkillParam[] list = SkillParam.ParseList(row.Params);
                if (!string.IsNullOrWhiteSpace(row.Params) && list.Length == 0)
                {
                    Debug.LogError($"[SkillDataLibrary] 技能 {row.SkillId} 的 params 列无法解析：\"{row.Params}\"");
                }
                paramIndex[row.SkillId] = list;

                HashSet<string> keys = new HashSet<string>();
                for (int j = 0; j < list.Length; j++) keys.Add(list[j].Key);
                foreach (Match match in PlaceholderPattern.Matches(row.Desc ?? string.Empty))
                {
                    if (!keys.Contains(match.Groups[1].Value))
                    {
                        Debug.LogError($"[SkillDataLibrary] 技能 {row.SkillId} 描述占位符 {{{match.Groups[1].Value}}} 在 params 中无对应键：\"{row.Params}\"");
                    }
                }
            }
        }

        /// <summary>
        /// 定位技能行：角色 id + 技能类型 → skillId → 行。
        /// skillId 编码与 SkillTable 约定一致：角色数值（unitId 去掉前两位字母）×100
        /// + 槽位（1 普攻 / 2 E / 3 Q / 4 天赋），如 CH0221 的小技能 = 221×100+2 = 22102。
        /// </summary>
        /// <param name="unitId">角色 id，与 CharacterTable 主键对齐（如 CH0221）。</param>
        /// <param name="skillType">技能类型（SkillRow.SkillTypes 取值）。</param>
        /// <param name="row">命中的技能行；未命中时为 null。</param>
        /// <returns>命中返回 true。</returns>
        public static bool TryGetSkillRow(string unitId, string skillType, out SkillRow row)
        {
            row = null;
            if (paramIndex == null || string.IsNullOrEmpty(unitId) || unitId.Length <= 2) return false;

            int slot = skillType switch
            {
                SkillRow.SkillTypes.Normal => 1,
                SkillRow.SkillTypes.E => 2,
                SkillRow.SkillTypes.Burst => 3,
                SkillRow.SkillTypes.Talent => 4,
                _ => 0,
            };
            if (slot == 0 || !int.TryParse(unitId.Substring(2), out int roleNumber)) return false;

            return table.TryGet(roleNumber * 100 + slot, out row);
        }

        /// <summary>
        /// 取指定技能在指定等级的全部占位符值（值集一次解析成型，调用方按键取用）。
        /// 等级夹取到 [1, maxLevel]；表未加载或该技能无参数时返回 false。
        /// </summary>
        /// <param name="row">技能行。</param>
        /// <param name="level">技能等级。</param>
        /// <param name="values">占位符键 → 该等级数值；未命中时为 null。</param>
        /// <returns>命中返回 true。</returns>
        public static bool TryResolveValues(SkillRow row, int level, out Dictionary<string, float> values)
        {
            values = null;
            if (row == null || paramIndex == null
                || !paramIndex.TryGetValue(row.SkillId, out SkillParam[] list) || list.Length == 0)
            {
                return false;
            }

            int clamped = Mathf.Clamp(level, 1, Mathf.Max(row.MaxLevel, 1));
            values = new Dictionary<string, float>(list.Length);
            for (int i = 0; i < list.Length; i++) values[list[i].Key] = list[i].GetValue(clamped);
            return true;
        }

        /// <summary>
        /// 表现层主入口：把技能描述中的占位符替换为该技能该等级的实际数值。
        /// 缺键占位符保留原文并告警（配错当场可见）；无参数技能原样返回描述。
        /// </summary>
        /// <param name="row">技能行。</param>
        /// <param name="level">技能等级。</param>
        /// <returns>替换后的描述文本；行或描述为空时返回 null。</returns>
        public static string FormatDesc(SkillRow row, int level)
        {
            if (row == null || string.IsNullOrEmpty(row.Desc)) return null;
            if (!TryResolveValues(row, level, out Dictionary<string, float> values)) return row.Desc;

            return PlaceholderPattern.Replace(row.Desc, match =>
            {
                string key = match.Groups[1].Value;
                if (!values.TryGetValue(key, out float value))
                {
                    Debug.LogWarning($"[SkillDataLibrary] 描述占位符无对应参数：技能 {row.SkillId} 的 {{{key}}}");
                    return match.Value;
                }
                return match.Groups[2].Success ? FormatPercent(value) : FormatNumber(value);
            });
        }

        // 数值展示：去尾零的十进制文本（float 二进制尾差在此收敛，50 显示为 "50"、0.4 显示为 "0.4"）
        private static string FormatNumber(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // 百分比展示：×100 加 %（数据 0.8 显示为 "80%"）
        private static string FormatPercent(float value)
        {
            return (value * 100f).ToString("0.###", CultureInfo.InvariantCulture) + "%";
        }
    }
}
