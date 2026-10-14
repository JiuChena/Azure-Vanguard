using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 技能占位符参数：一个占位符键的初值与每级成长，取值 = 初值 + 成长×(等级-1)。
    /// 来源为 SkillTable params 列（格式 {键:初值:成长, ...}）；键同时是描述文本占位符名
    /// 与表现层/触发 SO 的查询键，三处同源一套命名空间。
    /// </summary>
    public readonly struct SkillParam
    {
        // 占位符键（小写字母开头，仅小写字母/数字/下划线）
        public readonly string Key;

        // 1 级值
        public readonly float Initial;

        // 每级成长（可为负 = 随等级递减，0 = 不随等级变化）
        public readonly float Growth;

        // 键的合法形态（与导出工具的校验规则保持一致）
        private static readonly Regex KeyPattern = new Regex("^[a-z][a-z0-9_]*$");

        public SkillParam(string key, float initial, float growth)
        {
            Key = key;
            Initial = initial;
            Growth = growth;
        }

        /// <summary>解析 params 列文本；空列或无合法项返回空数组（导出工具已做格式校验的第一道关）。</summary>
        public static SkillParam[] ParseList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<SkillParam>();

            string body = raw.Trim();
            if (!body.StartsWith("{") || !body.EndsWith("}")) return Array.Empty<SkillParam>();
            body = body.Substring(1, body.Length - 2);

            string[] items = body.Split(',');
            List<SkillParam> list = new List<SkillParam>(items.Length);
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < items.Length; i++)
            {
                string token = items[i].Trim();
                if (token.Length == 0) continue;

                string[] parts = token.Split(':');
                if (parts.Length != 3) continue;
                string key = parts[0].Trim();
                if (!KeyPattern.IsMatch(key) || !seen.Add(key)) continue;
                if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float initial)) continue;
                if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float growth)) continue;
                list.Add(new SkillParam(key, initial, growth));
            }
            return list.ToArray();
        }

        /// <summary>按等级取值：初值 + 成长×(等级-1)，1 级即初值。</summary>
        public float GetValue(int level)
        {
            return Initial + Growth * (level - 1);
        }
    }
}
