using System;
using System.Collections.Generic;

namespace Framework.Gameplay.Unit
{
    /// <summary>
    /// 角色标签声明（CharacterTable.tags 列的解析结果）：标签名 + 方括号内附带属性。
    /// 列格式：逗号分隔多标签；单个标签 `TagName` 或 `TagName[attr1;attr2]`，方括号为可选附带属性
    /// （当前仅环境标签带心情，如 Indoor[Feeling_0]；后续拓展直接往括号里加分号隔开即可）。
    /// 标签图标地址约定 = `Tag_&lt;TagName&gt;`；附带属性首键同时是心情 emoji 的 sprite 名（如 Feeling_0）。
    /// </summary>
    public readonly struct CharacterTagDecl
    {
        // 标签名（如 MainDPS / Indoor），与图标地址 Tag_<name> 对应
        public readonly string Name;

        // 方括号内附带属性原始 token（分号分隔；无括号为空数组）
        public readonly string[] Extras;

        public CharacterTagDecl(string name, string[] extras)
        {
            Name = name;
            Extras = extras;
        }

        /// <summary>解析 tags 列原始串；空串/碎片/空名滤除，方括号缺失或未闭合按纯名处理（表配错不炸运行时）。</summary>
        public static CharacterTagDecl[] ParseList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<CharacterTagDecl>();
            string[] parts = raw.Split(',');
            List<CharacterTagDecl> tags = new List<CharacterTagDecl>(parts.Length);
            foreach (string part in parts)
            {
                string token = part.Trim();
                if (token.Length == 0) continue;

                int open = token.IndexOf('[');
                int close = open < 0 ? -1 : token.IndexOf(']', open + 1);
                if (close < 0)
                {
                    tags.Add(new CharacterTagDecl(token, Array.Empty<string>()));
                    continue;
                }

                string name = token.Substring(0, open).Trim();
                if (name.Length == 0) continue;
                string extrasRaw = token.Substring(open + 1, close - open - 1);
                string[] extras = string.IsNullOrWhiteSpace(extrasRaw)
                    ? Array.Empty<string>()
                    : extrasRaw.Split(';');
                for (int i = 0; i < extras.Length; i++) extras[i] = extras[i].Trim();
                tags.Add(new CharacterTagDecl(name, extras));
            }
            return tags.ToArray();
        }
    }
}
